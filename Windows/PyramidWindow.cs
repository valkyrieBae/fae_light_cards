using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace FaeLightCards
{
    public class PyramidWindow : Window
    {
        private readonly Plugin plugin;

        private const int PyramidRowCount = 5;
        private const int PyramidCardCount = 15;
        private const float PyramidTopPadding = 20f;
        private const float PyramidRowSpacing = 15f;
        private const float PyramidCardSpacing = 10f;
        private const float PyramidStaticAreaExtraWidth = 90f;
        private const float PyramidWindowExtraWidth = 40f;
        private const float PyramidWindowBottomPadding = 10f;
        private const float PyramidInactiveRowScale = 0.5f;
        private const float PyramidActiveRowScale = 1.0f;
        private const float PyramidRowScaleLerpRate = 7.0f;
        private const float PyramidFlipDuration = 0.35f;
        private const int FlipTargetSparkleCount = 15;

        private bool shouldResetPosition = false;
        private Vector2? dragTargetPosition = null;
        private float hoverTime = 0f;
        private bool isFirstDraw = true;
        private readonly float[] flipAnimationTimer = new float[PyramidCardCount];
        private readonly float[] rowCurrentScaleMultipliers = new float[PyramidRowCount + 1] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
        private Vector2 currentWindowPos = Vector2.Zero;
        public Dictionary<string, Vector2> PlayerRowScreenPositions { get; } = new Dictionary<string, Vector2>();
        public Vector2 CalculatedSize { get; private set; } = new Vector2(400, 500);

        private readonly record struct PyramidLayoutMetrics(
            float[] RowY,
            float[] RowScale,
            float[] RowCardWidth,
            float[] RowCardHeight,
            float[] RowSpacing,
            float StaticMaxRowWidth,
            float PyramidAreaWidth,
            Vector2 WindowSize)
        {
            public PyramidCardLayout GetCardLayout(int index, Vector2 windowPos)
            {
                var (row, col) = RulesEngine.GetCardPos(index);
                float rowContentWidth = row * RowCardWidth[row] + (row - 1) * RowSpacing[row];
                float startX = (PyramidAreaWidth - rowContentWidth) / 2f;
                Vector2 localPos = new(
                    startX + col * (RowCardWidth[row] + RowSpacing[row]),
                    RowY[row]);

                return new PyramidCardLayout(
                    row,
                    windowPos + localPos,
                    new Vector2(RowCardWidth[row], RowCardHeight[row]),
                    RowScale[row]);
            }
        }

        private readonly record struct PyramidCardLayout(
            int Row,
            Vector2 Position,
            Vector2 Size,
            float Scale)
        {
            public float Width => Size.X;
            public float Height => Size.Y;
            public Vector2 Center => Position + Size * 0.5f;
        }

        public PyramidWindow(Plugin plugin) : base("Pyramid###FaeLightCardsPyramidWindow")
        {
            this.plugin = plugin;
            this.IsOpen = false; // Closed by default
            this.RespectCloseHotkey = false;

            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(100, 100),
                MaximumSize = new Vector2(3000, 2000)
            };

            // Default position: left side of screen
            this.Position = new Vector2(50, 200);
            this.PositionCondition = ImGuiCond.FirstUseEver;
        }

        public void ResetPosition()
        {
            shouldResetPosition = true;
        }

        private ISharedImmediateTexture GetCardTexture(Card card)
        {
            return plugin.CardDeckService.GetCardTexture(card);
        }

        private ISharedImmediateTexture GetCardBackTexture()
        {
            return plugin.CardDeckService.GetCardBackTexture();
        }

        private Vector2 GetPyramidBaseCardSize()
        {
            return plugin.CardDeckService.GetPyramidBaseCardDisplaySize();
        }

        private float GetBaseScale()
        {
            // Base off the user's specific PyramidScale (scaled down slightly by default to fit nicely)
            return plugin.Configuration.PyramidScale * 0.75f;
        }

        private static float GetPseudoRandom(int index, float seedFactor)
        {
            float val = MathF.Sin(index * seedFactor) * 43758.5453f;
            return val - MathF.Floor(val);
        }

        private static uint GetRainbowColor(float time, float offset, float opacity)
        {
            // Hue cycles over time (takes 5 seconds for a full loop, frequency 0.2), with offset to make each particle out of sync
            float hue = (time * 0.2f + offset) % 1.0f;
            if (hue < 0) hue += 1.0f;

            float h = hue * 6.0f;
            int sector = (int)h;
            float f = h - sector;
            float q = 1.0f - f;
            float t = f;

            var (r, g, b) = sector switch
            {
                0 => (1.0f, t, 0.0f),
                1 => (q, 1.0f, 0.0f),
                2 => (0.0f, 1.0f, t),
                3 => (0.0f, q, 1.0f),
                4 => (t, 0.0f, 1.0f),
                _ => (1.0f, 0.0f, q)
            };

            return ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, opacity));
        }

        public float GetPyramidCardScale(int index)
        {
            int row = RulesEngine.GetRowIndex(index);
            float mult = rowCurrentScaleMultipliers[row];
            return GetBaseScale() * mult;
        }

        public float GetPyramidCardWidth(int index)
        {
            return GetPyramidBaseCardSize().X * GetPyramidCardScale(index);
        }

        public float GetPyramidCardHeight(int index)
        {
            return GetPyramidBaseCardSize().Y * GetPyramidCardScale(index);
        }

        public Vector2 GetPyramidCardScreenPos(int index)
        {
            return CreatePyramidLayoutMetrics().GetCardLayout(index, currentWindowPos).Position;
        }

        public Vector2 GetPlayerRowScreenPos(string name)
        {
            if (PlayerRowScreenPositions.TryGetValue(name, out var pos))
            {
                return pos;
            }
            // Fallback
            var metrics = CreatePyramidLayoutMetrics();
            return currentWindowPos + new Vector2(metrics.StaticMaxRowWidth + 100f, 200f);
        }

        private PyramidLayoutMetrics CreatePyramidLayoutMetrics()
        {
            float baseScale = GetBaseScale();
            var baseCardSize = GetPyramidBaseCardSize();
            float[] rowY = new float[PyramidRowCount + 1];
            float[] rowScale = new float[PyramidRowCount + 1];
            float[] rowCardWidth = new float[PyramidRowCount + 1];
            float[] rowCardHeight = new float[PyramidRowCount + 1];
            float[] rowSpacing = new float[PyramidRowCount + 1];

            float currentY = PyramidTopPadding;
            for (int row = 1; row <= PyramidRowCount; row++)
            {
                rowScale[row] = baseScale * rowCurrentScaleMultipliers[row];
                rowCardWidth[row] = baseCardSize.X * rowScale[row];
                rowCardHeight[row] = baseCardSize.Y * rowScale[row];
                rowSpacing[row] = PyramidCardSpacing * (rowScale[row] / baseScale);
                rowY[row] = currentY;
                currentY += rowCardHeight[row] + PyramidRowSpacing;
            }

            // Keep the pyramid aligned statically in its left-side area.
            float staticMaxRowWidth = PyramidRowCount * baseCardSize.X * baseScale
                                      + (PyramidRowCount - 1) * PyramidCardSpacing
                                      + PyramidStaticAreaExtraWidth * baseScale;
            float pyramidAreaWidth = staticMaxRowWidth + PyramidWindowExtraWidth;
            var windowSize = new Vector2(pyramidAreaWidth, currentY + PyramidWindowBottomPadding);

            return new PyramidLayoutMetrics(
                rowY,
                rowScale,
                rowCardWidth,
                rowCardHeight,
                rowSpacing,
                staticMaxRowWidth,
                pyramidAreaWidth,
                windowSize);
        }

        public override void PreDraw()
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoScrollbar
                                   | ImGuiWindowFlags.NoScrollWithMouse
                                   | ImGuiWindowFlags.NoTitleBar
                                   | ImGuiWindowFlags.NoBackground
                                   | ImGuiWindowFlags.NoResize;

            if (plugin.Configuration.IsLocked)
            {
                flags |= ImGuiWindowFlags.NoMove;
            }

            this.Flags = flags;

            if (dragTargetPosition.HasValue)
            {
                this.Position = dragTargetPosition.Value;
                this.PositionCondition = ImGuiCond.Always;
                dragTargetPosition = null;
            }
            else if (isFirstDraw || shouldResetPosition)
            {
                var viewport = ImGui.GetMainViewport();
                float x = viewport.Pos.X + 50f;
                float y = viewport.Pos.Y + 150f;

                this.Position = new Vector2(x, y);
                this.PositionCondition = ImGuiCond.Always;

                if (shouldResetPosition)
                {
                    shouldResetPosition = false;
                }

                if (isFirstDraw)
                {
                    int startActiveRow = plugin.GameState.ActiveRow;
                    for (int r = 1; r <= PyramidRowCount; r++)
                    {
                        rowCurrentScaleMultipliers[r] = (r == startActiveRow) ? PyramidActiveRowScale : PyramidInactiveRowScale;
                    }
                }
                isFirstDraw = false;
            }
            else
            {
                this.PositionCondition = ImGuiCond.None;
            }

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        }

        public override void Draw()
        {
            if (!ShouldDrawPyramid())
            {
                return;
            }

            currentWindowPos = ImGui.GetWindowPos();
            int activeRow = plugin.GameState.ActiveRow;
            float dt = ImGui.GetIO().DeltaTime;
            UpdateRowScaleAnimations(activeRow, dt);

            var metrics = CreatePyramidLayoutMetrics();
            ApplyPyramidWindowLayout(metrics);
            UpdatePyramidDragState(dt);
            DrawPyramidCards(metrics, activeRow, dt);
        }

        private bool ShouldDrawPyramid()
        {
            return (plugin.GameState.ActivePhase == GamePhase.Pyramid || plugin.GameState.ActivePhase == GamePhase.TieChoice)
                   && plugin.GameState.Pyramid.Count >= PyramidCardCount;
        }

        private void UpdateRowScaleAnimations(int activeRow, float dt)
        {
            for (int row = 1; row <= PyramidRowCount; row++)
            {
                float target = row == activeRow ? PyramidActiveRowScale : PyramidInactiveRowScale;
                rowCurrentScaleMultipliers[row] += (target - rowCurrentScaleMultipliers[row]) * dt * PyramidRowScaleLerpRate;
                rowCurrentScaleMultipliers[row] = Math.Clamp(rowCurrentScaleMultipliers[row], PyramidInactiveRowScale, PyramidActiveRowScale);
            }
        }

        private void ApplyPyramidWindowLayout(PyramidLayoutMetrics metrics)
        {
            ImGui.SetWindowSize(metrics.WindowSize);
            CalculatedSize = metrics.WindowSize;
            plugin.UiState.PyramidSize = CalculatedSize;
            plugin.UiState.PyramidPosition = ImGui.GetWindowPos();
            plugin.UiState.IsPyramidVisible = true;

            if (plugin.Configuration.ShowDebugBounds)
            {
                var wp = ImGui.GetWindowPos();
                UiDebugDraw.DrawBounds(wp, wp + metrics.WindowSize);
            }
        }

        private void UpdatePyramidDragState(float dt)
        {
            if (plugin.Configuration.IsLocked)
            {
                return;
            }

            if (ImGui.IsWindowHovered())
            {
                hoverTime += dt;
                if (hoverTime >= plugin.Configuration.TooltipDelay)
                {
                    ImGui.SetTooltip("Drag to move the pyramid.\nLock position in settings (/faecards).");
                }
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var delta = ImGui.GetIO().MouseDelta;
                    dragTargetPosition = ImGui.GetWindowPos() + delta;
                    plugin.UiState.LastMovedElementName = "Pyramid";
                    plugin.UiState.LastMovedElementCoords = dragTargetPosition.Value;
                }
            }
            else
            {
                hoverTime = 0f;
            }
        }

        private void DrawPyramidCards(PyramidLayoutMetrics metrics, int activeRow, float dt)
        {
            var drawList = ImGui.GetWindowDrawList();
            for (int index = 0; index < PyramidCardCount; index++)
            {
                DrawPyramidCard(drawList, index, metrics.GetCardLayout(index, currentWindowPos), activeRow, dt);
            }
        }

        private void DrawPyramidCard(ImDrawListPtr drawList, int index, PyramidCardLayout layout, int activeRow, float dt)
        {
            var card = plugin.GameState.Pyramid[index];
            bool isFlipped = plugin.GameState.PyramidFlipped[index];
            var wrap = GetPyramidCardTexture(card, index, isFlipped, dt, out float flipScaleX);
            if (wrap != null)
            {
                UiCardRenderer.DrawCardWith3DEffects(drawList, wrap.Handle, layout.Position, layout.Size, layout.Scale, flipScaleX);
            }

            DrawPyramidMatchHighlight(drawList, index, card, isFlipped, layout);
            DrawCurrentFlipTarget(drawList, index, isFlipped, layout, activeRow);
            DrawMatchedCards(drawList, index, layout);
        }

        private IDalamudTextureWrap? GetPyramidCardTexture(Card card, int index, bool isFlipped, float dt, out float flipScaleX)
        {
            if (isFlipped)
            {
                if (flipAnimationTimer[index] < PyramidFlipDuration)
                {
                    flipAnimationTimer[index] = Math.Min(flipAnimationTimer[index] + dt, PyramidFlipDuration);
                }
            }
            else
            {
                flipAnimationTimer[index] = 0f;
            }

            float tFlip = flipAnimationTimer[index] / PyramidFlipDuration;
            flipScaleX = 1f;

            if (!isFlipped)
            {
                return GetCardBackTexture().GetWrapOrEmpty();
            }

            if (tFlip < 0.5f)
            {
                flipScaleX = 1f - tFlip * 2f;
                return GetCardBackTexture().GetWrapOrEmpty();
            }

            flipScaleX = tFlip * 2f - 1f;
            return GetCardTexture(card).GetWrapOrEmpty();
        }

        private void DrawPyramidMatchHighlight(ImDrawListPtr drawList, int index, Card card, bool isFlipped, PyramidCardLayout layout)
        {
            if (!isFlipped || plugin.GameState.PyramidMatchedCardsLists[index].Count != 0 || plugin.IsLocalDealer)
            {
                return;
            }

            bool matchesHand = plugin.GameState.DisplayedHand.Any(c => c != null && c.Rank == card.Rank);
            if (!matchesHand)
            {
                return;
            }

            float time = (float)ImGui.GetTime();
            float pulse = 0.6f + MathF.Sin(time * 6f) * 0.3f;
            float outlineThickness = MathF.Max(3.0f, 4.0f * layout.Scale);
            float outlineRounding = layout.Width * 0.068f;
            drawList.AddRect(
                layout.Position,
                layout.Position + layout.Size,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.84f, 0.0f, pulse)),
                outlineRounding,
                ImDrawFlags.None,
                outlineThickness);
        }

        private void DrawCurrentFlipTarget(ImDrawListPtr drawList, int index, bool isFlipped, PyramidCardLayout layout, int activeRow)
        {
            bool canFlipCurrentCard = !plugin.RulesEngine.HasPendingMatches() && plugin.TurnManager.DealerPhaseTransitionTimer <= 0f;
            if (index != plugin.GameState.CurrentFlipIndex || isFlipped || layout.Row != activeRow || !canFlipCurrentCard)
            {
                return;
            }

            var originalCursorPos = ImGui.GetCursorPos();
            ImGui.SetCursorScreenPos(layout.Position);
            if (ImGui.InvisibleButton($"##pyramid_flip_btn_{index}", layout.Size))
            {
                plugin.GameController.HandleFlipPyramidCard();
            }
            ImGui.SetCursorPos(originalCursorPos);

            DrawFlipTargetSparkles(drawList, layout);
        }

        private void DrawFlipTargetSparkles(ImDrawListPtr drawList, PyramidCardLayout layout)
        {
            float time = (float)ImGui.GetTime();
            for (int sparkle = 0; sparkle < FlipTargetSparkleCount; sparkle++)
            {
                float randX = 0.05f + 0.90f * GetPseudoRandom(sparkle, 12.9898f);
                float randY = 0.05f + 0.90f * GetPseudoRandom(sparkle, 78.233f);

                float speedX = 2.0f + GetPseudoRandom(sparkle, 19.34f) * 2.0f;
                float speedY = 2.0f + GetPseudoRandom(sparkle, 47.85f) * 2.0f;
                float amplitudeX = 8f + GetPseudoRandom(sparkle, 31.45f) * 12f;
                float amplitudeY = 8f + GetPseudoRandom(sparkle, 56.12f) * 12f;

                float driftX = MathF.Sin(time * speedX + sparkle * 1.7f) * amplitudeX;
                float driftY = MathF.Cos(time * speedY + sparkle * 2.3f) * amplitudeY;

                var spotPos = layout.Position
                              + new Vector2(randX * layout.Width, randY * layout.Height)
                              + new Vector2(driftX, driftY) * layout.Scale;

                float sizeOsc = 19f + MathF.Sin(time * 6f + sparkle * 2f) * 5f;
                float opacity = 0.7f + MathF.Sin(time * 6f + sparkle * 1.5f) * 0.3f;
                uint color = GetRainbowColor(time, sparkle * 0.2f, opacity);

                HandWindow.DrawSparkle(drawList, spotPos, sizeOsc * layout.Scale, time * 3f + sparkle, color);
            }
        }

        private void DrawMatchedCards(ImDrawListPtr drawList, int index, PyramidCardLayout layout)
        {
            var matchedCards = plugin.GameState.PyramidMatchedCardsLists[index];
            var matchedRotations = plugin.GameState.PyramidMatchedCardsRotationsLists[index];
            for (int matchIndex = 0; matchIndex < matchedCards.Count; matchIndex++)
            {
                var matchedWrap = GetCardTexture(matchedCards[matchIndex]).GetWrapOrEmpty();
                if (matchedWrap != null)
                {
                    float rotation = matchIndex < matchedRotations.Count ? matchedRotations[matchIndex] : 0f;
                    UiCardRenderer.DrawRotatedCard(drawList, matchedWrap.Handle, layout.Center, layout.Size, layout.Scale, rotation);
                }
            }
        }

        public override void PostDraw()
        {
            ImGui.PopStyleVar();
        }
    }
}

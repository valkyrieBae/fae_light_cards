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

        private bool shouldResetPosition = false;
        private Vector2? dragTargetPosition = null;
        private float hoverTime = 0f;
        private bool isFirstDraw = true;
        private readonly float[] flipAnimationTimer = new float[15];
        private readonly float[] rowCurrentScaleMultipliers = new float[6] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
        private Vector2 currentWindowPos = Vector2.Zero;
        public Dictionary<string, Vector2> PlayerRowScreenPositions { get; } = new Dictionary<string, Vector2>();
        public Vector2 CalculatedSize { get; private set; } = new Vector2(400, 500);

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
            var (row, col) = RulesEngine.GetCardPos(index);
            float baseScale = GetBaseScale();
            var baseCardSize = GetPyramidBaseCardSize();
            float baseCardW = baseCardSize.X;
            float baseCardH = baseCardSize.Y;

            float currentY = 20f;
            float[] rowY = new float[6];
            float[] rowHeight = new float[6];
            float maxRowW = 0f;

            for (int r = 1; r <= 5; r++)
            {
                float rowScale = baseScale * rowCurrentScaleMultipliers[r];
                rowHeight[r] = baseCardH * rowScale;

                float w = baseCardW * rowScale;
                float spacing = 10f * (rowScale / baseScale);
                float rowW = r * w + (r - 1) * spacing;
                if (rowW > maxRowW)
                {
                    maxRowW = rowW;
                }
            }

            for (int r = 1; r <= 5; r++)
            {
                rowY[r] = currentY;
                currentY += rowHeight[r] + 15f;
            }

            // Keep the pyramid aligned statically in its left-side area
            float staticMaxRowW = 5f * baseCardW * baseScale + 4f * 10f + 90f * baseScale;
            float pyramidAreaW = staticMaxRowW + 40f;

            float rowScaleCurrent = baseScale * rowCurrentScaleMultipliers[row];
            float wCurrent = baseCardW * rowScaleCurrent;
            float spacingCurrent = 10f * (rowScaleCurrent / baseScale);
            float rowWCurrent = row * wCurrent + (row - 1) * spacingCurrent;
            float startX = (pyramidAreaW - rowWCurrent) / 2f;

            Vector2 localPos = new Vector2(
                startX + col * (wCurrent + spacingCurrent),
                rowY[row]
            );

            // No stack offset; all stacked cards are perfectly centered!
            Vector2 stackOffset = Vector2.Zero;

            return currentWindowPos + localPos + stackOffset;
        }

        public Vector2 GetPlayerRowScreenPos(string name)
        {
            if (PlayerRowScreenPositions.TryGetValue(name, out var pos))
            {
                return pos;
            }
            // Fallback
            float baseScale = GetBaseScale();
            float baseCardW = GetPyramidBaseCardSize().X;
            float staticMaxRowW = 5f * baseCardW * baseScale + 4f * 10f + 90f * baseScale;
            return currentWindowPos + new Vector2(staticMaxRowW + 100f, 200f);
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
                    for (int r = 1; r <= 5; r++)
                    {
                        rowCurrentScaleMultipliers[r] = (r == startActiveRow) ? 1.0f : 0.5f;
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
            if ((plugin.GameState.ActivePhase != GamePhase.Pyramid && plugin.GameState.ActivePhase != GamePhase.TieChoice) || plugin.GameState.Pyramid.Count < 15)
            {
                return;
            }

            currentWindowPos = ImGui.GetWindowPos();

            float baseScale = GetBaseScale();
            var baseCardSize = GetPyramidBaseCardSize();
            float baseCardW = baseCardSize.X;
            float baseCardH = baseCardSize.Y;

            int activeRow = plugin.GameState.ActiveRow;
            float dt = ImGui.GetIO().DeltaTime;
            for (int r = 1; r <= 5; r++)
            {
                float target = (r == activeRow) ? 1.0f : 0.5f;
                rowCurrentScaleMultipliers[r] += (target - rowCurrentScaleMultipliers[r]) * dt * 7.0f;
                rowCurrentScaleMultipliers[r] = Math.Clamp(rowCurrentScaleMultipliers[r], 0.5f, 1.0f);
            }

            float currentY = 20f;
            float[] rowY = new float[6];
            float[] rowHeight = new float[6];
            float maxRowW = 0f;

            for (int r = 1; r <= 5; r++)
            {
                float rowScale = baseScale * rowCurrentScaleMultipliers[r];
                rowHeight[r] = baseCardH * rowScale;

                float w = baseCardW * rowScale;
                float spacing = 10f * (rowScale / baseScale);
                float rowW = r * w + (r - 1) * spacing;
                if (rowW > maxRowW)
                {
                    maxRowW = rowW;
                }
            }

            for (int r = 1; r <= 5; r++)
            {
                rowY[r] = currentY;
                currentY += rowHeight[r] + 15f;
            }

            // Keep the pyramid aligned statically in its left-side area
            float staticMaxRowW = 5f * baseCardW * baseScale + 4f * 10f + 90f * baseScale;
            float pyramidAreaW = staticMaxRowW + 40f;

            float windowW = pyramidAreaW;
            float windowH = currentY + 10f;

            ImGui.SetWindowSize(new Vector2(windowW, windowH));
            CalculatedSize = new Vector2(windowW, windowH);
            plugin.UiState.PyramidSize = CalculatedSize;
            plugin.UiState.PyramidPosition = ImGui.GetWindowPos();
            plugin.UiState.IsPyramidVisible = true;

            if (plugin.Configuration.ShowDebugBounds)
            {
                var wp = ImGui.GetWindowPos();
                UiDebugDraw.DrawBounds(wp, wp + new Vector2(windowW, windowH));
            }

            if (!plugin.Configuration.IsLocked)
            {
                if (ImGui.IsWindowHovered())
                {
                    hoverTime += ImGui.GetIO().DeltaTime;
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

            for (int i = 0; i < 15; i++)
            {
                var (r, col) = RulesEngine.GetCardPos(i);
                float rowScale = baseScale * rowCurrentScaleMultipliers[r];
                float w = baseCardW * rowScale;
                float h = baseCardH * rowScale;
                float spacing = 10f * (rowScale / baseScale);
                float rowW = r * w + (r - 1) * spacing;
                float startX = (pyramidAreaW - rowW) / 2f; // centered in pyramidAreaW

                Vector2 drawPos = ImGui.GetWindowPos() + new Vector2(
                    startX + col * (w + spacing),
                    rowY[r]
                );

                var card = plugin.GameState.Pyramid[i];
                var isFlipped = plugin.GameState.PyramidFlipped[i];

                // Update flip animation timer
                float flipDuration = 0.35f;
                if (isFlipped)
                {
                    if (flipAnimationTimer[i] < flipDuration)
                    {
                        flipAnimationTimer[i] = Math.Min(flipAnimationTimer[i] + ImGui.GetIO().DeltaTime, flipDuration);
                    }
                }
                else
                {
                    flipAnimationTimer[i] = 0f;
                }

                float tFlip = flipAnimationTimer[i] / flipDuration;
                float flipScaleX = 1f;
                IDalamudTextureWrap? wrap = null;

                if (isFlipped)
                {
                    if (tFlip < 0.5f)
                    {
                        flipScaleX = 1f - tFlip * 2f;
                        wrap = GetCardBackTexture().GetWrapOrEmpty();
                    }
                    else
                    {
                        flipScaleX = tFlip * 2f - 1f;
                        wrap = GetCardTexture(card).GetWrapOrEmpty();
                    }
                }
                else
                {
                    wrap = GetCardBackTexture().GetWrapOrEmpty();
                }

                if (wrap != null)
                {
                    var drawList = ImGui.GetWindowDrawList();
                    UiCardRenderer.DrawCardWith3DEffects(drawList, wrap.Handle, drawPos, new Vector2(w, h), rowScale, flipScaleX);
                }

                // If this pyramid card is flipped, unmatched, and matches any card in the player's hand, draw a pulsing gold outline!
                bool matchesHand = false;
                if (isFlipped && plugin.GameState.PyramidMatchedCardsLists[i].Count == 0 && !plugin.IsLocalDealer)
                {
                    matchesHand = plugin.GameState.DisplayedHand.Any(c => c != null && c.Rank == card.Rank);
                    if (matchesHand)
                    {
                        var drawList = ImGui.GetWindowDrawList();
                        float time = (float)ImGui.GetTime();
                        float pulse = 0.6f + MathF.Sin(time * 6f) * 0.3f;
                        float outlineThickness = MathF.Max(3.0f, 4.0f * rowScale);
                        float outlineRounding = w * 0.068f;
                        drawList.AddRect(
                            drawPos,
                            drawPos + new Vector2(w, h),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.84f, 0.0f, pulse)),
                            outlineRounding,
                            ImDrawFlags.None,
                            outlineThickness
                        );
                    }
                }

                bool canFlipCurrentCard = !plugin.RulesEngine.HasPendingMatches() && plugin.TurnManager.DealerPhaseTransitionTimer <= 0f;
                if (i == plugin.GameState.CurrentFlipIndex && !isFlipped && RulesEngine.GetRowIndex(i) == activeRow && canFlipCurrentCard)
                {
                    // Add an invisible button over the card to flip it on click!
                    var originalCursorPos = ImGui.GetCursorPos();
                    ImGui.SetCursorScreenPos(drawPos);
                    if (ImGui.InvisibleButton($"##pyramid_flip_btn_{i}", new Vector2(w, h)))
                    {
                        plugin.GameController.HandleFlipPyramidCard();
                    }
                    ImGui.SetCursorPos(originalCursorPos);

                    var drawList = ImGui.GetWindowDrawList();
                    float time = (float)ImGui.GetTime();

                    // Make three times as many particles (15 particles)
                    for (int s = 0; s < 15; s++)
                    {
                        // Generate deterministic pseudo-random starting positions on the card
                        float randX = 0.05f + 0.90f * GetPseudoRandom(s, 12.9898f);
                        float randY = 0.05f + 0.90f * GetPseudoRandom(s, 78.233f);

                        // Generate deterministic pseudo-random drift parameters
                        float speedX = 2.0f + GetPseudoRandom(s, 19.34f) * 2.0f;
                        float speedY = 2.0f + GetPseudoRandom(s, 47.85f) * 2.0f;
                        float amplitudeX = 8f + GetPseudoRandom(s, 31.45f) * 12f;
                        float amplitudeY = 8f + GetPseudoRandom(s, 56.12f) * 12f;

                        // Slow smooth drift over time
                        float driftX = MathF.Sin(time * speedX + s * 1.7f) * amplitudeX;
                        float driftY = MathF.Cos(time * speedY + s * 2.3f) * amplitudeY;

                        var spotPos = drawPos + new Vector2(randX * w, randY * h) + new Vector2(driftX, driftY) * rowScale;

                        // Particle size (14f - 24f base size)
                        float sizeOsc = 19f + MathF.Sin(time * 6f + s * 2f) * 5f;
                        float opacity = 0.7f + MathF.Sin(time * 6f + s * 1.5f) * 0.3f;

                        // Rainbow of colors: each sparkle has a different offset and shifts over time
                        uint color = GetRainbowColor(time, s * 0.2f, opacity);

                        HandWindow.DrawSparkle(drawList, spotPos, sizeOsc * rowScale, time * 3f + s, color);
                    }
                }

                // Render all matched stacked cards perfectly centered but slightly rotated
                var matchedCards = plugin.GameState.PyramidMatchedCardsLists[i];
                var matchedRotations = plugin.GameState.PyramidMatchedCardsRotationsLists[i];
                for (int k = 0; k < matchedCards.Count; k++)
                {
                    var mCard = matchedCards[k];
                    var matchedWrap = GetCardTexture(mCard).GetWrapOrEmpty();
                    if (matchedWrap != null)
                    {
                        float rotation = k < matchedRotations.Count ? matchedRotations[k] : 0f;
                        Vector2 centerPos = drawPos + new Vector2(w * 0.5f, h * 0.5f);
                        var drawList = ImGui.GetWindowDrawList();
                        UiCardRenderer.DrawRotatedCard(drawList, matchedWrap.Handle, centerPos, new Vector2(w, h), rowScale, rotation);
                    }
                }
            }
        }

        public override void PostDraw()
        {
            ImGui.PopStyleVar();
        }
    }
}

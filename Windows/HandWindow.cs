using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class HandWindow : Window
    {
        private readonly Plugin plugin;
        private readonly Dictionary<int, Vector2> slotPositions = new();
        private readonly Random random = new();
        private const float HandScaleSaveDebounceSeconds = 0.35f;

        private bool shouldResetPosition = false;
        private Vector2? dragTargetPosition = null;
        private Vector2 lastSetWindowSize = Vector2.Zero;
        private float hoverTime = 0f;
        private bool isFirstDraw = true;
        private float lastWindowWidth = 0f;
        private bool isPositionCustom = false;
        private int pushedStyleVarCount = 0;
        private bool hasPendingHandScaleSave;
        private float pendingHandScaleSaveTimer;

        public HandWindow(Plugin plugin) : base("Hand###FaeLightCardsHandWindow")
        {
            this.plugin = plugin;
            this.IsOpen = true; // Visible by default
            this.RespectCloseHotkey = false;

            plugin.EventBus.BusRideCardDealt += TriggerBusRideDeal;
            plugin.EventBus.CardDealt += TriggerDealAnimation;
            plugin.EventBus.BusRideSlideDown += TriggerBusRideSlideDown;
            plugin.EventBus.LocalCardMatched += HandleLocalCardMatched;
            plugin.EventBus.ScionCardMatched += HandleScionCardMatched;

            // Set size constraints with a minimum size of 50px
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(50, 50),
                MaximumSize = new Vector2(3000, 2000)
            };

            this.Position = new Vector2(300, 400); // Default to a lower position than the deck
            this.PositionCondition = ImGuiCond.FirstUseEver;
        }

        public void TriggerDealAnimation(Card card, int slotIndex)
        {
            plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
            plugin.AnimationManager.TriggerDealAnimation(card, slotIndex, plugin.Configuration.AnimationType);
        }

        public bool HasActiveAnimations => plugin.AnimationManager.ActiveAnimations.Count > 0 || plugin.AnimationManager.ActiveDiscardAnimations.Count > 0 || plugin.AnimationManager.ActiveExitAnimations.Count > 0 || plugin.AnimationManager.PendingDiscardsQueue.Count > 0;
        public bool HasActiveDiscardAnimations => plugin.AnimationManager.ActiveDiscardAnimations.Count > 0;

        public int HandCount => plugin.GameState.ActivePhase == GamePhase.BusRide
            ? (plugin.GameState.BusRideCurrentCard != null ? 1 : 0)
            : plugin.GameState.DisplayedHand.Count;

        public Vector2 LastSetWindowSize => lastSetWindowSize;
        public Vector2 LastSetWindowPosition { get; private set; }

        public void ResetPosition()
        {
            shouldResetPosition = true;
            isPositionCustom = false;
            lastSetWindowSize = Vector2.Zero;
            lastWindowWidth = 0f;
            ClearAnimationsAndParticles();
        }

        public override void PreDraw()
        {
            if (dragTargetPosition.HasValue)
            {
                this.Position = dragTargetPosition.Value;
                this.PositionCondition = ImGuiCond.Always;
                dragTargetPosition = null;
            }
            else if (isFirstDraw || shouldResetPosition || !isPositionCustom)
            {
                var viewport = ImGui.GetMainViewport();

                // Calculate size exactly as in Draw()
                float scale = plugin.Configuration.HandScale;
                var baseCardSize = GetHandBaseCardSize();
                float baseCardW = baseCardSize.X;
                float baseCardH = baseCardSize.Y;
                float cardWidth = baseCardW * scale;
                float cardHeight = baseCardH * scale;
                float padX = 15f * scale;
                float padY = 15f * scale;
                float spacing = 10f * scale;
                int handCount = HandCount;

                int cols = Math.Min(handCount, 6);
                if (cols == 0) cols = 1;
                int rows = (handCount + 5) / 6;
                if (rows == 0) rows = 1;

                float windowW = (cardWidth * cols) + (spacing * Math.Max(0, cols - 1)) + (padX * 2);
                float windowH = (cardHeight * rows) + (spacing * Math.Max(0, rows - 1)) + (padY * 2);

                float defaultY = viewport.Pos.Y + (viewport.Size.Y - windowH) / 2f;
                float currentY = this.Position?.Y ?? defaultY;
                this.Position = new Vector2(
                    UiLayout.GetCenteredX(windowW),
                    (isFirstDraw || shouldResetPosition) ? defaultY : currentY
                );
                this.PositionCondition = ImGuiCond.Always;

                if (shouldResetPosition)
                {
                    shouldResetPosition = false;
                }
                isFirstDraw = false;
            }
            else
            {
                this.PositionCondition = ImGuiCond.None;
            }

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoScrollbar
                                   | ImGuiWindowFlags.NoScrollWithMouse
                                   | ImGuiWindowFlags.NoTitleBar
                                   | ImGuiWindowFlags.NoBackground
                                   | ImGuiWindowFlags.NoResize;

            // Block movement when locked
            if (plugin.Configuration.IsLocked)
            {
                flags |= ImGuiWindowFlags.NoMove;
            }

            this.Flags = flags;

            float stylePadX = 15f * plugin.Configuration.HandScale;
            float stylePadY = 15f * plugin.Configuration.HandScale;
            pushedStyleVarCount = 0;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(stylePadX, stylePadY));
            pushedStyleVarCount++;
        }

        public override void Draw()
        {
            slotPositions.Clear();
            UpdatePendingHandScaleSave(ImGui.GetIO().DeltaTime);
            float scale = plugin.Configuration.HandScale;

            // Base dimensions - 3x larger than the previous 2x default (so 6x of texture size)
            var baseCardSize = GetHandBaseCardSize();
            float baseCardW = baseCardSize.X;
            float baseCardH = baseCardSize.Y;

            float cardWidth = baseCardW * scale;
            float cardHeight = baseCardH * scale;

            float rawPadX = 15f;
            float rawPadY = 15f;
            float rawSpacing = 10f;



            float padX = rawPadX * scale;
            float padY = rawPadY * scale;
            float spacing = rawSpacing * scale;

            int handCount = HandCount;

            // 1. Smart Resize Detection
            var currentWindowSize = ImGui.GetWindowSize();
            bool userResized = false;

            if (!plugin.Configuration.IsLocked && lastSetWindowSize != Vector2.Zero)
            {
                float diffX = Math.Abs(currentWindowSize.X - lastSetWindowSize.X);
                float diffY = Math.Abs(currentWindowSize.Y - lastSetWindowSize.Y);
                if (diffX > 2f || diffY > 2f)
                {
                    userResized = true;
                }
            }

            if (userResized && handCount > 0)
            {
                // Available size for card content
                float availW = currentWindowSize.X - (rawPadX * 2);
                float availH = currentWindowSize.Y - (rawPadY * 2) - (plugin.Configuration.IsLocked ? 0f : 29f);

                // Calculate scale to fit hand horizontally
                float denomW = (handCount * baseCardW) + (Math.Max(0, handCount - 1) * rawSpacing);
                float scaleW = denomW > 0 ? (availW / denomW) : 1f;

                // Calculate scale to fit hand vertically
                float scaleH = availH / baseCardH;

                // Use the smaller scale to maintain card aspect ratio
                float dynamicScale = Math.Min(scaleW, scaleH);
                dynamicScale = Math.Clamp(dynamicScale, 0.10f, 3.0f);

                if (Math.Abs(plugin.Configuration.HandScale - dynamicScale) > 0.001f)
                {
                    plugin.Configuration.HandScale = dynamicScale;
                    ScheduleHandScaleSave();

                    scale = dynamicScale;
                }

                lastSetWindowSize = currentWindowSize;
                LastSetWindowPosition = ImGui.GetWindowPos();
                lastWindowWidth = currentWindowSize.X;
            }
            else
            {
                int cols = Math.Min(handCount, 6);
                if (cols == 0) cols = 1;
                int rows = (handCount + 5) / 6;
                if (rows == 0) rows = 1;

                float windowW = (cardWidth * cols) + (spacing * Math.Max(0, cols - 1)) + (padX * 2);
                float windowH = (cardHeight * rows) + (spacing * Math.Max(0, rows - 1)) + (padY * 2);

                // Enforce minimum size
                windowW = Math.Max(50f, windowW);
                windowH = Math.Max(50f, windowH);

                // If the width changed after a custom drag, adjust position to preserve that custom center.
                if (isPositionCustom && lastWindowWidth > 0f && Math.Abs(lastWindowWidth - windowW) > 0.01f)
                {
                    float shiftX = (lastWindowWidth - windowW) / 2f;
                    dragTargetPosition = ImGui.GetWindowPos() + new Vector2(shiftX, 0f);
                }

                ImGui.SetWindowSize(new Vector2(windowW, windowH));
                lastSetWindowSize = new Vector2(windowW, windowH);
                LastSetWindowPosition = ImGui.GetWindowPos();
                lastWindowWidth = windowW;
            }

            if (plugin.Configuration.ShowDebugBounds)
            {
                var wp = ImGui.GetWindowPos();
                UiDebugDraw.DrawBounds(wp, wp + ImGui.GetWindowSize());
                ImGui.GetForegroundDrawList().AddText(new Vector2(100f, 100f), 0xFFFFFFFF, $"Deck IsOpen: {plugin.DeckWindow.IsOpen}, Deck ActualPos: {plugin.DeckWindow.ActualPosition}");
            }

            // 2. Handle window dragging when unlocked
            if (!plugin.Configuration.IsLocked)
            {
                if (ImGui.IsWindowHovered())
                {
                    hoverTime += ImGui.GetIO().DeltaTime;
                    if (hoverTime >= plugin.Configuration.TooltipDelay)
                    {
                        ImGui.SetTooltip("Drag to move the hand.\nLock position in settings (/faecards).");
                    }

                    if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    {
                        var delta = ImGui.GetIO().MouseDelta;
                        dragTargetPosition = ImGui.GetWindowPos() + delta;
                        isPositionCustom = true;
                        plugin.UiState.LastMovedElementName = "Hand";
                        plugin.UiState.LastMovedElementCoords = dragTargetPosition.Value;
                    }
                }
                else
                {
                    hoverTime = 0f;
                }
            }

            bool isLocalDealer = plugin.IsLocalDealer;

            bool shouldDrawHand = plugin.GameState.ActivePhase == GamePhase.Accumulation ||
                                 (plugin.GameState.ActivePhase == GamePhase.Pyramid && !isLocalDealer) ||
                                 plugin.GameState.ActivePhase == GamePhase.BusRide;

            if (shouldDrawHand)
            {
                if (plugin.TurnManager.HandTransitionTimer > 0f)
                {
                    float progress = Math.Clamp(1.0f - (plugin.TurnManager.HandTransitionTimer / plugin.TurnManager.HandTransitionDuration), 0f, 1f);
                    float easeProgress = progress * progress * (3f - 2f * progress); // smoothstep

                    // 1. Previous hand (fade out, slide down)
                    float prevOpacity = 1.0f - easeProgress;
                    float prevOffset = easeProgress * 150f * scale;
                    DrawHandLayout(plugin.TurnManager.TransitionPrevHand, prevOffset, prevOpacity);

                    // 2. Next hand (fade in, slide down from above)
                    float nextOpacity = easeProgress;
                    float nextOffset = (1.0f - easeProgress) * -150f * scale;
                    DrawHandLayout(plugin.TurnManager.TransitionNextHand, nextOffset, nextOpacity);
                }
                else
                {
                    DrawHandLayout(plugin.GameState.DisplayedHand, 0f, 1.0f, trackSlotPositions: true);
                }
            }

            // 5. Update and Draw Active Animations
            UpdateAndDrawActiveAnimations(scale);
            UpdateAndDrawParticles();
        }

        private void ScheduleHandScaleSave()
        {
            hasPendingHandScaleSave = true;
            pendingHandScaleSaveTimer = HandScaleSaveDebounceSeconds;
        }

        private void UpdatePendingHandScaleSave(float dt)
        {
            if (!hasPendingHandScaleSave)
            {
                return;
            }

            pendingHandScaleSaveTimer -= dt;
            if (pendingHandScaleSaveTimer <= 0f)
            {
                FlushPendingHandScaleSave();
            }
        }

        public void FlushPendingHandScaleSave()
        {
            if (!hasPendingHandScaleSave)
            {
                return;
            }

            hasPendingHandScaleSave = false;
            plugin.Configuration.Save();
        }

        private void DrawHandLayout(IReadOnlyList<Card> hand, float startYOffset, float opacity, bool trackSlotPositions = false)
        {
            int handCount = hand.Count;
            if (plugin.GameState.ActivePhase == GamePhase.BusRide)
            {
                handCount = plugin.GameState.BusRideCurrentCard != null ? 1 : 0;
            }
            float scale = plugin.Configuration.HandScale;
            var baseCardSize = GetHandBaseCardSize();
            float baseCardW = baseCardSize.X;
            float baseCardH = baseCardSize.Y;
            float cardWidth = baseCardW * scale;
            float cardHeight = baseCardH * scale;
            float rawPadX = 15f;
            float rawPadY = 15f;
            float rawSpacing = 10f;
            float padX = rawPadX * scale;
            float padY = rawPadY * scale;
            float spacing = rawSpacing * scale;

            int cols = Math.Min(handCount, 6);
            if (cols == 0) cols = 1;
            int rows = (handCount + 5) / 6;
            if (rows == 0) rows = 1;

            float totalContentH = (cardHeight * rows) + (spacing * Math.Max(0, rows - 1));
            float totalStartY = ((lastSetWindowSize.Y - totalContentH) / 2f) + startYOffset;

            if (handCount > 0)
            {
                for (int v = 0; v < handCount; v++)
                {
                    int rowIndex = v / 6;
                    int colIndex = v % 6;
                    int cardsInRow = Math.Min(6, handCount - rowIndex * 6);

                    float rowContentW = (cardWidth * cardsInRow) + (spacing * Math.Max(0, cardsInRow - 1));
                    float startX = (lastWindowWidth - rowContentW) / 2f;
                    float startY = totalStartY + rowIndex * (cardHeight + spacing);

                    float finalSlotX = startX + colIndex * (cardWidth + spacing);

                    int handIndex = v;
                    Card? card = plugin.GameState.ActivePhase == GamePhase.BusRide
                        ? plugin.GameState.BusRideCurrentCard
                        : hand[handIndex];

                    ImGui.SetCursorPos(new Vector2(finalSlotX, startY));
                    var startPos = ImGui.GetCursorScreenPos();
                    if (trackSlotPositions)
                    {
                        slotPositions[handIndex] = startPos;
                    }
                    ImGui.Dummy(new Vector2(cardWidth, cardHeight));

                    bool isAnimating = plugin.AnimationManager.ActiveAnimations.Any(a => a.SlotIndex == handIndex);
                    if (!isAnimating && card != null)
                    {
                        DrawCard(card, startPos, scale, isFaceDown: false, opacity: opacity);
                    }
                }
            }
            else if (plugin.TurnManager.HandTransitionTimer <= 0f && plugin.GameState.ActiveMode != GameMode.Undecided)
            {
                // Draw dotted placeholder card
                var startPos = ImGui.GetCursorScreenPos();
                ImGui.Dummy(new Vector2(cardWidth, cardHeight));

                var color = plugin.Configuration.IsLocked
                    ? new Vector4(0.5f, 0.5f, 0.5f, 0.4f * opacity)
                    : new Vector4(0f, 0.8f, 1f, 0.8f * opacity); // Cyan when unlocked

                plugin.UiState.HandSize = new Vector2(cardWidth, cardHeight);
                plugin.UiState.HandPosition = ImGui.GetWindowPos();
                plugin.UiState.IsHandVisible = true;
                DrawDashedRect(
                    startPos,
                    startPos + new Vector2(cardWidth, cardHeight),
                    ImGui.ColorConvertFloat4ToU32(color),
                    Math.Max(1.5f, 2f * scale),
                    Math.Max(4f, 8f * scale),
                    Math.Max(3f, 6f * scale));
            }

            if (plugin.GameState.ActivePhase == GamePhase.BusRide)
            {
                string progressText = $"{plugin.GameState.BusRideCorrectStreak}/{plugin.Configuration.BusSize}";
                float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
                float gap = 24f * globalScale;

                Vector2 textSize;
                using (plugin.LargeFont.Push())
                {
                    textSize = ImGui.CalcTextSize(progressText);
                }

                var winPos = ImGui.GetWindowPos();
                var winSize = ImGui.GetWindowSize();

                float textX = winPos.X - gap - textSize.X;
                float textY = winPos.Y + (winSize.Y - textSize.Y) * 0.5f;

                var drawList = ImGui.GetForegroundDrawList();
                float outlineThickness = MathF.Max(1.0f, MathF.Round(3.5f * globalScale));
                Vector2[] outlineOffsets = new Vector2[]
                {
                    new Vector2(-outlineThickness, -outlineThickness),
                    new Vector2(0f, -outlineThickness),
                    new Vector2(outlineThickness, -outlineThickness),
                    new Vector2(-outlineThickness, 0f),
                    new Vector2(outlineThickness, 0f),
                    new Vector2(-outlineThickness, outlineThickness),
                    new Vector2(0f, outlineThickness),
                    new Vector2(outlineThickness, outlineThickness)
                };

                uint outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.8f));
                uint textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.98f, 0.75f, 0.14f, 1.0f));

                using (plugin.LargeFont.Push())
                {
                    foreach (var offset in outlineOffsets)
                    {
                        drawList.AddText(new Vector2(textX, textY) + offset, outlineColor, progressText);
                    }
                    drawList.AddText(new Vector2(textX, textY), textColor, progressText);
                }
            }

            bool shouldDrawRightSideMessage =
                plugin.GameState.ActivePhase == GamePhase.BusRide
                || (plugin.GameState.ActivePhase == GamePhase.Accumulation
                    && (plugin.GameState.ActiveMode == GameMode.Dealer
                        || (plugin.GameState.ActiveMode == GameMode.Player && plugin.TurnManager.PlayerNpcTurnsPending)));

            if (shouldDrawRightSideMessage && plugin.GameState.BusRiderName != GameConstants.LocalPlayerName && !string.IsNullOrEmpty(plugin.UiState.CurrentRightSideMessage))
            {
                string msg = plugin.UiState.CurrentRightSideMessage;
                float animScale = plugin.UiState.RightSideMessageScale;
                float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
                float gap = 24f * globalScale;

                Vector2 textSize;
                using (plugin.LargeFont.Push())
                {
                    ImGui.SetWindowFontScale(animScale);
                    textSize = ImGui.CalcTextSize(msg);
                    ImGui.SetWindowFontScale(1.0f);
                }

                var winPos = ImGui.GetWindowPos();
                var winSize = ImGui.GetWindowSize();

                float textX = winPos.X + winSize.X + gap;
                float textY = winPos.Y + (winSize.Y - textSize.Y) * 0.5f;

                if (animScale < 1.0f)
                {
                    Vector2 targetTextSize;
                    using (plugin.LargeFont.Push())
                    {
                        ImGui.SetWindowFontScale(1.0f);
                        targetTextSize = ImGui.CalcTextSize(msg);
                    }
                    float shiftX = (targetTextSize.X - textSize.X) * 0.5f;
                    float shiftY = (targetTextSize.Y - textSize.Y) * 0.5f;
                    textX += shiftX;
                    textY += shiftY;
                }

                var drawList = ImGui.GetForegroundDrawList();
                float outlineThickness = MathF.Max(1.0f, MathF.Round(3.5f * animScale * globalScale));
                Vector2[] outlineOffsets = new Vector2[]
                {
                    new Vector2(-outlineThickness, -outlineThickness),
                    new Vector2(0f, -outlineThickness),
                    new Vector2(outlineThickness, -outlineThickness),
                    new Vector2(-outlineThickness, 0f),
                    new Vector2(outlineThickness, 0f),
                    new Vector2(-outlineThickness, outlineThickness),
                    new Vector2(0f, outlineThickness),
                    new Vector2(outlineThickness, outlineThickness)
                };

                float rightSideMessageOpacity = 1.0f;
                if (plugin.GameState.ActiveMode == GameMode.Dealer && plugin.GameState.ActivePhase == GamePhase.Accumulation)
                {
                    if (plugin.TurnManager.DealerTransitionTimer > 0f)
                    {
                        rightSideMessageOpacity = 0.0f;
                    }
                }

                if (rightSideMessageOpacity > 0f)
                {
                    uint outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.8f * rightSideMessageOpacity));
                    uint textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f * rightSideMessageOpacity));

                    using (plugin.LargeFont.Push())
                    {
                        ImGui.SetWindowFontScale(animScale);
                        if (outlineThickness > 0.1f)
                        {
                            foreach (var offset in outlineOffsets)
                            {
                                drawList.AddText(new Vector2(textX, textY) + offset, outlineColor, msg);
                            }
                        }
                        drawList.AddText(new Vector2(textX, textY), textColor, msg);
                        ImGui.SetWindowFontScale(1.0f);
                    }
                }
            }

        }

        public override void PostDraw()
        {
            if (pushedStyleVarCount > 0)
            {
                ImGui.PopStyleVar(pushedStyleVarCount);
                pushedStyleVarCount = 0;
            }
        }

    }
}

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
        private const float HandRawPaddingX = 15f;
        private const float HandRawPaddingY = 15f;
        private const float HandRawSpacing = 10f;
        private const int HandMaxCardsPerRow = 6;
        private const float HandMinimumWindowSize = 50f;
        private const float UnlockedResizeHandleHeight = 29f;

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

        private readonly record struct HandLayoutMetrics(
            float Scale,
            float BaseCardWidth,
            float BaseCardHeight,
            float CardWidth,
            float CardHeight,
            float Spacing,
            int HandCount,
            int Rows,
            float WindowWidth,
            float WindowHeight)
        {
            public Vector2 CardSize => new(CardWidth, CardHeight);
            public Vector2 WindowSize => new(WindowWidth, WindowHeight);
        }

        public void ResetPosition()
        {
            shouldResetPosition = true;
            isPositionCustom = false;
            lastSetWindowSize = Vector2.Zero;
            lastWindowWidth = 0f;
            ClearAnimationsAndParticles();
        }

        private HandLayoutMetrics CreateCurrentHandLayoutMetrics(bool enforceMinimumWindowSize = false)
        {
            return CreateHandLayoutMetrics(HandCount, null, enforceMinimumWindowSize);
        }

        private HandLayoutMetrics CreateHandLayoutMetrics(int handCount, float? scaleOverride = null, bool enforceMinimumWindowSize = false)
        {
            float scale = scaleOverride ?? plugin.Configuration.HandScale;
            var baseCardSize = GetHandBaseCardSize();
            float cardWidth = baseCardSize.X * scale;
            float cardHeight = baseCardSize.Y * scale;
            float paddingX = HandRawPaddingX * scale;
            float paddingY = HandRawPaddingY * scale;
            float spacing = HandRawSpacing * scale;
            int columns = Math.Min(handCount, HandMaxCardsPerRow);
            if (columns == 0) columns = 1;

            int rows = (handCount + HandMaxCardsPerRow - 1) / HandMaxCardsPerRow;
            if (rows == 0) rows = 1;

            float windowWidth = (cardWidth * columns) + (spacing * Math.Max(0, columns - 1)) + (paddingX * 2);
            float windowHeight = (cardHeight * rows) + (spacing * Math.Max(0, rows - 1)) + (paddingY * 2);

            if (enforceMinimumWindowSize)
            {
                windowWidth = Math.Max(HandMinimumWindowSize, windowWidth);
                windowHeight = Math.Max(HandMinimumWindowSize, windowHeight);
            }

            return new HandLayoutMetrics(
                scale,
                baseCardSize.X,
                baseCardSize.Y,
                cardWidth,
                cardHeight,
                spacing,
                handCount,
                rows,
                windowWidth,
                windowHeight);
        }

        private int GetVisibleHandCount(IReadOnlyList<Card> hand)
        {
            return plugin.GameState.ActivePhase == GamePhase.BusRide
                ? (plugin.GameState.BusRideCurrentCard != null ? 1 : 0)
                : hand.Count;
        }

        private static Vector2 GetHandWindowPadding(float scale)
        {
            return new Vector2(HandRawPaddingX * scale, HandRawPaddingY * scale);
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
                var metrics = CreateCurrentHandLayoutMetrics();

                float defaultY = viewport.Pos.Y + (viewport.Size.Y - metrics.WindowHeight) / 2f;
                float currentY = this.Position?.Y ?? defaultY;
                this.Position = new Vector2(
                    UiLayout.GetCenteredX(metrics.WindowWidth),
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

            pushedStyleVarCount = 0;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, GetHandWindowPadding(plugin.Configuration.HandScale));
            pushedStyleVarCount++;
        }

        public override void Draw()
        {
            slotPositions.Clear();
            UpdatePendingHandScaleSave(ImGui.GetIO().DeltaTime);
            var metrics = CreateCurrentHandLayoutMetrics(enforceMinimumWindowSize: true);
            float scale = metrics.Scale;
            int handCount = metrics.HandCount;

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
                float availW = currentWindowSize.X - (HandRawPaddingX * 2);
                float availH = currentWindowSize.Y - (HandRawPaddingY * 2) - (plugin.Configuration.IsLocked ? 0f : UnlockedResizeHandleHeight);

                // Calculate scale to fit hand horizontally
                float denomW = (handCount * metrics.BaseCardWidth) + (Math.Max(0, handCount - 1) * HandRawSpacing);
                float scaleW = denomW > 0 ? (availW / denomW) : 1f;

                // Calculate scale to fit hand vertically
                float scaleH = availH / metrics.BaseCardHeight;

                // Use the smaller scale to maintain card aspect ratio
                float dynamicScale = Math.Min(scaleW, scaleH);
                dynamicScale = Math.Clamp(dynamicScale, 0.10f, 3.0f);

                if (Math.Abs(plugin.Configuration.HandScale - dynamicScale) > 0.001f)
                {
                    plugin.Configuration.HandScale = dynamicScale;
                    ScheduleHandScaleSave();

                    metrics = CreateHandLayoutMetrics(handCount, dynamicScale, enforceMinimumWindowSize: true);
                    scale = dynamicScale;
                }

                lastSetWindowSize = currentWindowSize;
                LastSetWindowPosition = ImGui.GetWindowPos();
                lastWindowWidth = currentWindowSize.X;
            }
            else
            {
                // If the width changed after a custom drag, adjust position to preserve that custom center.
                if (isPositionCustom && lastWindowWidth > 0f && Math.Abs(lastWindowWidth - metrics.WindowWidth) > 0.01f)
                {
                    float shiftX = (lastWindowWidth - metrics.WindowWidth) / 2f;
                    dragTargetPosition = ImGui.GetWindowPos() + new Vector2(shiftX, 0f);
                }

                ImGui.SetWindowSize(metrics.WindowSize);
                lastSetWindowSize = metrics.WindowSize;
                LastSetWindowPosition = ImGui.GetWindowPos();
                lastWindowWidth = metrics.WindowWidth;
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
            var metrics = CreateHandLayoutMetrics(GetVisibleHandCount(hand));
            int handCount = metrics.HandCount;
            float scale = metrics.Scale;
            float cardWidth = metrics.CardWidth;
            float cardHeight = metrics.CardHeight;
            float spacing = metrics.Spacing;

            float totalContentH = (cardHeight * metrics.Rows) + (spacing * Math.Max(0, metrics.Rows - 1));
            float totalStartY = ((lastSetWindowSize.Y - totalContentH) / 2f) + startYOffset;

            if (handCount > 0)
            {
                for (int v = 0; v < handCount; v++)
                {
                    int rowIndex = v / HandMaxCardsPerRow;
                    int colIndex = v % HandMaxCardsPerRow;
                    int cardsInRow = Math.Min(HandMaxCardsPerRow, handCount - rowIndex * HandMaxCardsPerRow);

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
                ImGui.Dummy(metrics.CardSize);

                var color = plugin.Configuration.IsLocked
                    ? new Vector4(0.5f, 0.5f, 0.5f, 0.4f * opacity)
                    : new Vector4(0f, 0.8f, 1f, 0.8f * opacity); // Cyan when unlocked

                plugin.UiState.HandSize = metrics.CardSize;
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

                float outlineThickness = MathF.Max(1.0f, MathF.Round(3.5f * globalScale));

                using (plugin.LargeFont.Push())
                {
                    UiLayout.DrawOutlinedText(
                        ImGui.GetForegroundDrawList(),
                        new Vector2(textX, textY),
                        progressText,
                        UITheme.GoldText,
                        new Vector4(0f, 0f, 0f, 0.8f),
                        outlineThickness);
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

                float outlineThickness = MathF.Max(1.0f, MathF.Round(3.5f * animScale * globalScale));

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
                    using (plugin.LargeFont.Push())
                    {
                        ImGui.SetWindowFontScale(animScale);
                        UiLayout.DrawOutlinedText(
                            ImGui.GetForegroundDrawList(),
                            new Vector2(textX, textY),
                            msg,
                            new Vector4(1f, 1f, 1f, rightSideMessageOpacity),
                            new Vector4(0f, 0f, 0f, 0.8f * rightSideMessageOpacity),
                            outlineThickness);
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

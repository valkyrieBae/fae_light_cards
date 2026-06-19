using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public class OverlayWindow : Window
    {
        private readonly Plugin plugin;

        public OverlayWindow(Plugin plugin) : base("Overlay###FaeLightCardsOverlayWindow",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMouseInputs)
        {
            this.plugin = plugin;
            this.RespectCloseHotkey = false;
            this.IsOpen = true;
            this.Size = new Vector2(ImGui.GetIO().DisplaySize.X, ImGui.GetIO().DisplaySize.Y);
            this.Position = Vector2.Zero;
        }

        public override void Draw()
        {
            float dt = ImGui.GetIO().DeltaTime;

            if (plugin.UiState.SecondaryMessage == null || plugin.UiState.SecondaryMessageAnimTime < 0f)
            {
                plugin.GameCoordinator.TryShowNextSecondaryMessage();
            }

            if (plugin.UiState.SecondaryMessage != null && plugin.UiState.SecondaryMessageAnimTime >= 0f)
            {
                plugin.UiState.SecondaryMessageAnimTime += dt;
                if (plugin.UiState.SecondaryMessageAnimTime >= UIConstants.SecondaryMessageDuration)
                {
                    plugin.UiState.SecondaryMessage = null;
                    plugin.UiState.SecondaryMessageAnimTime = -1f;
                    plugin.GameCoordinator.TryShowNextSecondaryMessage();
                    if (plugin.UiState.SecondaryMessage != null)
                    {
                        DrawSecondaryMessage();
                    }
                }
                else
                {
                    DrawSecondaryMessage();
                }
            }

            UpdateAndDrawWinLoseOverlay(dt);
            DrawInstructionBanner();
        }

        private void DrawSecondaryMessage()
        {
            if (plugin.UiState.SecondaryMessage == null) return;

            var viewport = ImGui.GetMainViewport();
            var handPos = plugin.HandWindow.LastSetWindowPosition != Vector2.Zero
                ? plugin.HandWindow.LastSetWindowPosition
                : new Vector2(viewport.Pos.X + 50f, viewport.Pos.Y + viewport.Size.Y - 200f);
            var handSize = plugin.HandWindow.LastSetWindowSize != Vector2.Zero
                ? plugin.HandWindow.LastSetWindowSize
                : new Vector2(300f, 150f);

            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            float gap = 24f * globalScale;

            float handBottom = handPos.Y + handSize.Y;
            float handCenterX = handPos.X + handSize.X * 0.5f;

            float elapsed = plugin.UiState.SecondaryMessageAnimTime;
            float scale = 1.0f;
            float alpha = 1.0f;

            if (elapsed < UIConstants.SecondaryMessagePopDuration)
            {
                float t = elapsed / UIConstants.SecondaryMessagePopDuration;
                scale = MathF.Sin(t * MathF.PI * 0.5f) * 1.15f;
            }
            else if (elapsed < UIConstants.SecondaryMessagePopDuration + UIConstants.SecondaryMessageSettleDuration)
            {
                float t = (elapsed - UIConstants.SecondaryMessagePopDuration) / UIConstants.SecondaryMessageSettleDuration;
                scale = 1.15f - t * 0.15f;
            }
            else if (elapsed > UIConstants.SecondaryMessageMinVisibleDuration)
            {
                float t = (elapsed - UIConstants.SecondaryMessageMinVisibleDuration) / UIConstants.SecondaryMessageFadeDuration;
                scale = 1.0f - t;
                alpha = 1.0f - t;
            }

            scale = Math.Clamp(scale, 0f, 1.2f);
            alpha = Math.Clamp(alpha, 0f, 1f);

            Vector2 targetTextSize;
            using (plugin.LargeFont.Push())
            {
                ImGui.SetWindowFontScale(1.0f);
                targetTextSize = ImGui.CalcTextSize(plugin.UiState.SecondaryMessage);
            }

            Vector2 textSize;
            using (plugin.LargeFont.Push())
            {
                ImGui.SetWindowFontScale(scale);
                textSize = ImGui.CalcTextSize(plugin.UiState.SecondaryMessage);
            }

            float x = handCenterX - textSize.X * 0.5f;
            float centerY = handBottom + gap + targetTextSize.Y * 0.5f;
            float y = centerY - textSize.Y * 0.5f;

            var drawList = ImGui.GetForegroundDrawList();
            float outlineThickness = MathF.Max(1.0f, MathF.Round(3.5f * globalScale * scale));

            using (plugin.LargeFont.Push())
            {
                ImGui.SetWindowFontScale(scale);
                UiLayout.DrawOutlinedText(
                    drawList,
                    new Vector2(x, y),
                    plugin.UiState.SecondaryMessage,
                    new Vector4(1f, 1f, 1f, alpha),
                    new Vector4(0f, 0f, 0f, 0.8f * alpha),
                    outlineThickness);
            }
        }

        private void DrawGoldenBanner(string text)
        {
            Vector2 pos = plugin.PlayersWindow.ActualPosition;
            Vector2 size = plugin.PlayersWindow.ActualSize;

            Vector2 textSize;
            using (plugin.MediumFont.Push())
            {
                textSize = ImGui.CalcTextSize(text);
            }

            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            float paddingX = 24f * globalScale;
            float bannerWidth = MathF.Max(size.X, textSize.X + paddingX);
            float bannerHeight = 42f * globalScale;

            float bannerPosX = (pos.X + size.X * 0.5f) - bannerWidth * 0.5f;
            float bannerPosY = pos.Y + size.Y + 8f * globalScale;
            Vector2 bannerPos = new Vector2(bannerPosX, bannerPosY);

            var drawList = ImGui.GetForegroundDrawList();
            drawList.AddRectFilled(bannerPos, bannerPos + new Vector2(bannerWidth, bannerHeight), ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.1f, 0.95f)), 4f);
            drawList.AddRect(bannerPos, bannerPos + new Vector2(bannerWidth, bannerHeight), ImGui.ColorConvertFloat4ToU32(new Vector4(0.98f, 0.75f, 0.14f, 0.8f)), 4f, ImDrawFlags.None, 1.5f);

            using (plugin.MediumFont.Push())
            {
                ImGui.SetWindowFontScale(0.85f);
                var currentTextSize = ImGui.CalcTextSize(text);

                var textLocalPos = bannerPos + new Vector2((bannerWidth - currentTextSize.X) * 0.5f, (bannerHeight - currentTextSize.Y) * 0.5f);
                textLocalPos.Y -= 2.0f * globalScale;

                float pulse = 0.8f + MathF.Sin((float)ImGui.GetTime() * 8f) * 0.2f;
                float thickness = MathF.Max(1.0f, MathF.Round(1.5f * globalScale));
                UiLayout.DrawOutlinedText(
                    drawList,
                    textLocalPos,
                    text,
                    UITheme.WithOpacity(UITheme.GoldText, pulse),
                    new Vector4(0.0f, 0.0f, 0.0f, pulse * 0.8f),
                    thickness);
                ImGui.SetWindowFontScale(1.0f);
            }
        }

        private void DrawInstructionBanner()
        {
            if (plugin.GameState.ActivePhase == GamePhase.TieChoice)
            {
                var players = plugin.GameState.Players;
                var nonDealers = players.Where(p => !p.IsDealer && p.IsEligibleForCurrentBusRide).ToList();
                int maxCards = nonDealers.Count > 0 ? nonDealers.Max(p => p.Hand.Count) : 0;
                var candidates = nonDealers.Where(p => p.Hand.Count == maxCards).ToList();

                if (candidates.Count > 1)
                {
                    DrawGoldenBanner("Pyramid complete! It's a tie! Choose a rider!");
                }
                else
                {
                    DrawGoldenBanner("Pyramid complete! It's time to ride the bus!");
                }
                return;
            }

            if (plugin.GameState.ActivePhase == GamePhase.Accumulation && plugin.GameState.HasPendingDrinkTarget)
            {
                var localPlayer = plugin.GameState.Players.FirstOrDefault(p => p.IsLocal);
                bool isLocalGiver = localPlayer != null && localPlayer.Name == plugin.GameState.PendingDrinkGiverName;
                int drinks = plugin.GameState.PendingDrinkAmount;
                string drinksText = $"{drinks} {(drinks == 1 ? "drink" : "drinks")}";
                string text = isLocalGiver
                    ? $"Pick who takes {drinksText}!"
                    : $"{plugin.GameState.PendingDrinkGiverName} picks who takes {drinksText}...";
                DrawGoldenBanner(text);
                return;
            }

            int slotIdx = plugin.GameState.PendingLocalMatchSlotIndex;
            if (slotIdx != -1)
            {
                int row = RulesEngine.GetRowIndex(slotIdx);
                int mult = RulesEngine.GetRowMultiplier(row);
                string text = $"Choose a target for {mult} {(mult == 1 ? "drink" : "drinks")}!";
                DrawGoldenBanner(text);
                return;
            }

            bool isLocalDealer = plugin.IsLocalDealer;
            if (plugin.GameState.ActiveMode == GameMode.Dealer && isLocalDealer && plugin.GameState.ActivePhase == GamePhase.Pyramid && plugin.GameState.CurrentFlipIndex > 0)
            {
                int flipIdx = plugin.GameState.CurrentFlipIndex - 1;
                if (flipIdx >= 0 && flipIdx < 15)
                {
                    var required = plugin.GameState.PyramidRequiredMatchers[flipIdx];
                    var matched = plugin.GameState.PyramidMatchedPlayerNamesLists[flipIdx];

                    int waitingCount = required.Where(r => matched.Count(m => m == r) < required.Count(m => m == r)).Distinct().Count();
                    if (waitingCount > 0)
                    {
                        string text = $"{waitingCount} {(waitingCount == 1 ? "player" : "players")} choosing...";
                        DrawGoldenBanner(text);
                        return;
                    }
                }
            }
        }

        private static float GetConveyorOffsetY(float globalScale)
        {
            return 65f * globalScale;
        }

        private static float ClampOrCenter(float value, float min, float max)
        {
            return max < min ? (min + max) * 0.5f : Math.Clamp(value, min, max);
        }

        public Vector2 GetConveyorAnchor()
        {
            var viewport = ImGui.GetMainViewport();
            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            float margin = 16f * globalScale;
            float offsetY = GetConveyorOffsetY(globalScale);
            float maxTextWidth = 1f;
            float maxTextHeight = 1f;

            using (plugin.LargeFont.Push())
            {
                foreach (var msg in plugin.UiState.ActiveOverlayMessages)
                {
                    var size = ImGui.CalcTextSize(msg.Text);
                    maxTextWidth = MathF.Max(maxTextWidth, size.X);
                    maxTextHeight = MathF.Max(maxTextHeight, size.Y);
                }

                foreach (var msg in plugin.UiState.OverlayMessageQueue)
                {
                    var size = ImGui.CalcTextSize(msg.Message);
                    maxTextWidth = MathF.Max(maxTextWidth, size.X);
                    maxTextHeight = MathF.Max(maxTextHeight, size.Y);
                }
            }

            float halfTextWidth = maxTextWidth * 0.5f;
            float halfTextHeight = maxTextHeight * 0.5f;
            float minCenterY = viewport.Pos.Y + offsetY + halfTextHeight + margin;
            float maxCenterY = viewport.Pos.Y + viewport.Size.Y - offsetY - halfTextHeight - margin;

            float centerX = viewport.Pos.X + viewport.Size.X * 0.5f;
            float centerY = viewport.Pos.Y + MathF.Max(96f * globalScale, offsetY + halfTextHeight + margin);

            if (TryGetVisiblePromptRect(out var promptMin, out var promptMax))
            {
                float laneMinX = centerX - halfTextWidth - margin;
                float laneMaxX = centerX + halfTextWidth + margin;
                float laneMinY = centerY - offsetY - halfTextHeight - margin;
                float laneMaxY = centerY + offsetY + halfTextHeight + margin;

                bool overlapsPrompt = laneMinX < promptMax.X
                                      && laneMaxX > promptMin.X
                                      && laneMinY < promptMax.Y
                                      && laneMaxY > promptMin.Y;
                if (overlapsPrompt)
                {
                    float belowCenterY = promptMax.Y + offsetY + halfTextHeight + margin;
                    float belowLaneMaxY = belowCenterY + offsetY + halfTextHeight + margin;
                    float aboveCenterY = promptMin.Y - offsetY - halfTextHeight - margin;
                    float aboveLaneMinY = aboveCenterY - offsetY - halfTextHeight - margin;

                    centerY = belowLaneMaxY <= viewport.Pos.Y + viewport.Size.Y - margin
                        ? belowCenterY
                        : aboveLaneMinY >= viewport.Pos.Y + margin
                            ? aboveCenterY
                            : centerY;
                }
            }

            centerX = ClampOrCenter(centerX, viewport.Pos.X + halfTextWidth + margin, viewport.Pos.X + viewport.Size.X - halfTextWidth - margin);
            centerY = ClampOrCenter(centerY, minCenterY, maxCenterY);

            return new Vector2(centerX, centerY);
        }

        private bool TryGetVisiblePromptRect(out Vector2 min, out Vector2 max)
        {
            min = Vector2.Zero;
            max = Vector2.Zero;

            if (!plugin.PromptWindow.IsOpen || plugin.UiState.PromptScale <= 0.01f)
            {
                return false;
            }

            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            Vector2 promptSize = plugin.PromptWindow.GetExpectedSize() * plugin.UiState.PromptScale * globalScale;
            Vector2 center = plugin.UiState.PromptCenterPos != Vector2.Zero
                ? plugin.UiState.PromptCenterPos
                : plugin.PromptWindow.GetDefaultPosition() + plugin.PromptWindow.GetExpectedSize() * globalScale * 0.5f;

            min = center - promptSize * 0.5f;
            max = center + promptSize * 0.5f;
            return true;
        }

        private (float posY, float scale, float opacity) GetMessageLayout(float v, float centerY, float offsetY)
        {
            float posY, scale, opacity;

            if (v >= 1.0f)
            {
                float t = Math.Clamp(2.0f - v, 0f, 1f);
                t = 1f - (1f - t) * (1f - t);
                posY = (centerY + offsetY) + t * (centerY - (centerY + offsetY));
                scale = 0.7f + t * (1.0f - 0.7f);
                opacity = 0.4f + t * (1.0f - 0.4f);
            }
            else if (v >= 0.0f)
            {
                float t = Math.Clamp(1.0f - v, 0f, 1f);
                t = 1f - (1f - t) * (1f - t);
                posY = centerY + t * ((centerY - offsetY) - centerY);
                scale = 1.0f + t * (0.7f - 1.0f);
                opacity = 1.0f + t * (0.4f - 1.0f);
            }
            else
            {
                float t = Math.Clamp(-v, 0f, 1f);
                t = t * t;
                posY = (centerY - offsetY) + t * ((centerY - 2.0f * offsetY) - (centerY - offsetY));
                scale = 0.7f + t * (0.5f - 0.7f);
                opacity = (1f - t) * 0.4f;
            }

            return (posY, scale, opacity);
        }

        private void UpdateAndDrawWinLoseOverlay(float dt)
        {
            if (plugin.UiState.PendingWinLoseMessage != null)
            {
                if (plugin.UiState.WinLoseDelayTimer < 0f)
                {
                    if (!plugin.HandWindow.HasActiveAnimations)
                    {
                        plugin.UiState.WinLoseDelayTimer = 0.2f;
                    }
                }
                else
                {
                    plugin.UiState.WinLoseDelayTimer -= dt;
                    if (plugin.UiState.WinLoseDelayTimer <= 0f)
                    {
                        plugin.GameCoordinator.QueueConveyorMessage(plugin.UiState.PendingWinLoseMessage);
                        plugin.UiState.PendingWinLoseMessage = null;
                        plugin.UiState.WinLoseDelayTimer = -1f;
                    }
                }
            }

            foreach (var msg in plugin.UiState.ActiveOverlayMessages)
            {
                if (msg.VisualPosition > msg.TargetPosition)
                {
                    msg.VisualPosition = MathF.Max(msg.TargetPosition, msg.VisualPosition - dt / UIConstants.ConveyorSlideDuration);
                }
                else if (msg.VisualPosition < msg.TargetPosition)
                {
                    msg.VisualPosition = MathF.Min(msg.TargetPosition, msg.VisualPosition + dt / UIConstants.ConveyorSlideDuration);
                }
            }

            foreach (var msg in plugin.UiState.ActiveOverlayMessages)
            {
                if (msg.CompletionAction == UIState.OverlayMessageCompletionAction.None
                    || msg.TargetPosition != 1.0f
                    || msg.CompletionActionHandled)
                {
                    continue;
                }

                if (MathF.Abs(msg.VisualPosition - 1.0f) >= 0.01f)
                {
                    msg.CompletionActionDelayTimer = -1f;
                    continue;
                }

                if (msg.CompletionActionDelayTimer < 0f)
                {
                    msg.CompletionActionDelayTimer = UIConstants.ConveyorCenterCompletionDelay;
                }
                else
                {
                    msg.CompletionActionDelayTimer -= dt;
                    if (msg.CompletionActionDelayTimer <= 0f)
                    {
                        plugin.GameCoordinator.OnMessageFinished(msg);
                    }
                }
            }

            plugin.UiState.ActiveOverlayMessages.RemoveAll(m => m.TargetPosition <= -1.0f && m.VisualPosition <= -1.0f);

            while (plugin.UiState.OverlayMessageQueue.Count > 0)
            {
                bool hasSlot1 = plugin.UiState.ActiveOverlayMessages.Any(m => m.TargetPosition == 1.0f);
                bool hasSlot2 = plugin.UiState.ActiveOverlayMessages.Any(m => m.TargetPosition == 2.0f);

                if (!hasSlot1)
                {
                    var nextMsg = plugin.UiState.OverlayMessageQueue.Dequeue();
                    plugin.GameCoordinator.TriggerWinLoseAnimation(nextMsg.Message, nextMsg.ShowFireworks, nextMsg.CompletionAction);
                    plugin.UiState.ConveyorTimer = UIConstants.ConveyorHoldDuration;
                }
                else if (!hasSlot2)
                {
                    var nextMsg = plugin.UiState.OverlayMessageQueue.Dequeue();
                    plugin.GameCoordinator.TriggerWinLoseAnimation(nextMsg.Message, nextMsg.ShowFireworks, nextMsg.CompletionAction);
                }
                else
                {
                    break;
                }
            }

            if (plugin.UiState.ActiveOverlayMessages.Count > 0)
            {
                plugin.UiState.ConveyorTimer -= dt;
                if (plugin.UiState.ConveyorTimer <= 0f)
                {
                    bool allSettled = plugin.UiState.ActiveOverlayMessages.Where(m => m.TargetPosition >= 0.0f).All(m => MathF.Abs(m.VisualPosition - m.TargetPosition) < 0.01f);
                    if (allSettled)
                    {
                        foreach (var msg in plugin.UiState.ActiveOverlayMessages)
                        {
                            float oldTarget = msg.TargetPosition;
                            msg.TargetPosition -= 1.0f;
                            if (oldTarget == 1.0f)
                            {
                                plugin.GameCoordinator.OnMessageFinished(msg);
                            }
                        }
                        plugin.UiState.ConveyorTimer = UIConstants.ConveyorHoldDuration;
                    }
                }
            }

            if (plugin.UiState.ActiveOverlayMessages.Count == 0) return;

            var viewport = ImGui.GetMainViewport();
            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            float margin = 16f * globalScale;
            var center = GetConveyorAnchor();
            float offsetY = GetConveyorOffsetY(globalScale);

            var drawList = ImGui.GetForegroundDrawList();

            using (plugin.LargeFont.Push())
            {
                foreach (var msg in plugin.UiState.ActiveOverlayMessages)
                {
                    var (posY, scale, opacity) = GetMessageLayout(msg.VisualPosition, center.Y, offsetY);

                    ImGui.SetWindowFontScale(scale);
                    var textSize = ImGui.CalcTextSize(msg.Text);
                    float posX = ClampOrCenter(
                        center.X - textSize.X * 0.5f,
                        viewport.Pos.X + margin,
                        viewport.Pos.X + viewport.Size.X - textSize.X - margin);
                    float clampedY = ClampOrCenter(
                        posY - textSize.Y * 0.5f,
                        viewport.Pos.Y + margin,
                        viewport.Pos.Y + viewport.Size.Y - textSize.Y - margin);

                    var screenPos = new Vector2(MathF.Round(posX), MathF.Round(clampedY));
                    float outlineThickness = MathF.Max(1.0f, MathF.Round(3.5f * scale * globalScale));
                    UiLayout.DrawOutlinedText(
                        drawList,
                        screenPos,
                        msg.Text,
                        new Vector4(1f, 1f, 1f, opacity),
                        new Vector4(0f, 0f, 0f, opacity),
                        outlineThickness);
                }
            }
        }
    }
}

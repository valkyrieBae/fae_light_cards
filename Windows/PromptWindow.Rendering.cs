using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class PromptWindow
    {
        private void RenderPromptScreen(PromptRenderModel model, float scale, Vector2 expectedWindowSize)
        {
            switch (model.ScreenKind)
            {
                case PromptScreenKind.StandardChoices:
                    DrawStandardPrompt(model, scale, expectedWindowSize);
                    break;
                case PromptScreenKind.EnterRoomCode:
                    DrawEnterRoomCodeScreen(scale, expectedWindowSize);
                    break;
                case PromptScreenKind.Lobby:
                    DrawLobbyScreen(scale, expectedWindowSize);
                    break;
            }
        }

        private void DrawStandardPrompt(PromptRenderModel model, float scale, Vector2 expectedWindowSize)
        {
            var drawList = ImGui.GetWindowDrawList();
            var textSize = ImGui.CalcTextSize(model.PromptText);
            int numOptions = model.Options.Count;

            var textX = MathF.Round(ImGui.GetWindowPos().X + (expectedWindowSize.X - textSize.X) / 2f);
            var textY = numOptions == 0
                ? MathF.Round(ImGui.GetWindowPos().Y + (expectedWindowSize.Y - textSize.Y) / 2f)
                : MathF.Round(ImGui.GetWindowPos().Y + 5f * scale);
            var textStartPos = new Vector2(textX, textY);

            float opacity = plugin.UiState.PromptScale;
            DrawTextWithOutline(drawList, textStartPos, model.PromptText, ApplyOpacity(0xFFFFFFFF, opacity), ApplyOpacity(0xFF000000, opacity), scale);
            DrawChoiceButtons(model, textSize, numOptions, opacity, scale, expectedWindowSize);
        }

        private void DrawChoiceButtons(PromptRenderModel model, Vector2 textSize, int numOptions, float opacity, float scale, Vector2 expectedWindowSize)
        {
            float buttonW = (numOptions == 4 ? 52f : 130f) * scale;
            float buttonH = 46f * scale;
            float spacing = (numOptions == 4 ? 12f : 16f) * scale;
            float totalW = numOptions * buttonW + (numOptions - 1) * spacing;
            float startX = (expectedWindowSize.X - totalW) / 2f;
            float buttonY = textSize.Y + 15f * scale;

            for (int i = 0; i < numOptions; i++)
            {
                float currentButtonScale = 1.0f;
                if (plugin.UiState.PromptState == UIState.PromptAnimState.ButtonClick && plugin.UiState.ClickedButtonIndex == i)
                {
                    float t = plugin.UiState.PromptAnimTimer / UIConstants.ButtonClickDuration;
                    currentButtonScale = 1.0f - 0.15f * MathF.Sin(t * MathF.PI);
                }

                Vector2 normalSize = new(buttonW, buttonH);
                Vector2 actualSize = normalSize * currentButtonScale;
                float buttonX = startX + i * (buttonW + spacing);
                Vector2 normalPos = new(buttonX, buttonY);

                if (currentButtonScale < 1.0f)
                {
                    Vector2 offset = (normalSize - actualSize) * 0.5f;
                    ImGui.SetCursorPos(normalPos + offset);
                }
                else
                {
                    ImGui.SetCursorPos(normalPos);
                }

                var option = model.Options[i];
                bool clicked = UiLayout.DrawCustomChoiceButton(
                    option.Label,
                    actualSize,
                    option.Theme,
                    opacity,
                    scale * currentButtonScale,
                    SpawnPromptButtonFeedback);

                if (clicked && !model.IsLockedOut)
                {
                    HandleChoiceClick(i);
                }
            }
        }

        private void DrawEnterRoomCodeScreen(float scale, Vector2 windowSize)
        {
            var drawList = ImGui.GetWindowDrawList();
            DrawPromptPanelBackground(drawList, windowSize, scale);

            string title = "Enter Room Code";
            var textSize = ImGui.CalcTextSize(title);
            var textPos = ImGui.GetWindowPos() + new Vector2((windowSize.X - textSize.X) / 2f, 10f * scale);
            DrawTextWithOutline(drawList, textPos, title, ApplyOpacity(0xFFFFFFFF, plugin.UiState.PromptScale), ApplyOpacity(0xFF000000, plugin.UiState.PromptScale), scale);

            ImGui.SetCursorPos(new Vector2((windowSize.X - 120f * scale) / 2f, 40f * scale));
            ImGui.SetNextItemWidth(120f * scale);

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f * scale);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.5f * scale);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, UITheme.InputBackground);
            ImGui.PushStyleColor(ImGuiCol.Border, UITheme.InputBorder);
            ImGui.InputText("##RoomCodeInput", ref roomInput, 4, ImGuiInputTextFlags.CharsUppercase | ImGuiInputTextFlags.CharsNoBlank);
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);

            bool canJoin = roomInput.Trim().Length == 4;
            ImGui.SetCursorPos(new Vector2((windowSize.X - 110f * scale) / 2f - 60f * scale, 90f * scale));
            if (UiLayout.DrawCustomChoiceButton("Join", new Vector2(110f * scale, 36f * scale), UITheme.ForEnabled(canJoin, UITheme.Primary), 1.0f, scale, SpawnPromptButtonFeedback) && canJoin)
            {
                HandleJoinRoom();
            }

            ImGui.SetCursorPos(new Vector2((windowSize.X - 110f * scale) / 2f + 60f * scale, 90f * scale));
            if (UiLayout.DrawCustomChoiceButton("Cancel", new Vector2(110f * scale, 36f * scale), UITheme.Danger, 1.0f, scale, SpawnPromptButtonFeedback))
            {
                HandleCancelNetworkFlow();
            }
        }

        private void DrawLobbyScreen(float scale, Vector2 windowSize)
        {
            var drawList = ImGui.GetWindowDrawList();
            DrawPromptPanelBackground(drawList, windowSize, scale);

            string roomId = plugin.AppState.CurrentRoomId;
            bool isDealer = plugin.AppState.ChosenGameMode == GameMode.Dealer;

            string codeText = $"Room Code: {roomId}";
            var codeTextPos = ImGui.GetWindowPos() + new Vector2(12f * scale, 10f * scale);
            DrawTextWithOutline(drawList, codeTextPos, codeText, ApplyOpacity(0xFFFFFFFF, plugin.UiState.PromptScale), ApplyOpacity(0xFF000000, plugin.UiState.PromptScale), scale);

            ImGui.SetCursorPos(new Vector2(windowSize.X - 95f * scale - 12f * scale, 8f * scale));
            string copyLabel = copiedTimer > 0f ? "Copied!" : "Copy";
            if (UiLayout.DrawCustomChoiceButton(copyLabel, new Vector2(95f * scale, 24f * scale), UITheme.Secondary, 1.0f, scale * 0.8f, SpawnPromptButtonFeedback))
            {
                HandleCopyRoomCode(roomId);
            }

            ImGui.SetCursorPos(new Vector2(12f * scale, 38f * scale));
            ImGui.TextColored(UITheme.SuccessText, "Server: Connected");

            int count = plugin.GameState.Players.Count;
            ImGui.SetCursorPos(new Vector2(12f * scale, 56f * scale));
            ImGui.Text($"Connected Players: {count}/8");

            float startY = 76f * scale;
            ImGui.SetCursorPos(new Vector2(12f * scale, startY));
            ImGui.BeginChild("##LobbyPlayersChild", new Vector2(windowSize.X - 24f * scale, count * 24f * scale + 5f * scale), false, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
            for (int i = 0; i < count; i++)
            {
                var p = plugin.GameState.Players[i];
                string nameStr = p.Name;
                if (p.IsDealer) nameStr += " (Dealer)";
                if (p.IsLocal) nameStr += " [You]";

                ImGui.TextColored(p.IsDealer ? UITheme.GoldText : UITheme.WhiteText, $" - {nameStr}");
            }
            ImGui.EndChild();

            float btnY = startY + count * 24f * scale + 12f * scale;
            if (isDealer)
            {
                DrawLobbyDealerActions(scale, windowSize, btnY, count);
            }
            else
            {
                DrawLobbyPlayerActions(scale, windowSize, btnY);
            }
        }

        private void DrawLobbyDealerActions(float scale, Vector2 windowSize, float btnY, int playerCount)
        {
            bool includeNpcs = plugin.Configuration.IncludeNpcs;
            bool canStart = playerCount >= 2 || includeNpcs;

            ImGui.SetCursorPos(new Vector2((windowSize.X - 110f * scale) / 2f - 60f * scale, btnY));
            if (UiLayout.DrawCustomChoiceButton("Start Game", new Vector2(110f * scale, 36f * scale), UITheme.ForEnabled(canStart, UITheme.Primary), 1.0f, scale, SpawnPromptButtonFeedback) && canStart)
            {
                HandleStartNetworkGame(includeNpcs);
            }

            ImGui.SetCursorPos(new Vector2((windowSize.X - 110f * scale) / 2f + 60f * scale, btnY));
            if (UiLayout.DrawCustomChoiceButton("Disconnect", new Vector2(110f * scale, 36f * scale), UITheme.Danger, 1.0f, scale, SpawnPromptButtonFeedback))
            {
                HandleDisconnectNetworkGame();
            }
        }

        private void DrawLobbyPlayerActions(float scale, Vector2 windowSize, float btnY)
        {
            ImGui.SetCursorPos(new Vector2(12f * scale, btnY + 8f * scale));
            ImGui.TextColored(UITheme.WarningText, "Waiting to start...");

            ImGui.SetCursorPos(new Vector2(windowSize.X - 110f * scale - 12f * scale, btnY));
            if (UiLayout.DrawCustomChoiceButton("Disconnect", new Vector2(110f * scale, 36f * scale), UITheme.Danger, 1.0f, scale, SpawnPromptButtonFeedback))
            {
                HandleDisconnectNetworkGame();
            }
        }

        private void DrawPromptPanelBackground(ImDrawListPtr drawList, Vector2 windowSize, float scale)
        {
            var winPos = ImGui.GetWindowPos();
            drawList.AddRectFilled(winPos, winPos + windowSize, ImGui.ColorConvertFloat4ToU32(UITheme.PromptBackground), 8f * scale);
            drawList.AddRect(winPos, winPos + windowSize, ImGui.ColorConvertFloat4ToU32(UITheme.PromptBorder), 8f * scale, ImDrawFlags.None, 2f * scale);
        }

        private void DrawTextWithOutline(ImDrawListPtr drawList, Vector2 pos, string text, uint fillColor, uint outlineColor, float scale)
        {
            float offsetVal = MathF.Max(1.0f, MathF.Round(1.5f * scale));
            UiLayout.DrawOutlinedText(drawList, pos, text, fillColor, outlineColor, offsetVal, UiLayout.TextOutlineMode.Diagonal);
        }

        private void SpawnPromptButtonFeedback(Vector2 startPos, Vector2 size, float scale)
        {
            plugin.HandWindow.SpawnButtonFeedbackParticles(startPos + size * 0.5f, size, scale);
        }

        private uint ApplyOpacity(uint color, float opacity)
        {
            uint alpha = (color >> 24) & 0xFF;
            uint newAlpha = (uint)(alpha * opacity);
            return (color & 0x00FFFFFF) | (newAlpha << 24);
        }

        private void UpdatePromptDragState()
        {
            if (plugin.Configuration.IsLocked)
            {
                isDraggingPrompt = false;
                return;
            }

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                isDraggingPrompt = false;
            }
            else if (isDraggingPrompt)
            {
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    var delta = ImGui.GetIO().MouseDelta;
                    dragTargetPosition = ImGui.GetWindowPos() + delta;
                    plugin.UiState.IsPromptPositionCustom = true;
                    plugin.UiState.LastMovedElementName = "Prompt";
                    plugin.UiState.LastMovedElementCoords = dragTargetPosition.Value;
                }
            }
            else if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (!ImGui.IsAnyItemActive() && !ImGui.IsAnyItemHovered())
                {
                    isDraggingPrompt = true;
                }
            }
        }
    }
}

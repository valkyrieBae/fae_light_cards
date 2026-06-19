using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public class PyramidControlsWindow : Window
    {
        private readonly Plugin plugin;
        private bool shouldResetPosition = false;
        private bool isFirstDraw = true;
        private bool isDragCandidate = false;
        private bool isDraggingControls = false;
        private bool suppressClickAction = false;
        private Vector2 dragStartMousePos = Vector2.Zero;
        private Vector2 dragStartWindowPos = Vector2.Zero;
        private Vector2? dragTargetPosition = null;
        private float hoverTime = 0f;

        private static readonly Vector2 WindowSize = new(200f, 95f);
        private const float DragThreshold = 4f;

        public PyramidControlsWindow(Plugin plugin) : base("Pyramid Controls###FaeLightCardsPyramidControlsWindow")
        {
            this.plugin = plugin;
            this.IsOpen = false;
            this.RespectCloseHotkey = false;
            this.Size = WindowSize;
            this.SizeCondition = ImGuiCond.Always;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = WindowSize,
                MaximumSize = WindowSize
            };
            this.Flags = ImGuiWindowFlags.NoDecoration
                       | ImGuiWindowFlags.NoMove
                       | ImGuiWindowFlags.NoSavedSettings
                       | ImGuiWindowFlags.NoBackground;
        }

        public void ResetPosition()
        {
            shouldResetPosition = true;
        }

        public override void PreDraw()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

            if (dragTargetPosition.HasValue)
            {
                this.Position = dragTargetPosition.Value;
                this.PositionCondition = ImGuiCond.Always;
                dragTargetPosition = null;
            }
            else if (isFirstDraw || shouldResetPosition)
            {
                this.Position = GetDefaultPosition();
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

            this.Flags = ImGuiWindowFlags.NoDecoration
                       | ImGuiWindowFlags.NoMove
                       | ImGuiWindowFlags.NoSavedSettings
                       | ImGuiWindowFlags.NoBackground;
        }

        public override void Draw()
        {
            bool isManualDealer = plugin.GameState.ActiveMode == GameMode.Dealer && plugin.IsLocalDealer;
            bool isAutoPlayerDealer = plugin.GameState.ActiveMode == GameMode.Player
                                      && plugin.AppState.ActiveConnectionMode == ConnectionMode.LocalOnly;

            if (plugin.GameState.ActivePhase != GamePhase.Pyramid || (!isManualDealer && !isAutoPlayerDealer))
            {
                return;
            }

            float scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            bool actionClicked = false;
            bool actionButtonActive = false;
            Action? runAction = null;

            using (plugin.MediumFont.Push())
            {
                int row = plugin.GameState.ActiveRow;
                int multiplier = 6 - row;
                string promptText = $"Row {row} (x{multiplier})";

                bool isEnabled;
                string label;
                ButtonTheme buttonTheme;

                bool showAdvance = plugin.RulesEngine.IsActiveRowFullyFlipped() && plugin.RulesEngine.HasNextRow();
                if (isAutoPlayerDealer)
                {
                    bool isComplete = plugin.GameState.CurrentFlipIndex >= 15;
                    isEnabled = !isComplete;
                    label = isComplete ? "Done" : plugin.TurnManager.PyramidDealerPaused ? "Resume" : "Pause";
                    ButtonTheme enabledTheme = plugin.TurnManager.PyramidDealerPaused ? UITheme.Primary : UITheme.Pause;
                    buttonTheme = UITheme.ForEnabled(isEnabled, enabledTheme);
                }
                else
                {
                    bool hasPendingMatches = plugin.RulesEngine.HasPendingMatches();

                    // Visual-only overlays should not block this, but unresolved player matches should.
                    isEnabled = plugin.GameState.CurrentFlipIndex < 15 && !hasPendingMatches && plugin.TurnManager.DealerPhaseTransitionTimer <= 0f;
                    label = showAdvance ? "Next Row" : "Flip Card";
                    ButtonTheme enabledTheme = showAdvance ? UITheme.Secondary : UITheme.Primary;
                    buttonTheme = UITheme.ForEnabled(isEnabled, enabledTheme);
                }

                // Draw Row / Multiplier Text Centered
                var textSize = ImGui.CalcTextSize(promptText);
                ImGui.SetCursorPos(new Vector2((WindowSize.X - textSize.X) / 2f, 10f * scale));
                ImGui.Text(promptText);

                // Draw Button Centered
                float buttonW = 120f * scale;
                float buttonH = 34f * scale;
                ImGui.SetCursorPos(new Vector2((WindowSize.X - buttonW) / 2f, 40f * scale));

                actionClicked = UiLayout.DrawCustomChoiceButton(label, new Vector2(buttonW, buttonH), buttonTheme, 1.0f, scale);
                actionButtonActive = ImGui.IsItemActive();
                runAction = () =>
                {
                    if (isEnabled)
                    {
                        plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                        if (isAutoPlayerDealer)
                        {
                            plugin.TurnManager.PyramidDealerPaused = !plugin.TurnManager.PyramidDealerPaused;
                            if (!plugin.TurnManager.PyramidDealerPaused && plugin.TurnManager.PyramidDealerTimer < 0f)
                            {
                                plugin.TurnManager.PyramidDealerTimer = UIConstants.PyramidDealerStepDelay;
                            }
                        }
                        else if (showAdvance)
                        {
                            plugin.GameController.HandleAdvancePyramidRow();
                        }
                        else
                        {
                            plugin.GameController.HandleFlipPyramidCard();
                        }
                    }
                };
            }

            bool suppressActionThisFrame = UpdateDragState(actionButtonActive, scale);
            if (actionClicked && !suppressActionThisFrame)
            {
                runAction?.Invoke();
            }
        }

        public override void PostDraw()
        {
            ImGui.PopStyleVar();
        }

        private Vector2 GetDefaultPosition()
        {
            var viewport = ImGui.GetMainViewport();
            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;

            float scaledWidth = WindowSize.X * globalScale;
            float scaledHeight = WindowSize.Y * globalScale;

            var pyramidPos = plugin.UiState.PyramidPosition != Vector2.Zero
                ? plugin.UiState.PyramidPosition
                : new Vector2(viewport.Pos.X + 50f, viewport.Pos.Y + 150f);
            var pyramidSize = plugin.UiState.PyramidSize != Vector2.Zero
                ? plugin.UiState.PyramidSize
                : plugin.PyramidWindow.CalculatedSize;

            float gap = 20f * globalScale;
            float margin = 12f * globalScale;
            float x = pyramidPos.X + (pyramidSize.X - scaledWidth) * 0.5f;
            float y = pyramidPos.Y + pyramidSize.Y + gap;

            x = Math.Clamp(x, viewport.Pos.X + margin, viewport.Pos.X + viewport.Size.X - scaledWidth - margin);
            y = Math.Clamp(y, viewport.Pos.Y + margin, viewport.Pos.Y + viewport.Size.Y - scaledHeight - margin);

            return new Vector2(x, y);
        }

        private bool UpdateDragState(bool actionButtonActive, float scale)
        {
            if (plugin.Configuration.IsLocked)
            {
                ResetDragState();
                return false;
            }

            bool isWindowHovered = ImGui.IsWindowHovered();
            if (isWindowHovered && !isDraggingControls)
            {
                hoverTime += ImGui.GetIO().DeltaTime;
                if (hoverTime >= plugin.Configuration.TooltipDelay)
                {
                    ImGui.SetTooltip("Drag to move the pyramid controls.\nLock position in settings (/faecards).");
                }
            }
            else if (!isWindowHovered)
            {
                hoverTime = 0f;
            }

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                bool shouldSuppressClick = suppressClickAction;
                ResetDragState();
                return shouldSuppressClick;
            }

            bool canStartDrag = isWindowHovered || actionButtonActive;
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && canStartDrag)
            {
                isDragCandidate = true;
                isDraggingControls = false;
                suppressClickAction = false;
                dragStartMousePos = ImGui.GetIO().MousePos;
                dragStartWindowPos = ImGui.GetWindowPos();
            }

            if (!isDragCandidate)
            {
                return false;
            }

            Vector2 dragOffset = ImGui.GetIO().MousePos - dragStartMousePos;
            float dragThreshold = DragThreshold * scale;
            if (!isDraggingControls && dragOffset.LengthSquared() < dragThreshold * dragThreshold)
            {
                return false;
            }

            isDraggingControls = true;
            suppressClickAction = true;
            dragTargetPosition = dragStartWindowPos + dragOffset;
            plugin.UiState.LastMovedElementName = "Pyramid Controls";
            plugin.UiState.LastMovedElementCoords = dragTargetPosition.Value;
            return false;
        }

        private void ResetDragState()
        {
            isDragCandidate = false;
            isDraggingControls = false;
            suppressClickAction = false;
        }
    }
}

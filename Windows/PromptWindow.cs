using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public class PromptWindow : Window
    {
        private static readonly Vector2 PromptSize = new(300f, 120f);
        private readonly Plugin plugin;
        private bool shouldResetPosition = false;
        private Vector2? dragTargetPosition = null;
        private bool isFirstDraw = true;
        private int pushedStyleVarCount = 0;
        private Vector2? lastNormalPosition = null;
        private string roomInput = "";
        private float copiedTimer = 0f;
        private bool isDraggingPrompt = false;

        public PromptWindow(Plugin plugin) : base("Prompt###FaeLightCardsPromptWindow")
        {
            this.plugin = plugin;
            this.IsOpen = true;
            this.RespectCloseHotkey = false;
            this.Size = PromptSize;
            this.SizeCondition = ImGuiCond.Always;

            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = PromptSize,
                MaximumSize = PromptSize
            };

            this.Position = new Vector2(300, 300);
            this.PositionCondition = ImGuiCond.FirstUseEver;
        }

        public void ResetPosition()
        {
            shouldResetPosition = true;
            plugin.UiState.IsPromptPositionCustom = false;
            lastNormalPosition = null;
            plugin.UiState.PromptCenterPos = Vector2.Zero;
        }

        public Vector2 GetExpectedSize()
        {
            Vector2 expectedSize = PromptSize;
            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Connected)
            {
                expectedSize = new Vector2(300f, 150f);
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Networked && plugin.GameState.ActiveMode == GameMode.Undecided)
            {
                int playerCount = plugin.GameState.Players.Count;
                expectedSize = new Vector2(300f, 160f + Math.Max(0, playerCount) * 24f);
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.ConnectionFailed
                     && !string.IsNullOrWhiteSpace(plugin.AppState.ConnectionFailureMessage))
            {
                expectedSize = new Vector2(340f, 210f);
            }
            return expectedSize;
        }

        public Vector2 GetDefaultPosition()
        {
            var viewport = ImGui.GetMainViewport();
            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            Vector2 expectedSize = GetExpectedSize();
            float scaledWidth = expectedSize.X * globalScale;

            float x, y;
            if (plugin.GameState.ActivePhase == GamePhase.Pyramid && plugin.UiState.IsPyramidVisible)
            {
                var pyramidPos = plugin.UiState.PyramidPosition != Vector2.Zero ? plugin.UiState.PyramidPosition : new Vector2(viewport.Pos.X + 50f, viewport.Pos.Y + 150f);
                var pyramidSize = plugin.UiState.PyramidSize;

                x = pyramidPos.X + (pyramidSize.X - scaledWidth) / 2f;
                float gap = 20f * globalScale;
                y = pyramidPos.Y + pyramidSize.Y + gap;
            }
            else if (plugin.GameState.ActivePhase == GamePhase.Accumulation && plugin.UiState.IsHandVisible)
            {
                var handPos = plugin.HandWindow.LastSetWindowPosition != Vector2.Zero ? plugin.HandWindow.LastSetWindowPosition : new Vector2(viewport.Pos.X + 50f, viewport.Pos.Y + viewport.Size.Y - 200f);
                var handSize = plugin.HandWindow.LastSetWindowSize != Vector2.Zero ? plugin.HandWindow.LastSetWindowSize : new Vector2(300f, 150f);

                x = handPos.X + (handSize.X - scaledWidth) / 2f;
                float gap = 20f * globalScale;
                y = handPos.Y - (expectedSize.Y * globalScale) - gap;
            }
            else
            {
                x = viewport.Pos.X + (viewport.Size.X - scaledWidth) / 2f;

                // Position Y above the hand window (if registered) to guarantee a gap
                float handTop = plugin.HandWindow.Position?.Y ?? (viewport.Pos.Y + (viewport.Size.Y - plugin.HandWindow.LastSetWindowSize.Y) / 2f);
                float gap = 24f * globalScale;
                y = handTop - (expectedSize.Y * globalScale) - gap - 50f * globalScale;
            }

            // Ensure it does not go off-screen top
            y = Math.Max(viewport.Pos.Y + 10f * globalScale, y);

            return new Vector2(x, y);
        }

        public Vector2 CurrentCenter
        {
            get
            {
                float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
                if (plugin.UiState.IsPromptPositionCustom && plugin.UiState.PromptCenterPos != Vector2.Zero)
                {
                    return plugin.UiState.PromptCenterPos;
                }

                return GetDefaultPosition() + GetExpectedSize() * globalScale * 0.5f;
            }
        }

        public void ResetPromptState()
        {
            plugin.UiState.PromptState = UIState.PromptAnimState.Normal;
            plugin.UiState.PromptScale = 1.0f;
            plugin.UiState.ClickedButtonIndex = -1;
            plugin.UiState.PromptAnimTimer = 0f;
        }

        public void UpdatePromptTransition(float dt)
        {
            if (plugin.UiState.PromptState == UIState.PromptAnimState.Normal)
            {
                plugin.UiState.PromptScale = 1.0f;
                return;
            }

            plugin.UiState.PromptAnimTimer += dt;

            if (plugin.UiState.PromptState == UIState.PromptAnimState.ButtonClick)
            {
                if (plugin.UiState.PromptAnimTimer >= UIConstants.ButtonClickDuration)
                {
                    plugin.UiState.PromptState = UIState.PromptAnimState.Shrinking;
                    plugin.UiState.PromptAnimTimer = 0f;
                }
            }
            else if (plugin.UiState.PromptState == UIState.PromptAnimState.Shrinking)
            {
                float t = Math.Clamp(plugin.UiState.PromptAnimTimer / UIConstants.PromptShrinkDuration, 0f, 1f);
                plugin.UiState.PromptScale = 1.0f - t;

                if (plugin.UiState.PromptAnimTimer >= UIConstants.PromptShrinkDuration)
                {
                    plugin.UiState.PromptState = UIState.PromptAnimState.Hidden;
                    plugin.UiState.PromptScale = 0f;
                    plugin.UiState.PromptAnimTimer = 0f;
                }
            }
            else if (plugin.UiState.PromptState == UIState.PromptAnimState.Hidden)
            {
                plugin.UiState.PromptScale = 0f;
            }
            else if (plugin.UiState.PromptState == UIState.PromptAnimState.Growing)
            {
                float t = Math.Clamp(plugin.UiState.PromptAnimTimer / UIConstants.PromptGrowDuration, 0f, 1f);
                plugin.UiState.PromptScale = t;

                if (plugin.UiState.PromptAnimTimer >= UIConstants.PromptGrowDuration)
                {
                    plugin.UiState.PromptState = UIState.PromptAnimState.Normal;
                    plugin.UiState.PromptScale = 1.0f;
                    plugin.UiState.PromptAnimTimer = 0f;
                    plugin.UiState.ClickedButtonIndex = -1;
                }
            }
        }


        public override void PreDraw()
        {
            Vector2 expectedSize = GetExpectedSize();

            if (plugin.UiState.PromptState != UIState.PromptAnimState.Normal)
            {
                float scale = Math.Max(0.001f, plugin.UiState.PromptScale);
                Vector2 currentSize = expectedSize * scale;
                this.Size = currentSize;
                this.SizeCondition = ImGuiCond.Always;
                this.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = currentSize,
                    MaximumSize = currentSize
                };
                float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;

                // Center around the last normal position (or CurrentCenter if not set)
                Vector2 center = lastNormalPosition.HasValue
                    ? (lastNormalPosition.Value + expectedSize * globalScale * 0.5f)
                    : CurrentCenter;

                this.Position = center - (expectedSize * scale * globalScale) * 0.5f;
                this.PositionCondition = ImGuiCond.Always;
            }
            else
            {
                this.Size = expectedSize;
                this.SizeCondition = ImGuiCond.Always;
                this.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = expectedSize,
                    MaximumSize = expectedSize
                };

                if (dragTargetPosition.HasValue)
                {
                    this.Position = dragTargetPosition.Value;
                    this.PositionCondition = ImGuiCond.Always;
                    lastNormalPosition = dragTargetPosition.Value;
                    dragTargetPosition = null;
                }
                else if (lastNormalPosition.HasValue)
                {
                    // Restore the pre-animation position
                    this.Position = lastNormalPosition.Value;
                    this.PositionCondition = ImGuiCond.Always;
                    lastNormalPosition = null; // Clear so it only forces position once
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
            }

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

            pushedStyleVarCount = 0;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            pushedStyleVarCount++;
        }

        private uint ApplyOpacity(uint color, float opacity)
        {
            uint alpha = (color >> 24) & 0xFF;
            uint newAlpha = (uint)(alpha * opacity);
            return (color & 0x00FFFFFF) | (newAlpha << 24);
        }

        private bool CanUseDealerBusRideControls()
        {
            bool isFirstCard = plugin.GameState.BusRideCurrentCard == null;
            return (isFirstCard || plugin.TurnManager.DealerNpcHasGuessed)
                   && !plugin.IsAnimationPlaying
                   && plugin.TurnManager.DealerTransitionTimer <= 0f
                   && plugin.UiState.BusRideResetTimer <= 0f
                   && plugin.UiState.BusRidePromptGrowTimer <= 0f
                   && plugin.UiState.BusRideVictoryResetTimer <= 0f;
        }

        private List<GuessOption> GetDealerPhaseChangeOptions()
        {
            Vector4 text = new Vector4(1f, 1f, 1f, 1.0f);
            Vector4 primaryFill = new Vector4(0.2f, 0.5f, 0.3f, 1.0f);
            Vector4 primaryOutline = new Vector4(0.1f, 0.3f, 0.15f, 1.0f);
            Vector4 endFill = new Vector4(0.85f, 0.15f, 0.2f, 1.0f);
            Vector4 endOutline = new Vector4(0.4f, 0.05f, 0.08f, 1.0f);
            Vector4 noFill = new Vector4(0.2f, 0.4f, 0.6f, 1.0f);
            Vector4 noOutline = new Vector4(0.1f, 0.2f, 0.3f, 1.0f);

            if (plugin.UiState.DealerPhaseChangeEndGameConfirmationPending)
            {
                return new List<GuessOption>
                {
                    new GuessOption("Yes", new ButtonTheme(endFill, endOutline, text, endOutline)),
                    new GuessOption("No", new ButtonTheme(noFill, noOutline, text, noOutline))
                };
            }

            string primaryLabel = plugin.UiState.DealerPhaseChangePrompt == UIState.DealerPhaseChangePromptState.Phase1Complete
                ? "Build Pyramid"
                : "Ride Bus";

            return new List<GuessOption>
            {
                new GuessOption(primaryLabel, new ButtonTheme(primaryFill, primaryOutline, text, primaryOutline)),
                new GuessOption("End Game", new ButtonTheme(endFill, endOutline, text, endOutline))
            };
        }

        private string GetDealerPhaseChangePromptText()
        {
            if (plugin.UiState.DealerPhaseChangeEndGameConfirmationPending)
            {
                return "End Game?";
            }

            return plugin.UiState.DealerPhaseChangePrompt switch
            {
                UIState.DealerPhaseChangePromptState.Phase1Complete => "Phase 1 Complete",
                UIState.DealerPhaseChangePromptState.Phase2Complete => "Pyramid Complete",
                _ => string.Empty
            };
        }

        private static string FormatConnectionFailurePromptMessage(string message)
        {
            string formatted = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            if (string.IsNullOrWhiteSpace(formatted))
            {
                return string.Empty;
            }

            formatted = formatted
                .Replace("Update required: you are on version ", "Update required:\nyou are on version ", StringComparison.Ordinal)
                .Replace(", and you need at least version ", ",\nand you need at least\nversion ", StringComparison.Ordinal);

            const int maxLength = 160;
            if (formatted.Length > maxLength)
            {
                formatted = formatted[..maxLength].TrimEnd() + "...";
            }

            return formatted;
        }

        private void HandleDealerPhaseChangePromptClick(int optionIndex)
        {
            var promptState = plugin.UiState.DealerPhaseChangePrompt;
            if (promptState == UIState.DealerPhaseChangePromptState.None)
            {
                return;
            }

            if (plugin.UiState.DealerPhaseChangeEndGameConfirmationPending)
            {
                if (optionIndex == 0)
                {
                    plugin.GameCoordinator.ClearDealerPhaseChangePrompt();
                    plugin.GameController.EndGame();
                }
                else if (optionIndex == 1)
                {
                    plugin.UiState.DealerPhaseChangeEndGameConfirmationPending = false;
                }
                return;
            }

            if (optionIndex == 1)
            {
                plugin.UiState.DealerPhaseChangeEndGameConfirmationPending = true;
                return;
            }

            if (promptState == UIState.DealerPhaseChangePromptState.Phase1Complete)
            {
                plugin.GameController.HandleDealerAdvanceNextPlayer();
            }
            else if (promptState == UIState.DealerPhaseChangePromptState.Phase2Complete)
            {
                plugin.GameCoordinator.BeginDealerBusRiderSelection();
            }
        }

        public override void Draw()
        {
            if (plugin.UiState.PromptScale <= 0.01f) return;

            if (copiedTimer > 0f)
            {
                copiedTimer -= ImGui.GetIO().DeltaTime;
            }

            Vector2 expectedSize = GetExpectedSize();
            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            float scale = plugin.UiState.PromptScale * globalScale;
            Vector2 expectedWindowSize = expectedSize * scale;

            if (plugin.UiState.PromptState == UIState.PromptAnimState.Normal)
            {
                plugin.UiState.PromptCenterPos = ImGui.GetWindowPos() + expectedWindowSize * 0.5f;
                lastNormalPosition = ImGui.GetWindowPos(); // Save normal position dynamically
            }

            bool isLocalDealer = plugin.IsLocalDealer;

            string promptText;
            IReadOnlyList<GuessOption> options;

            if (plugin.AppState.ChosenGameMode == GameMode.Undecided)
            {
                promptText = "Select Game Mode";
                options = new List<GuessOption>
                {
                    new GuessOption("Player", new ButtonTheme(
                        new Vector4(0.2f, 0.4f, 0.6f, 1.0f),
                        new Vector4(0.1f, 0.2f, 0.3f, 1.0f),
                        new Vector4(1f, 1f, 1f, 1.0f),
                        new Vector4(0.1f, 0.2f, 0.3f, 1.0f)
                    )),
                    new GuessOption("Dealer", new ButtonTheme(
                        new Vector4(0.85f, 0.15f, 0.2f, 1.0f),
                        new Vector4(0.4f, 0.05f, 0.08f, 1.0f),
                        new Vector4(1f, 1f, 1f, 1.0f),
                        new Vector4(0.4f, 0.05f, 0.08f, 1.0f)
                    ))
                };
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Undecided)
            {
                promptText = "Select Connection Mode";
                options = new List<GuessOption>
                {
                    new GuessOption("Local-Only", new ButtonTheme(
                        new Vector4(0.2f, 0.4f, 0.6f, 1.0f),
                        new Vector4(0.1f, 0.2f, 0.3f, 1.0f),
                        new Vector4(1f, 1f, 1f, 1.0f),
                        new Vector4(0.1f, 0.2f, 0.3f, 1.0f)
                    )),
                    new GuessOption("Networked", new ButtonTheme(
                        new Vector4(0.85f, 0.15f, 0.2f, 1.0f),
                        new Vector4(0.4f, 0.05f, 0.08f, 1.0f),
                        new Vector4(1f, 1f, 1f, 1.0f),
                        new Vector4(0.4f, 0.05f, 0.08f, 1.0f)
                    ))
                };
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Connecting)
            {
                promptText = "Looking for server...";
                options = new List<GuessOption>();
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.ConnectionFailed)
            {
                string failureMessage = FormatConnectionFailurePromptMessage(plugin.AppState.ConnectionFailureMessage);
                promptText = string.IsNullOrWhiteSpace(failureMessage)
                    ? "Connection Failed"
                    : $"Connection Failed\n{failureMessage}";
                options = new List<GuessOption>
                {
                    new GuessOption("Retry", new ButtonTheme(
                        new Vector4(0.2f, 0.5f, 0.3f, 1.0f),
                        new Vector4(0.1f, 0.3f, 0.15f, 1.0f),
                        new Vector4(1f, 1f, 1f, 1.0f),
                        new Vector4(0.1f, 0.3f, 0.15f, 1.0f)
                    )),
                    new GuessOption("Local-Only", new ButtonTheme(
                        new Vector4(0.85f, 0.15f, 0.2f, 1.0f),
                        new Vector4(0.4f, 0.05f, 0.08f, 1.0f),
                        new Vector4(1f, 1f, 1f, 1.0f),
                        new Vector4(0.4f, 0.05f, 0.08f, 1.0f)
                    ))
                };
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Connected || (plugin.AppState.ActiveConnectionMode == ConnectionMode.Networked && plugin.GameState.ActiveMode == GameMode.Undecided))
            {
                promptText = string.Empty;
                options = new List<GuessOption>();
            }
            else if (plugin.GameState.ActivePhase == GamePhase.TieChoice)
            {
                return;
            }
            else if (plugin.GameState.HasPendingDrinkTarget)
            {
                return;
            }
            else if (plugin.GameState.ActiveMode == GameMode.Dealer && isLocalDealer)
            {
                if (plugin.UiState.DealerPhaseChangePrompt != UIState.DealerPhaseChangePromptState.None)
                {
                    promptText = GetDealerPhaseChangePromptText();
                    options = GetDealerPhaseChangeOptions();
                }
                else if (plugin.GameState.ActivePhase == GamePhase.Accumulation)
                {
                    if (plugin.TurnManager.DealerNeedNextPlayer)
                    {
                        var nonDealers = plugin.GameState.Players.Where(p => !p.IsDealer).ToList();
                        bool allFinished = nonDealers.Count > 0 && nonDealers.All(p => p.Hand.Count == 4);
                        promptText = allFinished ? "Phase 1 Complete" : "Next Player Up";
                        string btnLabel = allFinished ? "Build Pyramid" : "Next Player";

                        Vector4 fill = new Vector4(0.2f, 0.5f, 0.3f, 1.0f);
                        Vector4 outline = new Vector4(0.1f, 0.3f, 0.15f, 1.0f);
                        Vector4 text = new Vector4(1f, 1f, 1f, 1.0f);

                        options = new List<GuessOption>
                        {
                            new GuessOption(btnLabel, new ButtonTheme(fill, outline, text, outline))
                        };
                    }
                    else
                    {
                        promptText = $"Deal to {plugin.GameState.DealerActivePlayerName}?";
                        bool isEnabled = plugin.TurnManager.DealerNpcHasGuessed && !plugin.IsAnimationPlaying && plugin.TurnManager.DealerTransitionTimer <= 0f;
                        Vector4 fill = isEnabled ? new Vector4(0.2f, 0.5f, 0.3f, 1.0f) : new Vector4(0.2f, 0.2f, 0.2f, 0.4f);
                        Vector4 outline = isEnabled ? new Vector4(0.1f, 0.3f, 0.15f, 1.0f) : new Vector4(0.4f, 0.4f, 0.4f, 0.4f);
                        Vector4 text = isEnabled ? new Vector4(1f, 1f, 1f, 1.0f) : new Vector4(0.6f, 0.6f, 0.6f, 0.4f);

                        options = new List<GuessOption>
                        {
                            new GuessOption("Deal Card", new ButtonTheme(fill, outline, text, outline))
                        };
                    }
                }

                else if (plugin.GameState.ActivePhase == GamePhase.BusRide)
                {
                    bool isEnabled = CanUseDealerBusRideControls();
                    Vector4 enabledText = new Vector4(1f, 1f, 1f, 1.0f);
                    Vector4 disabledFill = new Vector4(0.2f, 0.2f, 0.2f, 0.4f);
                    Vector4 disabledOutline = new Vector4(0.4f, 0.4f, 0.4f, 0.4f);
                    Vector4 disabledText = new Vector4(0.6f, 0.6f, 0.6f, 0.4f);
                    Vector4 dealFill = isEnabled ? new Vector4(0.2f, 0.5f, 0.3f, 1.0f) : disabledFill;
                    Vector4 dealOutline = isEnabled ? new Vector4(0.1f, 0.3f, 0.15f, 1.0f) : disabledOutline;
                    Vector4 endFill = isEnabled ? new Vector4(0.85f, 0.15f, 0.2f, 1.0f) : disabledFill;
                    Vector4 endOutline = isEnabled ? new Vector4(0.4f, 0.05f, 0.08f, 1.0f) : disabledOutline;
                    Vector4 noFill = isEnabled ? new Vector4(0.2f, 0.4f, 0.6f, 1.0f) : disabledFill;
                    Vector4 noOutline = isEnabled ? new Vector4(0.1f, 0.2f, 0.3f, 1.0f) : disabledOutline;
                    Vector4 text = isEnabled ? enabledText : disabledText;

                    if (plugin.UiState.BusRideEndConfirmationPending)
                    {
                        promptText = "End Bus Ride?";
                        options = new List<GuessOption>
                        {
                            new GuessOption("Yes", new ButtonTheme(endFill, endOutline, text, endOutline)),
                            new GuessOption("No", new ButtonTheme(noFill, noOutline, text, noOutline))
                        };
                    }
                    else
                    {
                        promptText = $"Deal to {plugin.GameState.BusRiderName}?";
                        options = new List<GuessOption>
                        {
                            new GuessOption("Deal Card", new ButtonTheme(dealFill, dealOutline, text, dealOutline)),
                            new GuessOption("End Phase", new ButtonTheme(endFill, endOutline, text, endOutline))
                        };
                    }
                }
                else
                {
                    return;
                }
            }
            else if (plugin.UiState.PromptState == UIState.PromptAnimState.ButtonClick || plugin.UiState.PromptState == UIState.PromptAnimState.Shrinking)
            {
                promptText = plugin.UiState.CachedPromptText;
                options = plugin.UiState.CachedOptions;
            }
            else
            {
                var stage = plugin.RulesEngine.GetCurrentStage();
                if (stage == null) return;
                promptText = stage.PromptText;
                options = stage.Options;
            }

            using (plugin.MediumFont.Push())
            {
                ImGui.SetWindowFontScale(plugin.UiState.PromptScale);
                var drawList = ImGui.GetWindowDrawList();

                if (plugin.Configuration.ShowDebugBounds)
                {
                    var wp = ImGui.GetWindowPos();
                    UiDebugDraw.DrawBounds(wp, wp + expectedWindowSize);
                }

                if (plugin.UiState.PromptScale > 0.01f)
                {
                    if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Connected)
                    {
                        DrawEnterRoomCodeScreen(scale, expectedWindowSize);
                        return;
                    }
                    else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Networked && plugin.GameState.ActiveMode == GameMode.Undecided)
                    {
                        DrawLobbyScreen(scale, expectedWindowSize);
                        return;
                    }
                }

                var textSize = ImGui.CalcTextSize(promptText);
                int numOptions = options.Count;

                // Draw prompt text with outline centered, scaled, and pixel-aligned
                var textX = MathF.Round(ImGui.GetWindowPos().X + (expectedWindowSize.X - textSize.X) / 2f);
                var textY = numOptions == 0
                    ? MathF.Round(ImGui.GetWindowPos().Y + (expectedWindowSize.Y - textSize.Y) / 2f)
                    : MathF.Round(ImGui.GetWindowPos().Y + 5f * scale);
                var textStartPos = new Vector2(textX, textY);

                float opacity = plugin.UiState.PromptScale;
                DrawTextWithOutline(drawList, textStartPos, promptText, ApplyOpacity(0xFFFFFFFF, opacity), ApplyOpacity(0xFF000000, opacity), scale);

                // Draw choice buttons side-by-side (2 or 4 options)
                float buttonW = (numOptions == 4 ? 52f : 130f) * scale;
                float buttonH = 46f * scale;
                float spacing = (numOptions == 4 ? 12f : 16f) * scale;
                float totalW = numOptions * buttonW + (numOptions - 1) * spacing;
                float startX = (expectedWindowSize.X - totalW) / 2f;
                float buttonY = textSize.Y + 15f * scale;

                bool isLockedOut = plugin.IsAnimationPlaying || plugin.GameState.HasPendingDrinkTarget || plugin.UiState.PromptState != UIState.PromptAnimState.Normal;

                for (int i = 0; i < numOptions; i++)
                {
                    float currentButtonScale = 1.0f;
                    if (plugin.UiState.PromptState == UIState.PromptAnimState.ButtonClick && plugin.UiState.ClickedButtonIndex == i)
                    {
                        float t = plugin.UiState.PromptAnimTimer / UIConstants.ButtonClickDuration;
                        currentButtonScale = 1.0f - 0.15f * MathF.Sin(t * MathF.PI);
                    }

                    Vector2 normalSize = new Vector2(buttonW, buttonH);
                    Vector2 actualSize = normalSize * currentButtonScale;

                    float buttonX = startX + i * (buttonW + spacing);
                    Vector2 normalPos = new Vector2(buttonX, buttonY);

                    if (currentButtonScale < 1.0f)
                    {
                        Vector2 offset = (normalSize - actualSize) * 0.5f;
                        ImGui.SetCursorPos(normalPos + offset);
                    }
                    else
                    {
                        ImGui.SetCursorPos(normalPos);
                    }

                    var option = options[i];
                    bool clicked = DrawCustomChoiceButton(
                        option.Label,
                        actualSize,
                        option.Theme.GetFill(opacity),
                        option.Theme.GetOutline(opacity),
                        option.Theme.GetText(opacity),
                        option.Theme.GetTextOutline(opacity),
                        scale * currentButtonScale
                    );

                    if (clicked && !isLockedOut)
                    {
                        plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                        if (plugin.AppState.ChosenGameMode == GameMode.Undecided)
                        {
                            if (i == 0) // Player
                            {
                                plugin.AppState.ChosenGameMode = GameMode.Player;
                            }
                            else if (i == 1) // Dealer
                            {
                                plugin.AppState.ChosenGameMode = GameMode.Dealer;
                            }
                        }
                        else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Undecided)
                        {
                            if (i == 0) // Local-Only
                            {
                                plugin.AppState.ActiveConnectionMode = ConnectionMode.LocalOnly;
                                plugin.SetGameMode(plugin.AppState.ChosenGameMode);
                            }
                            else if (i == 1) // Networked
                            {
                                plugin.AppState.ActiveConnectionMode = ConnectionMode.Connecting;
                                plugin.GameController?.Dispose();
                                plugin.GameController = new NetworkController(plugin);
                            }
                        }
                        else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.ConnectionFailed)
                        {
                            if (i == 0) // Retry
                            {
                                plugin.AppState.ActiveConnectionMode = ConnectionMode.Connecting;
                                plugin.GameController?.Dispose();
                                plugin.GameController = new NetworkController(plugin);
                            }
                            else if (i == 1) // Local-Only
                            {
                                plugin.AppState.ActiveConnectionMode = ConnectionMode.LocalOnly;
                                plugin.SetGameMode(plugin.AppState.ChosenGameMode);
                            }
                        }
                        else if (plugin.GameState.ActiveMode == GameMode.Dealer && isLocalDealer)
                        {
                            if (plugin.UiState.DealerPhaseChangePrompt != UIState.DealerPhaseChangePromptState.None)
                            {
                                HandleDealerPhaseChangePromptClick(i);
                            }
                            else if (plugin.GameState.ActivePhase == GamePhase.Accumulation)
                            {
                                if (plugin.TurnManager.DealerNeedNextPlayer)
                                {
                                    plugin.GameController.HandleDealerAdvanceNextPlayer();
                                }
                                else if (plugin.TurnManager.DealerNpcHasGuessed && plugin.TurnManager.DealerTransitionTimer <= 0f)
                                {
                                    plugin.GameController.HandleDealerDeal();
                                }
                            }

                            else if (plugin.GameState.ActivePhase == GamePhase.BusRide)
                            {
                                bool canUseBusRideControls = CanUseDealerBusRideControls();
                                if (plugin.UiState.BusRideEndConfirmationPending)
                                {
                                    if (i == 0 && canUseBusRideControls)
                                    {
                                        plugin.UiState.BusRideEndConfirmationPending = false;
                                        plugin.GameController.EndGame();
                                    }
                                    else if (i == 1 && canUseBusRideControls)
                                    {
                                        plugin.UiState.BusRideEndConfirmationPending = false;
                                    }
                                }
                                else if (canUseBusRideControls)
                                {
                                    if (i == 0)
                                    {
                                        plugin.GameController.HandleDealerDealBusCard();
                                    }
                                    else if (i == 1)
                                    {
                                        plugin.UiState.BusRideEndConfirmationPending = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            plugin.GameController.HandlePlayerGuess(i);
                        }
                    }
                }
            }

            if (!plugin.Configuration.IsLocked)
            {
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

        private void DrawTextWithOutline(ImDrawListPtr drawList, Vector2 pos, string text, uint fillColor, uint outlineColor, float scale)
        {
            float offsetVal = MathF.Max(1.0f, MathF.Round(1.5f * scale));
            var outlineOffset = new Vector2(offsetVal, offsetVal);

            drawList.AddText(pos - outlineOffset, outlineColor, text);
            drawList.AddText(pos + new Vector2(outlineOffset.X, -outlineOffset.Y), outlineColor, text);
            drawList.AddText(pos + new Vector2(-outlineOffset.X, outlineOffset.Y), outlineColor, text);
            drawList.AddText(pos + outlineOffset, outlineColor, text);

            drawList.AddText(pos, fillColor, text);
        }

        private bool DrawCustomChoiceButton(string label, Vector2 size, Vector4 fillCol, Vector4 outlineCol, Vector4 textCol, Vector4 textOutlineCol, float scale)
        {
            var startPos = ImGui.GetCursorScreenPos();

            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.08f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.15f));

            bool clicked = ImGui.Button($"##{label}", size);

            ImGui.PopStyleColor(3);

            if (clicked)
            {
                plugin.HandWindow.SpawnButtonFeedbackParticles(startPos + size * 0.5f, size, scale);
            }

            bool pressed = ImGui.IsItemActive();
            var drawStartPos = pressed ? startPos + new Vector2(1.5f * scale, 1.5f * scale) : startPos;
            var drawEndPos = drawStartPos + size;

            var drawList = ImGui.GetWindowDrawList();

            // Fill background
            drawList.AddRectFilled(drawStartPos, drawEndPos, ImGui.ColorConvertFloat4ToU32(fillCol), 6f * scale);

            // Draw outline
            drawList.AddRect(drawStartPos, drawEndPos, ImGui.ColorConvertFloat4ToU32(outlineCol), 6f * scale, ImDrawFlags.None, 2.5f * scale);

            // Draw text centered and pixel-aligned
            var textSize = ImGui.CalcTextSize(label);
            var textPos = new Vector2(
                MathF.Round(drawStartPos.X + (size.X - textSize.X) * 0.5f),
                MathF.Round(drawStartPos.Y + (size.Y - textSize.Y) * 0.5f - 1.0f * scale)
            );

            // Draw text outline
            float thickness = MathF.Max(1.0f, MathF.Round(1.5f * scale));
            uint outlineU32 = ImGui.ColorConvertFloat4ToU32(textOutlineCol);
            uint textU32 = ImGui.ColorConvertFloat4ToU32(textCol);

            for (float dx = -thickness; dx <= thickness; dx += thickness)
            {
                for (float dy = -thickness; dy <= thickness; dy += thickness)
                {
                    if (dx == 0 && dy == 0) continue;
                    drawList.AddText(textPos + new Vector2(dx, dy), outlineU32, label);
                }
            }
            drawList.AddText(textPos, textU32, label);

            return clicked;
        }

        private void DrawEnterRoomCodeScreen(float scale, Vector2 windowSize)
        {
            var drawList = ImGui.GetWindowDrawList();

            // Draw dark background card
            var winPos = ImGui.GetWindowPos();
            uint bgCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.08f, 0.85f));
            uint borderCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.3f, 0.9f));
            drawList.AddRectFilled(winPos, winPos + windowSize, bgCol, 8f * scale);
            drawList.AddRect(winPos, winPos + windowSize, borderCol, 8f * scale, ImDrawFlags.None, 2f * scale);

            string title = "Enter Room Code";
            var textSize = ImGui.CalcTextSize(title);
            var textPos = ImGui.GetWindowPos() + new Vector2((windowSize.X - textSize.X) / 2f, 10f * scale);
            DrawTextWithOutline(drawList, textPos, title, ApplyOpacity(0xFFFFFFFF, plugin.UiState.PromptScale), ApplyOpacity(0xFF000000, plugin.UiState.PromptScale), scale);

            // Room code InputText
            ImGui.SetCursorPos(new Vector2((windowSize.X - 120f * scale) / 2f, 40f * scale));
            ImGui.SetNextItemWidth(120f * scale);

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f * scale);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.5f * scale);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.12f, 0.12f, 0.18f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.25f, 0.25f, 0.38f, 0.90f));
            ImGui.InputText("##RoomCodeInput", ref roomInput, 4, ImGuiInputTextFlags.CharsUppercase | ImGuiInputTextFlags.CharsNoBlank);
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);

            // Button: Join Room
            bool canJoin = roomInput.Trim().Length == 4;
            Vector4 joinFill = canJoin ? new Vector4(0.2f, 0.5f, 0.3f, 1.0f) : new Vector4(0.2f, 0.2f, 0.2f, 0.4f);
            Vector4 joinOutline = canJoin ? new Vector4(0.1f, 0.3f, 0.15f, 1.0f) : new Vector4(0.4f, 0.4f, 0.4f, 0.4f);
            Vector4 joinTextCol = canJoin ? new Vector4(1f, 1f, 1f, 1.0f) : new Vector4(0.6f, 0.6f, 0.6f, 0.4f);

            ImGui.SetCursorPos(new Vector2((windowSize.X - 110f * scale) / 2f - 60f * scale, 90f * scale));
            if (DrawCustomChoiceButton("Join", new Vector2(110f * scale, 36f * scale), joinFill, joinOutline, joinTextCol, joinOutline, scale) && canJoin)
            {
                plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                if (plugin.GameController is NetworkController nc)
                {
                    nc.JoinRoom(roomInput);
                }
            }

            // Button: Cancel
            Vector4 cancelFill = new Vector4(0.85f, 0.15f, 0.2f, 1.0f);
            Vector4 cancelOutline = new Vector4(0.4f, 0.05f, 0.08f, 1.0f);
            Vector4 cancelTextCol = new Vector4(1f, 1f, 1f, 1.0f);

            ImGui.SetCursorPos(new Vector2((windowSize.X - 110f * scale) / 2f + 60f * scale, 90f * scale));
            if (DrawCustomChoiceButton("Cancel", new Vector2(110f * scale, 36f * scale), cancelFill, cancelOutline, cancelTextCol, cancelOutline, scale))
            {
                plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                plugin.ResetGame();
            }
        }

        private void DrawLobbyScreen(float scale, Vector2 windowSize)
        {
            var drawList = ImGui.GetWindowDrawList();

            // Draw dark background card
            var winPos = ImGui.GetWindowPos();
            uint bgCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.08f, 0.85f));
            uint borderCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.3f, 0.9f));
            drawList.AddRectFilled(winPos, winPos + windowSize, bgCol, 8f * scale);
            drawList.AddRect(winPos, winPos + windowSize, borderCol, 8f * scale, ImDrawFlags.None, 2f * scale);

            string roomId = plugin.AppState.CurrentRoomId;
            bool isDealer = plugin.AppState.ChosenGameMode == GameMode.Dealer;

            // Room code info
            string codeText = $"Room Code: {roomId}";
            var codeTextPos = ImGui.GetWindowPos() + new Vector2(12f * scale, 10f * scale);
            DrawTextWithOutline(drawList, codeTextPos, codeText, ApplyOpacity(0xFFFFFFFF, plugin.UiState.PromptScale), ApplyOpacity(0xFF000000, plugin.UiState.PromptScale), scale);

            // Copy button
            ImGui.SetCursorPos(new Vector2(windowSize.X - 95f * scale - 12f * scale, 8f * scale));
            string copyLabel = copiedTimer > 0f ? "Copied!" : "Copy";
            Vector4 copyFill = new Vector4(0.2f, 0.4f, 0.6f, 1.0f);
            Vector4 copyOutline = new Vector4(0.1f, 0.2f, 0.3f, 1.0f);
            Vector4 copyTextCol = new Vector4(1f, 1f, 1f, 1.0f);

            if (DrawCustomChoiceButton(copyLabel, new Vector2(95f * scale, 24f * scale), copyFill, copyOutline, copyTextCol, copyOutline, scale * 0.8f))
            {
                plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                ImGui.SetClipboardText(roomId);
                copiedTimer = 2.0f;
            }

            // Server status & players count
            ImGui.SetCursorPos(new Vector2(12f * scale, 38f * scale));
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1.0f), "Server: Connected");

            int count = plugin.GameState.Players.Count;
            ImGui.SetCursorPos(new Vector2(12f * scale, 56f * scale));
            ImGui.Text($"Connected Players: {count}/8");

            // List of players
            float startY = 76f * scale;
            ImGui.SetCursorPos(new Vector2(12f * scale, startY));
            ImGui.BeginChild("##LobbyPlayersChild", new Vector2(windowSize.X - 24f * scale, count * 24f * scale + 5f * scale), false, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar);
            for (int i = 0; i < count; i++)
            {
                var p = plugin.GameState.Players[i];
                string nameStr = p.Name;
                if (p.IsDealer) nameStr += " (Dealer)";
                if (p.IsLocal) nameStr += " [You]";

                ImGui.TextColored(p.IsDealer ? new Vector4(0.98f, 0.75f, 0.14f, 1f) : new Vector4(1f, 1f, 1f, 1f), $" - {nameStr}");
            }
            ImGui.EndChild();

            // Lower area for actions
            float btnY = startY + count * 24f * scale + 12f * scale;

            Vector4 cancelFill = new Vector4(0.85f, 0.15f, 0.2f, 1.0f);
            Vector4 cancelOutline = new Vector4(0.4f, 0.05f, 0.08f, 1.0f);
            Vector4 cancelTextCol = new Vector4(1f, 1f, 1f, 1.0f);

            if (isDealer)
            {
                bool includeNpcs = plugin.Configuration.IncludeNpcs;

                // Dealer actions: Start Game and Disconnect
                bool canStart = count >= 2 || includeNpcs; // Dealer + at least 1 player OR adding NPCs
                Vector4 startFill = canStart ? new Vector4(0.2f, 0.5f, 0.3f, 1.0f) : new Vector4(0.2f, 0.2f, 0.2f, 0.4f);
                Vector4 startOutline = canStart ? new Vector4(0.1f, 0.3f, 0.15f, 1.0f) : new Vector4(0.4f, 0.4f, 0.4f, 0.4f);
                Vector4 startTextCol = canStart ? new Vector4(1f, 1f, 1f, 1.0f) : new Vector4(0.6f, 0.6f, 0.6f, 0.4f);

                ImGui.SetCursorPos(new Vector2((windowSize.X - 110f * scale) / 2f - 60f * scale, btnY));
                if (DrawCustomChoiceButton("Start Game", new Vector2(110f * scale, 36f * scale), startFill, startOutline, startTextCol, startOutline, scale) && canStart)
                {
                    plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                    if (plugin.GameController is NetworkController nc)
                    {
                        nc.StartGame(includeNpcs);
                    }
                }

                ImGui.SetCursorPos(new Vector2((windowSize.X - 110f * scale) / 2f + 60f * scale, btnY));
                if (DrawCustomChoiceButton("Disconnect", new Vector2(110f * scale, 36f * scale), cancelFill, cancelOutline, cancelTextCol, cancelOutline, scale))
                {
                    plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                    plugin.ResetGame();
                }
            }
            else
            {
                // Player actions: Disconnect and text "Waiting to start..."
                ImGui.SetCursorPos(new Vector2(12f * scale, btnY + 8f * scale));
                ImGui.TextColored(new Vector4(1.0f, 0.6f, 0.0f, 1.0f), "Waiting to start...");

                ImGui.SetCursorPos(new Vector2(windowSize.X - 110f * scale - 12f * scale, btnY));
                if (DrawCustomChoiceButton("Disconnect", new Vector2(110f * scale, 36f * scale), cancelFill, cancelOutline, cancelTextCol, cancelOutline, scale))
                {
                    plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                    plugin.ResetGame();
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

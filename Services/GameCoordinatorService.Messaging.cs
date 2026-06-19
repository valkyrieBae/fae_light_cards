using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class GameCoordinatorService
    {
        public void ShowSecondaryMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (plugin.UiState.SecondaryMessage != null && plugin.UiState.SecondaryMessageAnimTime >= 0f)
            {
                plugin.UiState.SecondaryMessageQueue.Enqueue(message);
                return;
            }

            plugin.UiState.SecondaryMessage = message;
            plugin.UiState.SecondaryMessageAnimTime = 0f;
        }
        public bool TryShowNextSecondaryMessage()
        {
            if (plugin.UiState.SecondaryMessage != null && plugin.UiState.SecondaryMessageAnimTime >= 0f)
            {
                return false;
            }

            if (plugin.UiState.SecondaryMessageQueue.Count == 0)
            {
                return false;
            }

            plugin.UiState.SecondaryMessage = plugin.UiState.SecondaryMessageQueue.Dequeue();
            plugin.UiState.SecondaryMessageAnimTime = 0f;
            return true;
        }
        public void QueueConveyorMessage(
            string message,
            bool showFireworks = false,
            UIState.OverlayMessageCompletionAction completionAction = UIState.OverlayMessageCompletionAction.None)
        {
            plugin.UiState.OverlayMessageQueue.Enqueue(new UIState.OverlayMessage(
                message,
                showFireworks,
                completionAction,
                UIState.MessageIntent.Conveyor));
        }
        public void QueueConveyorMessage(UIState.OverlayMessage message)
        {
            plugin.UiState.OverlayMessageQueue.Enqueue(message);
        }
        public void GrowPromptIfHidden()
        {
            bool isCollapsed = plugin.UiState.PromptState == UIState.PromptAnimState.Hidden
                               || plugin.UiState.PromptScale <= 0.01f;
            if (!isCollapsed)
            {
                return;
            }

            plugin.UiState.PromptState = UIState.PromptAnimState.Growing;
            plugin.UiState.PromptScale = 0.0f;
            plugin.UiState.PromptAnimTimer = 0f;
        }
        public void SetRightSideMessage(string newMessage)
        {
            if (plugin.UiState.CurrentRightSideMessage == newMessage) return;

            plugin.UiState.TargetRightSideMessage = newMessage;

            bool isDealerAccumulationTransition = plugin.GameState.ActiveMode == GameMode.Dealer
                                                  && plugin.GameState.ActivePhase == GamePhase.Accumulation
                                                  && (plugin.TurnManager.DealerTransitionTimer > 0f || plugin.TurnManager.DealerTransitionStarted);

            bool isThinkingMessage = newMessage.EndsWith("is thinking...");

            if (isDealerAccumulationTransition || string.IsNullOrEmpty(plugin.UiState.CurrentRightSideMessage) || isThinkingMessage)
            {
                plugin.UiState.CurrentRightSideMessage = newMessage;
                plugin.UiState.RightSideMessageScale = 0.0f;
                plugin.UiState.RightSideMessageState = UIState.RightSideAnimState.PoppingIn;
                plugin.UiState.RightSideMessageAnimTimer = 0f;
            }
            else
            {
                plugin.UiState.RightSideMessageState = UIState.RightSideAnimState.PoppingOut;
                plugin.UiState.RightSideMessageAnimTimer = 0f;
            }
        }
        public void AddPendingLogAnnouncement(string message, float delay)
        {
            plugin.TurnManager.PendingLogAnnouncements.Add(new TurnManager.PendingLogAnnouncement(message, delay));
        }
        public void TriggerWinLoseAnimation(
            string message,
            bool showFireworks = false,
            UIState.OverlayMessageCompletionAction completionAction = UIState.OverlayMessageCompletionAction.None)
        {
            float targetPos = 1.0f;
            if (plugin.UiState.ActiveOverlayMessages.Any(m => m.TargetPosition == 1.0f))
            {
                targetPos = 2.0f;
            }

            plugin.UiState.ActiveOverlayMessages.Add(new UIState.ActiveOverlayMessage
            {
                Text = message,
                ShowFireworks = showFireworks,
                Intent = UIState.MessageIntent.Conveyor,
                CompletionAction = completionAction,
                VisualPosition = 2.0f,
                TargetPosition = targetPos
            });

            if (showFireworks)
            {
                var center = plugin.OverlayWindow.GetConveyorAnchor();
                Vector2 textSize;
                using (plugin.LargeFont.Push())
                {
                    textSize = ImGui.CalcTextSize(message);
                }
                plugin.HandWindow.SpawnMessageFireworks(center, textSize, Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
            }

            if (message.StartsWith("Win!")
                || message.Contains("received", StringComparison.OrdinalIgnoreCase)
                || message.Contains("gave you", StringComparison.OrdinalIgnoreCase)
                || message.Contains("matched!", StringComparison.OrdinalIgnoreCase))
            {
                plugin.EventBus.PublishPlaySound(plugin.Configuration.WinSound);
            }
            else
            {
                plugin.EventBus.PublishPlaySound(plugin.Configuration.LoseSound);
            }
        }
        public void OnMessageFinished(UIState.ActiveOverlayMessage finishedMessage)
        {
            if (finishedMessage.CompletionAction == UIState.OverlayMessageCompletionAction.StartBusRide)
            {
                if (finishedMessage.CompletionActionHandled) return;
                finishedMessage.CompletionActionHandled = true;
                ClearDealerPhaseChangePrompt();
                plugin.UiState.BusRideEndConfirmationPending = false;

                if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Networked)
                {
                    plugin.GameState.ActivePhase = GamePhase.BusRide;
                }
                else
                {
                    plugin.RulesEngine.SetupBusRide(plugin.Configuration.BusSize);

                    plugin.TurnManager.NpcThinkingTimer = -1f;
                    plugin.TurnManager.DealerNpcHasGuessed = false;
                    plugin.TurnManager.DealerCurrentNpcGuess = -1;
                    plugin.TurnManager.PendingPlayerNpcOutcome = null;
                    plugin.TurnManager.PendingPlayerBusRideGuess = -1;
                    plugin.TurnManager.PlayerBusRideResultTimer = -1f;

                    if (plugin.GameState.ActiveMode == GameMode.Dealer)
                    {
                        plugin.UiState.CurrentRightSideMessage = string.Empty;
                        plugin.UiState.TargetRightSideMessage = string.Empty;
                        plugin.UiState.RightSideMessageScale = 1.0f;
                        plugin.UiState.RightSideMessageState = UIState.RightSideAnimState.Normal;
                        plugin.UiState.PromptState = UIState.PromptAnimState.Normal;
                        plugin.UiState.PromptScale = 1.0f;
                        plugin.UiState.PromptAnimTimer = 0f;
                    }
                    else
                    {
                        plugin.UiState.CurrentRightSideMessage = "Dealer deals...";
                        plugin.UiState.TargetRightSideMessage = plugin.UiState.CurrentRightSideMessage;
                        plugin.UiState.RightSideMessageScale = 1.0f;
                        plugin.UiState.RightSideMessageState = UIState.RightSideAnimState.Normal;
                        plugin.UiState.PromptState = UIState.PromptAnimState.Hidden;
                        plugin.UiState.PromptScale = 0.0f;
                        plugin.UiState.PromptAnimTimer = 0f;
                    }
                }
                plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
            }
        }
    }
}

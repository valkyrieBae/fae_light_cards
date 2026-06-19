using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class GameCoordinatorService
    {
        private readonly Plugin plugin;

        public GameCoordinatorService(Plugin plugin)
        {
            this.plugin = plugin;
            plugin.EventBus.OverlayMessageTriggered += QueueConveyorMessage;
            plugin.EventBus.SecondaryMessageRequested += ShowSecondaryMessage;
            plugin.EventBus.RightSideMessageRequested += SetRightSideMessage;
        }

        public bool HasBlockingAiProgressionVisuals(bool includePromptTransitions = true)
        {
            bool hasPromptTransition = includePromptTransitions
                                       && (plugin.UiState.PromptState == UIState.PromptAnimState.ButtonClick
                                           || plugin.UiState.PromptState == UIState.PromptAnimState.Shrinking
                                           || plugin.UiState.PromptState == UIState.PromptAnimState.Growing);

            return plugin.HandWindow.HasActiveAnimations
                   || plugin.UiState.ActiveOverlayMessages.Count > 0
                   || plugin.UiState.OverlayMessageQueue.Count > 0
                   || plugin.UiState.PendingWinLoseMessage != null
                   || plugin.UiState.SecondaryMessage != null
                   || plugin.UiState.SecondaryMessageQueue.Count > 0
                   || hasPromptTransition
                   || plugin.GameState.HasPendingDrinkTarget
                   || plugin.GameState.PendingLocalMatchSlotIndex != -1
                   || plugin.RulesEngine.HasPendingScionMatches;
        }

        public void Update(float dt)
        {
            ValidateDealerPhaseChangePrompt();
            EnsureDealerPhaseChangePrompt();

            if (plugin.GameState.ActivePhase != GamePhase.BusRide)
            {
                plugin.UiState.BusRideEndConfirmationPending = false;
            }

            // Process deferred network phase transitions
            if (plugin.TurnManager.DeferredNetworkPhase.HasValue)
            {
                if (!plugin.IsAnimationPlaying && plugin.UiState.OverlayMessageQueue.Count == 0)
                {
                    plugin.GameState.ActivePhase = plugin.TurnManager.DeferredNetworkPhase.Value;
                    plugin.TurnManager.DeferredNetworkPhase = null;
                }
            }

            UpdatePendingLogAnnouncements(dt);


            if (plugin.GameState.PendingPlaySlotIndex.HasValue)
            {
                plugin.RulesEngine.PlayHandCard(plugin.GameState.PendingPlaySlotIndex.Value);
                plugin.GameState.PendingPlaySlotIndex = null;
            }

            // Hand transition timer
            if (plugin.TurnManager.HandTransitionTimer > 0f)
            {
                plugin.TurnManager.HandTransitionTimer -= dt;
                if (plugin.TurnManager.HandTransitionTimer <= 0f)
                {
                    plugin.TurnManager.HandTransitionTimer = 0f;
                    plugin.TurnManager.TransitionPrevHand.Clear();
                    plugin.TurnManager.TransitionNextHand.Clear();
                }
            }

            // Dealer phase transition
            if (plugin.TurnManager.DealerPhaseTransitionTimer > 0f)
            {
                if (!plugin.GameState.HasPendingDrinkTarget)
                {
                    plugin.TurnManager.DealerPhaseTransitionTimer -= dt;
                    if (plugin.TurnManager.DealerPhaseTransitionTimer <= 0f)
                    {
                        plugin.TurnManager.DealerPhaseTransitionTimer = 0f;
                        if (plugin.TurnManager.DealerNextPhasePending == GamePhase.Pyramid)
                        {
                            plugin.GameState.ActivePhase = GamePhase.Pyramid;
                            plugin.RulesEngine.SetupPyramid();
                            plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
                            plugin.UiState.PromptScale = 1.0f;
                            plugin.UiState.PromptState = UIState.PromptAnimState.Normal;
                            plugin.TurnManager.DealerNpcHasGuessed = false;
                            plugin.TurnManager.DealerCurrentNpcGuess = -1;
                        }
                        plugin.TurnManager.DealerNextPhasePending = null;
                    }
                }
            }

            // Pop-in/pop-out right side messages
            bool shouldAnimateRightSideMessage =
                plugin.GameState.ActivePhase == GamePhase.BusRide
                || (plugin.GameState.ActivePhase == GamePhase.Accumulation
                    && (plugin.GameState.ActiveMode == GameMode.Dealer
                        || (plugin.GameState.ActiveMode == GameMode.Player && plugin.TurnManager.PlayerNpcTurnsPending)));

            if (shouldAnimateRightSideMessage)
            {
                if (plugin.UiState.RightSideMessageState == UIState.RightSideAnimState.PoppingOut)
                {
                    plugin.UiState.RightSideMessageAnimTimer += dt;
                    float duration = 0.15f;
                    float t = Math.Clamp(plugin.UiState.RightSideMessageAnimTimer / duration, 0f, 1f);
                    plugin.UiState.RightSideMessageScale = 1.0f - t;
                    if (plugin.UiState.RightSideMessageAnimTimer >= duration)
                    {
                        plugin.UiState.CurrentRightSideMessage = plugin.UiState.TargetRightSideMessage;
                        plugin.UiState.RightSideMessageState = UIState.RightSideAnimState.PoppingIn;
                        plugin.UiState.RightSideMessageAnimTimer = 0f;
                    }
                }
                else if (plugin.UiState.RightSideMessageState == UIState.RightSideAnimState.PoppingIn)
                {
                    plugin.UiState.RightSideMessageAnimTimer += dt;
                    float duration = 0.15f;
                    float t = Math.Clamp(plugin.UiState.RightSideMessageAnimTimer / duration, 0f, 1f);
                    plugin.UiState.RightSideMessageScale = t;
                    if (plugin.UiState.RightSideMessageAnimTimer >= duration)
                    {
                        plugin.UiState.RightSideMessageState = UIState.RightSideAnimState.Normal;
                        plugin.UiState.RightSideMessageScale = 1.0f;
                        plugin.UiState.RightSideMessageAnimTimer = -1f;
                    }
                }

                if (plugin.GameState.ActiveMode == GameMode.Player && plugin.GameState.ActivePhase == GamePhase.BusRide)
                {
                    if (plugin.UiState.BusRideVictoryResetTimer > 0f)
                    {
                        plugin.UiState.BusRideVictoryResetTimer -= dt;
                        if (plugin.UiState.BusRideVictoryResetTimer <= 0f)
                        {
                            plugin.UiState.BusRideVictoryResetTimer = -1f;
                            plugin.ResetGame();
                        }
                    }

                    if (plugin.UiState.BusRideResetTimer > 0f)
                    {
                        plugin.UiState.BusRideResetTimer -= dt;
                        if (plugin.UiState.BusRideResetTimer <= 0f)
                        {
                            plugin.UiState.BusRideResetTimer = -1f;
                            var oldCard = plugin.GameState.BusRideCurrentCard;
                            if (oldCard != null)
                            {
                                plugin.HandWindow.TriggerBusRideSlideRight(oldCard);
                            }
                            plugin.GameState.BusRideCurrentCard = null;

                            var newCard = plugin.RulesEngine.DrawBusRideCard();
                            plugin.GameState.BusRideCurrentCard = newCard;
                            plugin.HandWindow.TriggerBusRideDeal(newCard);

                            var rider1 = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.BusRiderName);
                            bool isNpc1 = rider1 != null ? (!rider1.IsHuman && !rider1.IsLocal) : (plugin.GameState.BusRiderName != GameConstants.LocalPlayerName);
                            SetRightSideMessage(isNpc1 ? $"{plugin.GameState.BusRiderName} is thinking..." : string.Empty);

                            if (plugin.GameState.BusRiderName == GameConstants.LocalPlayerName)
                            {
                                GrowPromptIfHidden();
                            }
                        }
                    }

                    if (plugin.UiState.BusRidePromptGrowTimer > 0f)
                    {
                        plugin.UiState.BusRidePromptGrowTimer -= dt;
                        if (plugin.UiState.BusRidePromptGrowTimer <= 0f)
                        {
                            plugin.UiState.BusRidePromptGrowTimer = -1f;
                            var rider2 = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.BusRiderName);
                            bool isNpc2 = rider2 != null ? (!rider2.IsHuman && !rider2.IsLocal) : (plugin.GameState.BusRiderName != GameConstants.LocalPlayerName);
                            SetRightSideMessage(isNpc2 ? $"{plugin.GameState.BusRiderName} is thinking..." : string.Empty);

                            if (plugin.GameState.BusRiderName == GameConstants.LocalPlayerName)
                            {
                                GrowPromptIfHidden();
                            }
                        }
                    }
                }
                else if (plugin.GameState.ActiveMode == GameMode.Dealer && plugin.GameState.ActivePhase == GamePhase.Accumulation)
                {
                    if (plugin.TurnManager.DealerTransitionTimer > 0f)
                    {
                        plugin.TurnManager.DealerTransitionTimer -= dt;
                        if (plugin.TurnManager.DealerTransitionTimer <= 0f)
                        {
                            plugin.TurnManager.DealerTransitionTimer = 0f;

                            plugin.TurnManager.DealerNpcHasGuessed = false;
                            plugin.TurnManager.DealerCurrentNpcGuess = -1;
                            var activePlayer = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.DealerActivePlayerName);
                            bool isNpc = activePlayer != null ? (!activePlayer.IsHuman && !activePlayer.IsLocal) : true;
                            if (isNpc)
                            {
                                plugin.TurnManager.NpcThinkingTimer = UIConstants.AiThinkingBaseDuration; // Handled dynamically in OfflineController now, but fallback
                                SetRightSideMessage($"{plugin.GameState.DealerActivePlayerName} is thinking...");
                            }
                            else
                            {
                                plugin.TurnManager.NpcThinkingTimer = -1f;
                                SetRightSideMessage(string.Empty);
                            }
                            plugin.TurnManager.DealerTransitionStarted = false;

                            plugin.UiState.PromptState = UIState.PromptAnimState.Shrinking;
                            plugin.UiState.PromptAnimTimer = 0f;
                        }
                    }
                    else if (plugin.AppState.ResetToModeSelectionTimer > 0f)
                    {
                        plugin.AppState.ResetToModeSelectionTimer -= dt;
                        if (plugin.AppState.ResetToModeSelectionTimer <= 0f)
                        {
                            plugin.AppState.ResetToModeSelectionTimer = 0f;
                            plugin.ResetGame();
                        }
                    }
                }
                else if (plugin.GameState.ActiveMode == GameMode.Dealer && plugin.GameState.ActivePhase == GamePhase.BusRide)
                {
                    if (plugin.UiState.BusRideVictoryResetTimer > 0f)
                    {
                        plugin.UiState.BusRideVictoryResetTimer -= dt;
                        if (plugin.UiState.BusRideVictoryResetTimer <= 0f)
                        {
                            plugin.UiState.BusRideVictoryResetTimer = -1f;
                            plugin.ResetGame();
                        }
                    }

                    if (plugin.UiState.BusRideResetTimer > 0f)
                    {
                        plugin.UiState.BusRideResetTimer -= dt;
                        if (plugin.UiState.BusRideResetTimer <= 0f)
                        {
                            plugin.UiState.BusRideResetTimer = -1f;
                            var oldCard = plugin.GameState.BusRideCurrentCard;
                            if (oldCard != null)
                            {
                                plugin.HandWindow.TriggerBusRideSlideRight(oldCard);
                            }

                            plugin.GameState.BusRideCurrentCard = null;
                            plugin.TurnManager.DealerNpcHasGuessed = false;
                            plugin.TurnManager.DealerCurrentNpcGuess = -1;
                            plugin.TurnManager.NpcThinkingTimer = -1f;
                            SetRightSideMessage(string.Empty);

                            plugin.UiState.PromptState = UIState.PromptAnimState.Normal;
                            plugin.UiState.PromptScale = 1.0f;
                            plugin.UiState.PromptAnimTimer = 0f;
                        }
                    }

                    if (plugin.TurnManager.DealerTransitionTimer > 0f)
                    {
                        plugin.TurnManager.DealerTransitionTimer -= dt;
                        if (plugin.TurnManager.DealerTransitionTimer <= 0f)
                        {
                            plugin.TurnManager.DealerTransitionTimer = 0f;
                            if (plugin.UiState.BusRideVictoryResetTimer <= 0f)
                            {
                                plugin.TurnManager.DealerNpcHasGuessed = false;
                                plugin.TurnManager.DealerCurrentNpcGuess = -1;

                                if (plugin.GameState.BusRideCurrentCard == null)
                                {
                                    plugin.TurnManager.NpcThinkingTimer = -1f;
                                    SetRightSideMessage(string.Empty);
                                    plugin.UiState.PromptState = UIState.PromptAnimState.Normal;
                                    plugin.UiState.PromptScale = 1.0f;
                                    plugin.UiState.PromptAnimTimer = 0f;
                                }
                                else
                                {
                                    var activePlayer = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.BusRiderName);
                                    bool isNpc = activePlayer != null ? (!activePlayer.IsHuman && !activePlayer.IsLocal) : (plugin.GameState.BusRiderName != GameConstants.LocalPlayerName);
                                    if (isNpc)
                                    {
                                        plugin.TurnManager.NpcThinkingTimer = UIConstants.AiThinkingBaseDuration;
                                        SetRightSideMessage($"{plugin.GameState.BusRiderName} is thinking...");
                                    }
                                    else
                                    {
                                        plugin.TurnManager.NpcThinkingTimer = -1f;
                                        SetRightSideMessage(string.Empty);
                                    }

                                    plugin.UiState.PromptState = UIState.PromptAnimState.Shrinking;
                                    plugin.UiState.PromptAnimTimer = 0f;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

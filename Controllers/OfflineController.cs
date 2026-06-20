using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class OfflineController : IGameController
    {
        private readonly Plugin plugin;
        private readonly NpcAiService npcAi = new NpcAiService();
        private readonly Random rng = new Random();

        public OfflineController(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public bool HasPendingActions => plugin.RulesEngine.HasPendingScionMatches
                                         || plugin.GameState.HasPendingDrinkTarget
                                         || plugin.TurnManager.PendingPlayerNpcOutcome != null
                                         || plugin.TurnManager.PendingPlayerBusRideGuess >= 0;

        public void Update(float dt)
        {
            // NPC update ticks based on active mode and phase
            if (plugin.GameState.ActiveMode == GameMode.Player && plugin.GameState.ActivePhase == GamePhase.Accumulation && plugin.TurnManager.PlayerNpcTurnsPending)
            {
                UpdatePlayerModeNpcTurns(dt);
            }
            else if (plugin.GameState.ActiveMode == GameMode.Player && plugin.GameState.ActivePhase == GamePhase.Pyramid)
            {
                UpdatePlayerModePyramidDealer(dt);
            }
            else if (plugin.GameState.ActiveMode == GameMode.Player && plugin.GameState.ActivePhase == GamePhase.BusRide)
            {
                if (UpdatePendingPlayerBusRideGuess(dt))
                {
                    return;
                }

                if (plugin.GameCoordinator.HasBlockingAiProgressionVisuals())
                {
                    return;
                }

                if (plugin.GameState.BusRideCurrentCard == null && plugin.UiState.BusRideVictoryResetTimer <= 0f)
                {
                    // NPC Dealer thinking about dealing the first card
                    if (plugin.TurnManager.NpcThinkingTimer < 0f)
                    {
                        plugin.TurnManager.NpcThinkingTimer = npcAi.GetThinkingTime(UIConstants.AiThinkingBaseDuration, UIConstants.AiThinkingVariance);
                        plugin.EventBus.PublishRightSideMessage("Dealer deals...");
                    }
                    else
                    {
                        plugin.TurnManager.NpcThinkingTimer -= dt;
                        if (plugin.TurnManager.NpcThinkingTimer <= 0f)
                        {
                            plugin.TurnManager.NpcThinkingTimer = -1f;

                            var nextCard = plugin.RulesEngine.DrawBusRideCard();
                            plugin.GameState.BusRideCurrentCard = nextCard;
                            plugin.EventBus.PublishBusRideCardDealt(nextCard);
                            plugin.GameState.ActionLog.Add($"Dealer dealt initial card for {plugin.GameState.BusRiderName}: {nextCard}.");

                            var rider = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.BusRiderName);
                            bool isNpc = rider != null ? (!rider.IsHuman && !rider.IsLocal) : (plugin.GameState.BusRiderName != GameConstants.LocalPlayerName);
                            plugin.EventBus.PublishRightSideMessage(isNpc ? $"{plugin.GameState.BusRiderName} is thinking..." : string.Empty);

                            if (plugin.GameState.BusRiderName == GameConstants.LocalPlayerName)
                            {
                                plugin.EventBus.PublishPromptStateChange(UIState.PromptAnimState.Growing, 0.0f);
                            }
                            else
                            {
                                plugin.TurnManager.NpcThinkingTimer = npcAi.GetThinkingTime(UIConstants.AiThinkingBaseDuration, UIConstants.AiThinkingVariance);
                            }
                        }
                    }
                }
                // Simulate NPC bus rider choice in Player mode
                else if (plugin.GameState.BusRiderName != GameConstants.LocalPlayerName && plugin.UiState.BusRideVictoryResetTimer <= 0f && plugin.GameState.BusRideCurrentCard != null)
                {
                    if (plugin.UiState.BusRideResetTimer <= 0f && plugin.UiState.BusRidePromptGrowTimer <= 0f)
                    {
                        if (plugin.TurnManager.NpcThinkingTimer < 0f)
                        {
                            plugin.TurnManager.NpcThinkingTimer = npcAi.GetThinkingTime(UIConstants.AiThinkingBaseDuration, UIConstants.AiThinkingVariance);
                            plugin.EventBus.PublishRightSideMessage($"{plugin.GameState.BusRiderName} is thinking...");
                        }
                        else
                        {
                            plugin.TurnManager.NpcThinkingTimer -= dt;
                            if (plugin.TurnManager.NpcThinkingTimer <= 0f)
                            {
                                plugin.TurnManager.NpcThinkingTimer = -1f;
                                var currentCard = plugin.GameState.BusRideCurrentCard;
                                if (currentCard != null)
                                {
                                    int choice = npcAi.DetermineBusRideGuess(currentCard);
                                    string guessedStr = choice == 0 ? "higher" : "lower";
                                    plugin.EventBus.PublishRightSideMessage($"{plugin.GameState.BusRiderName} guessed {guessedStr}!");
                                    plugin.TurnManager.PendingPlayerBusRideGuess = choice;
                                    plugin.TurnManager.PlayerBusRideResultTimer = UIConstants.AiResultRevealDelay;
                                }
                            }
                        }
                    }
                }
            }
            else if (plugin.GameState.ActiveMode == GameMode.Dealer && plugin.GameState.ActivePhase == GamePhase.Accumulation)
            {
                if (plugin.GameCoordinator.HasBlockingAiProgressionVisuals())
                {
                    return;
                }

                // Simulating NPC guesses in Dealer Mode Accumulation phase
                if (!plugin.TurnManager.DealerNpcHasGuessed && plugin.TurnManager.DealerTransitionTimer <= 0f && plugin.AppState.ResetToModeSelectionTimer <= 0f && !plugin.TurnManager.DealerNeedNextPlayer)
                {
                    if (plugin.TurnManager.NpcThinkingTimer < 0f)
                    {
                        plugin.TurnManager.NpcThinkingTimer = npcAi.GetThinkingTime(UIConstants.AiThinkingBaseDuration, UIConstants.AiThinkingVariance);
                        plugin.EventBus.PublishRightSideMessage($"{plugin.GameState.DealerActivePlayerName} is thinking...");
                    }
                    else
                    {
                        plugin.TurnManager.NpcThinkingTimer -= dt;
                        if (plugin.TurnManager.NpcThinkingTimer <= 0f)
                        {
                            plugin.TurnManager.NpcThinkingTimer = -1f;
                            var activePlayer = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.DealerActivePlayerName);
                            if (activePlayer != null)
                            {
                                int round = activePlayer.Hand.Count;
                                var stage = plugin.RulesEngine.GuessingStages[round];

                                int choice = npcAi.DetermineAccumulationGuess(activePlayer, stage);

                                plugin.TurnManager.DealerCurrentNpcGuess = choice;
                                plugin.TurnManager.DealerNpcHasGuessed = true;
                                string optionLabel = stage.Options[choice].Label;
                                plugin.EventBus.PublishRightSideMessage($"{activePlayer.Name} guessed {optionLabel}!");

                                plugin.EventBus.PublishPromptStateChange(UIState.PromptAnimState.Growing, 0.0f);
                            }
                        }
                    }
                }
            }
            else if (plugin.GameState.ActiveMode == GameMode.Dealer && plugin.GameState.ActivePhase == GamePhase.BusRide)
            {
                if (plugin.GameCoordinator.HasBlockingAiProgressionVisuals())
                {
                    return;
                }

                // NPC bus rider guessing in Dealer mode
                if (!plugin.TurnManager.DealerNpcHasGuessed && plugin.UiState.BusRideVictoryResetTimer <= 0f && plugin.TurnManager.DealerTransitionTimer <= 0f && plugin.GameState.BusRideCurrentCard != null)
                {
                    if (plugin.TurnManager.NpcThinkingTimer < 0f)
                    {
                        plugin.TurnManager.NpcThinkingTimer = npcAi.GetThinkingTime(UIConstants.AiThinkingBaseDuration, UIConstants.AiThinkingVariance);
                        plugin.EventBus.PublishRightSideMessage($"{plugin.GameState.BusRiderName} is thinking...");
                    }
                    else
                    {
                        plugin.TurnManager.NpcThinkingTimer -= dt;
                        if (plugin.TurnManager.NpcThinkingTimer <= 0f)
                        {
                            plugin.TurnManager.NpcThinkingTimer = -1f;
                            var currentCard = plugin.GameState.BusRideCurrentCard;
                            if (currentCard != null)
                            {
                                int choice = npcAi.DetermineBusRideGuess(currentCard);

                                plugin.TurnManager.DealerCurrentNpcGuess = choice;
                                plugin.TurnManager.DealerNpcHasGuessed = true;

                                string guessedStr = choice == 0 ? "higher" : "lower";
                                plugin.EventBus.PublishRightSideMessage($"{plugin.GameState.BusRiderName} guessed {guessedStr}!");

                                plugin.EventBus.PublishPromptStateChange(UIState.PromptAnimState.Growing, 0.0f);
                            }
                        }
                    }
                }
            }

            // Tick Scion matching queue in Pyramid Phase
            if (plugin.GameState.ActivePhase == GamePhase.Pyramid)
            {
                UpdatePendingScionMatches();
            }
        }

        public void EndGame()
        {
            plugin.ResetGame();
        }

        public void RestartGame()
        {
            plugin.GameCoordinator.RestartLocalDealerGame();
        }

        public void DebugSkipToPyramid()
        {
            // Handled locally by Plugin.cs when in debug mode
        }

        public void DebugSkipToLastCard()
        {
            // Handled locally by Plugin.cs when in debug mode
        }

        public void ChooseBusRider(string playerName)
        {
            plugin.ChooseBusRider(playerName);
        }

        public void Dispose()
        {
            // Nothing to clean up in Debug (offline) mode
            GC.SuppressFinalize(this);
        }
    }
}

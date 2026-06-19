using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class OfflineController
    {
        public void HandleDealerDealBusCard()
        {
            if (plugin.GameState.ActiveMode != GameMode.Dealer || plugin.GameState.ActivePhase != GamePhase.BusRide) return;

            bool isFirstCard = plugin.GameState.BusRideCurrentCard == null;
            if (!isFirstCard && !plugin.TurnManager.DealerNpcHasGuessed) return;
            if (plugin.TurnManager.DealerTransitionTimer > 0f
                || plugin.UiState.BusRideResetTimer > 0f
                || plugin.UiState.BusRidePromptGrowTimer > 0f
                || plugin.UiState.BusRideVictoryResetTimer > 0f) return;

            var currentCard = plugin.GameState.BusRideCurrentCard;
            if (currentCard == null)
            {
                // Deal first card of the bus ride!
                var nextCard = plugin.RulesEngine.DrawBusRideCard();
                plugin.GameState.BusRideCurrentCard = nextCard;
                plugin.EventBus.PublishBusRideCardDealt(nextCard);

                var riderName = plugin.GameState.BusRiderName;
                plugin.GameState.ActionLog.Add($"Dealer dealt initial card for {riderName}: {nextCard}.");

                // Start NPC guesser timer if the rider is an NPC
                if (riderName != GameConstants.LocalPlayerName)
                {
                    var rider = plugin.GameState.Players.FirstOrDefault(p => p.Name == riderName);
                    bool isNpc = rider != null ? (!rider.IsHuman && !rider.IsLocal) : true;
                    if (isNpc)
                    {
                        plugin.TurnManager.DealerNpcHasGuessed = false;
                        plugin.TurnManager.DealerCurrentNpcGuess = -1;
                        plugin.TurnManager.NpcThinkingTimer = npcAi.GetThinkingTime(UIConstants.AiThinkingBaseDuration, UIConstants.AiThinkingVariance);
                        plugin.EventBus.PublishRightSideMessage($"{riderName} is thinking...");
                    }
                    else
                    {
                        plugin.TurnManager.DealerNpcHasGuessed = false;
                        plugin.TurnManager.DealerCurrentNpcGuess = -1;
                        plugin.TurnManager.NpcThinkingTimer = -1f;
                        plugin.EventBus.PublishRightSideMessage(string.Empty);
                    }
                }
                else
                {
                    plugin.EventBus.PublishRightSideMessage(string.Empty);
                    plugin.EventBus.PublishPromptStateChange(UIState.PromptAnimState.Growing, 0.0f);
                }
                return;
            }

            var nextCard2 = plugin.RulesEngine.DrawBusRideCard();

            bool won = plugin.TurnManager.DealerCurrentNpcGuess == 0
                ? nextCard2.Rank > currentCard.Rank
                : nextCard2.Rank < currentCard.Rank;

            var previousCard = currentCard;
            plugin.GameState.BusRideCurrentCard = nextCard2;

            plugin.EventBus.PublishBusRideSlideDown(previousCard);
            plugin.EventBus.PublishBusRideCardDealt(nextCard2);

            var riderName2 = plugin.GameState.BusRiderName;
            var player = plugin.GameState.Players.FirstOrDefault(p => p.Name == riderName2);
            string guessedStr = plugin.TurnManager.DealerCurrentNpcGuess == 0 ? "higher" : "lower";

            if (won)
            {
                plugin.GameState.BusRideCorrectStreak++;
                plugin.GameState.ActionLog.Add($"{riderName2} guessed {guessedStr}. Correct! Guess {plugin.GameState.BusRideCorrectStreak}/{plugin.Configuration.BusSize}: {nextCard2} is {guessedStr} than {previousCard}.");

                if (plugin.GameState.BusRideCorrectStreak >= plugin.Configuration.BusSize)
                {
                    string victoryMsg = $"Victory! {riderName2} survived the bus!";
                    plugin.EventBus.PublishSecondaryMessage(victoryMsg);
                    plugin.EventBus.PublishRightSideMessage($"{riderName2} survived!");
                    plugin.UiState.BusRideVictoryResetTimer = 2.0f;
                    plugin.EventBus.PublishPlaySound(plugin.Configuration.WinSound);
                }
                else
                {
                    plugin.EventBus.PublishSecondaryMessage("Correct! Next!");
                    plugin.EventBus.PublishRightSideMessage($"{riderName2} guessed {guessedStr}!");
                    plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                }
            }
            else
            {
                plugin.GameState.BusRideCorrectStreak = 0;
                plugin.GameState.ActionLog.Add($"{riderName2} guessed {guessedStr}. Wrong! {nextCard2} is not {guessedStr} than {previousCard}. Resetting to 0/{plugin.Configuration.BusSize}.");

                string wrongMsg = $"{riderName2} drinks!";
                plugin.EventBus.PublishSecondaryMessage(wrongMsg);
                plugin.EventBus.PublishRightSideMessage($"{riderName2} guessed {guessedStr}! Wrong!");
                plugin.UiState.BusRideResetTimer = UIConstants.AiOutcomeHoldDuration;
                plugin.EventBus.PublishPlaySound(plugin.Configuration.LoseSound);
            }

            plugin.TurnManager.DealerNpcHasGuessed = false;
            plugin.TurnManager.DealerCurrentNpcGuess = -1;
            plugin.TurnManager.DealerTransitionTimer = UIConstants.AiOutcomeHoldDuration;
        }
        private bool UpdatePendingPlayerBusRideGuess(float dt)
        {
            if (plugin.TurnManager.PendingPlayerBusRideGuess < 0)
            {
                return false;
            }

            if (plugin.GameState.ActivePhase != GamePhase.BusRide || plugin.GameState.BusRideCurrentCard == null)
            {
                plugin.TurnManager.PendingPlayerBusRideGuess = -1;
                plugin.TurnManager.PlayerBusRideResultTimer = -1f;
                return false;
            }

            if (plugin.GameCoordinator.HasBlockingAiProgressionVisuals())
            {
                return true;
            }

            plugin.TurnManager.PlayerBusRideResultTimer -= dt;
            if (plugin.TurnManager.PlayerBusRideResultTimer > 0f)
            {
                return true;
            }

            int pendingGuess = plugin.TurnManager.PendingPlayerBusRideGuess;
            plugin.TurnManager.PendingPlayerBusRideGuess = -1;
            plugin.TurnManager.PlayerBusRideResultTimer = -1f;

            HandlePlayerGuess(pendingGuess);
            return true;
        }
    }
}

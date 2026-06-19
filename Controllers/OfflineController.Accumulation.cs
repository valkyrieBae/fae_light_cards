using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class OfflineController
    {
        public void HandlePlayerGuess(int pickedOptionIndex)
        {
            if (plugin.GameState.ActivePhase == GamePhase.Accumulation)
            {
                if (plugin.GameState.HasPendingDrinkTarget) return;

                var stage = plugin.RulesEngine.GetCurrentStage();
                if (stage == null) return;

                plugin.UiState.CachedPromptText = stage.PromptText;
                plugin.UiState.CachedOptions = new List<GuessOption>(stage.Options);

                var hand = plugin.GameState.GetRequiredDisplayedHand(nameof(HandlePlayerGuess));
                var handCopy = new List<Card>(hand);

                int oldCount = hand.Count;
                plugin.RulesEngine.DrawCard();
                int newCount = hand.Count;

                if (newCount > oldCount)
                {
                    var newCard = hand[newCount - 1];
                    plugin.EventBus.PublishCardDealt(newCard, newCount - 1);

                    bool won = stage.EvaluateGuess(newCard, pickedOptionIndex, handCopy);
                    int drinks = stage.DrinksCount;
                    string optionLabel = stage.Options[pickedOptionIndex].Label;
                    var localPlayer = plugin.GameState.Players.FirstOrDefault(p => p.IsLocal);
                    if (localPlayer != null)
                    {
                        if (won)
                        {
                            bool hasTargetChoice = SetPendingDrinkTarget(localPlayer, drinks);
                            string winDetail = hasTargetChoice
                                ? $"Choose who receives {drinks} drink{(drinks == 1 ? "" : "s")}."
                                : "No valid drink target was available.";
                            plugin.GameState.ActionLog.Add($"You guessed {optionLabel}. Correct! Dealt {newCard}. {winDetail}");
                        }
                        else
                        {
                            localPlayer.DrinksTaken += drinks;
                            plugin.GameState.ActionLog.Add($"You guessed {optionLabel}. Wrong! Dealt {newCard}. You take {drinks} drink{(drinks == 1 ? "" : "s")}.");
                        }
                    }

                    string drinksText = drinks == 1 ? "1 drink" : $"{drinks} drinks";
                    string winLoseText = won
                        ? $"Win! Give {drinksText}!"
                        : $"Lose! Take {drinksText}!";

                    plugin.EventBus.PublishSecondaryMessage(winLoseText);
                    plugin.EventBus.PublishPlaySound(won ? plugin.Configuration.WinSound : plugin.Configuration.LoseSound);

                    plugin.UiState.ClickedButtonIndex = pickedOptionIndex;
                    plugin.EventBus.PublishPromptStateChange(UIState.PromptAnimState.ButtonClick, 1.0f);
                    BeginPlayerModeNpcTurns(oldCount);
                }
            }
            else if (plugin.GameState.ActivePhase == GamePhase.BusRide)
            {
                if (plugin.GameState.BusRiderName == GameConstants.LocalPlayerName)
                {
                    var stage = plugin.RulesEngine.GetCurrentStage();
                    if (stage != null)
                    {
                        plugin.UiState.CachedPromptText = stage.PromptText;
                        plugin.UiState.CachedOptions = new List<GuessOption>(stage.Options);
                    }
                }

                var currentCard = plugin.GameState.BusRideCurrentCard;
                if (currentCard == null) return;

                var nextCard = plugin.RulesEngine.DrawBusRideCard();

                bool won = pickedOptionIndex == 0
                    ? nextCard.Rank > currentCard.Rank
                    : nextCard.Rank < currentCard.Rank;

                var previousCard = currentCard;
                plugin.GameState.BusRideCurrentCard = nextCard;

                plugin.EventBus.PublishBusRideSlideDown(previousCard);
                plugin.EventBus.PublishBusRideCardDealt(nextCard);

                string riderName = plugin.GameState.BusRiderName;
                string guessedStr = pickedOptionIndex == 0 ? "higher" : "lower";

                if (won)
                {
                    plugin.GameState.BusRideCorrectStreak++;
                    plugin.GameState.ActionLog.Add($"{riderName} guessed {guessedStr}. Correct! Guess {plugin.GameState.BusRideCorrectStreak}/{plugin.Configuration.BusSize}: {nextCard} is {guessedStr} than {previousCard}.");

                    if (plugin.GameState.BusRideCorrectStreak >= plugin.Configuration.BusSize)
                    {
                        string victoryMsg = riderName == GameConstants.LocalPlayerName ? "Victory! You survived the bus!" : $"Victory! {riderName} survived the bus!";
                        plugin.EventBus.PublishSecondaryMessage(victoryMsg);
                        plugin.EventBus.PublishRightSideMessage(riderName == GameConstants.LocalPlayerName ? "You survived!" : $"{riderName} survived!");
                        plugin.UiState.BusRideVictoryResetTimer = 2.0f;
                        plugin.EventBus.PublishPlaySound(plugin.Configuration.WinSound);
                    }
                    else
                    {
                        plugin.EventBus.PublishSecondaryMessage("Correct! Next!");
                        plugin.EventBus.PublishRightSideMessage(riderName == GameConstants.LocalPlayerName ? $"You guessed {guessedStr}! Correct!" : $"{riderName} guessed {guessedStr}! Correct!");
                        plugin.UiState.BusRidePromptGrowTimer = UIConstants.AiOutcomeHoldDuration;
                    }
                }
                else
                {
                    plugin.GameState.BusRideCorrectStreak = 0;
                    plugin.GameState.ActionLog.Add($"{riderName} guessed {guessedStr}. Wrong! {nextCard} is not {guessedStr} than {previousCard}. Resetting to 0/{plugin.Configuration.BusSize}.");

                    string wrongMsg = riderName == GameConstants.LocalPlayerName ? "Wrong! Drink and go again!" : $"Wrong! {riderName} drinks!";
                    plugin.EventBus.PublishSecondaryMessage(wrongMsg);
                    plugin.EventBus.PublishRightSideMessage(riderName == GameConstants.LocalPlayerName ? $"You guessed {guessedStr}! Wrong!" : $"{riderName} guessed {guessedStr}! Wrong!");
                    plugin.UiState.BusRideResetTimer = UIConstants.AiOutcomeHoldDuration;
                }

                if (riderName == GameConstants.LocalPlayerName)
                {
                    plugin.UiState.ClickedButtonIndex = pickedOptionIndex;
                    plugin.EventBus.PublishPromptStateChange(UIState.PromptAnimState.ButtonClick, 1.0f);
                }
            }
        }
        public void HandleDealerDeal()
        {
            if (plugin.GameState.ActiveMode != GameMode.Dealer || plugin.GameState.ActivePhase != GamePhase.Accumulation) return;
            if (plugin.GameState.HasPendingDrinkTarget) return;
            if (!plugin.TurnManager.DealerNpcHasGuessed || plugin.TurnManager.DealerTransitionTimer > 0f || plugin.TurnManager.DealerNeedNextPlayer) return;

            var activePlayer = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.DealerActivePlayerName);
            if (activePlayer == null) return;

            int round = activePlayer.Hand.Count;
            if (round >= plugin.RulesEngine.GuessingStages.Length) return;

            var stage = plugin.RulesEngine.GuessingStages[round];
            var dealtCard = plugin.RulesEngine.DrawFromDeck();
            bool won = stage.EvaluateGuess(dealtCard, plugin.TurnManager.DealerCurrentNpcGuess, activePlayer.Hand);

            plugin.EventBus.PublishCardDealt(dealtCard, activePlayer.Hand.Count);

            activePlayer.Hand.Add(dealtCard);

            int drinks = stage.DrinksCount;
            string optionLabel = stage.Options[plugin.TurnManager.DealerCurrentNpcGuess].Label;

            if (won)
            {
                plugin.EventBus.PublishPlaySound(plugin.Configuration.WinSound);
                if (activePlayer.IsLocal && !activePlayer.IsDealer)
                {
                    bool hasTargetChoice = SetPendingDrinkTarget(activePlayer, drinks);
                    if (hasTargetChoice)
                    {
                        plugin.EventBus.PublishSecondaryMessage("Correct! You win!");
                        plugin.GameState.ActionLog.Add($"{activePlayer.Name} guessed {optionLabel}. Correct! Dealt {dealtCard}. Choose who receives {drinks} drink{(drinks == 1 ? "" : "s")}.");
                    }
                    else
                    {
                        plugin.EventBus.PublishSecondaryMessage("Correct! No one can take it!");
                        plugin.GameState.ActionLog.Add($"{activePlayer.Name} guessed {optionLabel}. Correct! Dealt {dealtCard}. No valid drink target was available.");
                    }
                }
                else
                {
                    var targetPlayer = SelectAutoDrinkTarget(activePlayer);
                    if (targetPlayer != null)
                    {
                        activePlayer.DrinksGiven += drinks;
                        targetPlayer.DrinksTaken += drinks;
                        plugin.EventBus.PublishSecondaryMessage($"{activePlayer.Name} gives {targetPlayer.Name} {drinks} drink{(drinks == 1 ? "" : "s")}!");
                        plugin.GameState.ActionLog.Add($"{activePlayer.Name} guessed {optionLabel}. Correct! Dealt {dealtCard}. {activePlayer.Name} gives {drinks} drink{(drinks == 1 ? "" : "s")} to {targetPlayer.Name}.");
                    }
                    else
                    {
                        plugin.EventBus.PublishSecondaryMessage($"{activePlayer.Name} gives {drinks} drink{(drinks == 1 ? "" : "s")}!");
                        plugin.GameState.ActionLog.Add($"{activePlayer.Name} guessed {optionLabel}. Correct! Dealt {dealtCard}.");
                    }
                }
            }
            else
            {
                plugin.EventBus.PublishPlaySound(plugin.Configuration.LoseSound);
                activePlayer.DrinksTaken += drinks;

                plugin.EventBus.PublishSecondaryMessage($"Wrong! {activePlayer.Name} takes {drinks} drink{(drinks == 1 ? "" : "s")}!");

                plugin.GameState.ActionLog.Add($"{activePlayer.Name} guessed {optionLabel}. Wrong! Dealt {dealtCard}. {activePlayer.Name} takes {drinks} drink{(drinks == 1 ? "" : "s")}.");
            }

            plugin.TurnManager.DealerTransitionTimer = 0f;
            plugin.TurnManager.DealerNeedNextPlayer = !plugin.GameState.HasPendingDrinkTarget;
            if (plugin.TurnManager.DealerNeedNextPlayer)
            {
                plugin.EventBus.PublishRightSideMessage(string.Empty);
                if (AreAllNonDealersFinishedAccumulation())
                {
                    plugin.GameCoordinator.ShowDealerPhaseChangePrompt(UIState.DealerPhaseChangePromptState.Phase1Complete);
                }
            }
        }
        public void HandleDealerAdvanceNextPlayer()
        {
            if (plugin.GameState.ActiveMode != GameMode.Dealer || plugin.GameState.ActivePhase != GamePhase.Accumulation) return;
            if (plugin.GameState.HasPendingDrinkTarget) return;
            if (!plugin.TurnManager.DealerNeedNextPlayer || plugin.TurnManager.DealerTransitionTimer > 0f) return;

            var nonDealers = plugin.GameState.Players.Where(p => !p.IsDealer).ToList();
            bool allPlayersFinishedAccumulation = nonDealers.Count > 0 && nonDealers.All(p => p.Hand.Count == 4);
            if (allPlayersFinishedAccumulation)
            {
                plugin.GameCoordinator.ClearDealerPhaseChangePrompt();
                plugin.GameCoordinator.QueueConveyorMessage("Phase 1 Complete!", true);
                plugin.TurnManager.DealerTransitionTimer = -1f;
                plugin.TurnManager.DealerPhaseTransitionTimer = 3.0f;
                plugin.TurnManager.DealerNextPhasePending = GamePhase.Pyramid;
                plugin.TurnManager.DealerNeedNextPlayer = false;
                plugin.TurnManager.DealerNpcHasGuessed = false;
                plugin.TurnManager.DealerCurrentNpcGuess = -1;
                plugin.UiState.PromptState = UIState.PromptAnimState.Hidden;
                plugin.UiState.PromptScale = 0.0f;
                plugin.UiState.PromptAnimTimer = 0f;
            }
            else
            {
                int nextPlayerIdx = (plugin.TurnManager.DealerCurrentPlayerIndex + 1) % plugin.GameState.Players.Count;
                var curPlayer = plugin.GameState.Players[plugin.TurnManager.DealerCurrentPlayerIndex];
                var nextPlayer = plugin.GameState.Players[nextPlayerIdx];

                plugin.TurnManager.TransitionPrevHand = new List<Card>(curPlayer.Hand);
                plugin.TurnManager.TransitionNextHand = new List<Card>(nextPlayer.Hand);

                plugin.TurnManager.DealerCurrentPlayerIndex = nextPlayerIdx;
                plugin.GameState.DealerActivePlayerName = nextPlayer.Name;

                plugin.TurnManager.HandTransitionTimer = plugin.TurnManager.HandTransitionDuration;
                plugin.TurnManager.DealerTransitionTimer = plugin.TurnManager.HandTransitionDuration;

                plugin.TurnManager.DealerNeedNextPlayer = false;
                plugin.TurnManager.DealerTransitionStarted = true;
            }
        }
    }
}

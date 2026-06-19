using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class OfflineController
    {
        private void BeginPlayerModeNpcTurns(int roundIndex)
        {
            if (plugin.GameState.ActiveMode != GameMode.Player || plugin.GameState.ActivePhase != GamePhase.Accumulation) return;
            if (roundIndex < 0 || roundIndex >= plugin.RulesEngine.GuessingStages.Length) return;

            plugin.TurnManager.PlayerNpcTurnsPending = true;
            plugin.TurnManager.PlayerNpcTurnIndex = 0;
            plugin.TurnManager.PlayerNpcTurnRound = roundIndex;
            plugin.TurnManager.NpcThinkingTimer = -1f;
            plugin.TurnManager.PlayerNpcOutcomeTimer = 0f;
            plugin.TurnManager.PendingPlayerNpcOutcome = null;
            plugin.TurnManager.PendingPlayerBusRideGuess = -1;
            plugin.TurnManager.PlayerBusRideResultTimer = -1f;
        }
        private void UpdatePlayerModePyramidDealer(float dt)
        {
            if (plugin.AppState.ActiveConnectionMode != ConnectionMode.LocalOnly) return;
            if (plugin.GameState.CurrentFlipIndex >= 15) return;

            if (!plugin.TurnManager.PyramidDealerHasStarted)
            {
                plugin.TurnManager.PyramidDealerHasStarted = true;
                plugin.TurnManager.PyramidDealerPaused = false;
                plugin.TurnManager.PyramidDealerTimer = UIConstants.PyramidDealerStepDelay;
            }

            if (plugin.GameCoordinator.HasBlockingAiProgressionVisuals())
            {
                plugin.TurnManager.PyramidDealerTimer = -1f;
                return;
            }

            if (plugin.TurnManager.PyramidDealerPaused)
            {
                return;
            }

            if (plugin.TurnManager.PyramidDealerTimer < 0f)
            {
                plugin.TurnManager.PyramidDealerTimer = UIConstants.PyramidDealerStepDelay;
                return;
            }

            plugin.TurnManager.PyramidDealerTimer -= dt;
            if (plugin.TurnManager.PyramidDealerTimer > 0f)
            {
                return;
            }

            plugin.TurnManager.PyramidDealerTimer = UIConstants.PyramidDealerStepDelay;

            if (plugin.RulesEngine.IsActiveRowFullyFlipped() && plugin.RulesEngine.HasNextRow())
            {
                plugin.GameController.HandleAdvancePyramidRow();
                return;
            }

            int flipIndex = plugin.GameState.CurrentFlipIndex;
            if (flipIndex < 0 || flipIndex >= 15) return;
            if (RulesEngine.GetRowIndex(flipIndex) != plugin.GameState.ActiveRow) return;

            plugin.GameController.HandleFlipPyramidCard();
        }
        private void UpdatePlayerModeNpcTurns(float dt)
        {
            if (plugin.GameState.HasPendingDrinkTarget) return;

            if (UpdatePendingPlayerNpcOutcome(dt))
            {
                return;
            }

            if (plugin.TurnManager.PlayerNpcOutcomeTimer > 0f)
            {
                plugin.TurnManager.PlayerNpcOutcomeTimer -= dt;
                plugin.TurnManager.PlayerNpcOutcomeTimer = Math.Max(0f, plugin.TurnManager.PlayerNpcOutcomeTimer);
            }

            if (plugin.GameCoordinator.HasBlockingAiProgressionVisuals()) return;
            if (plugin.TurnManager.PlayerNpcOutcomeTimer > 0f) return;

            var npcs = plugin.GameState.Players.Where(p => !p.IsLocal && !p.IsDealer).ToList();
            int roundIndex = plugin.TurnManager.PlayerNpcTurnRound;

            while (plugin.TurnManager.PlayerNpcTurnIndex < npcs.Count && npcs[plugin.TurnManager.PlayerNpcTurnIndex].Hand.Count > roundIndex)
            {
                plugin.TurnManager.PlayerNpcTurnIndex++;
            }

            if (roundIndex < 0 || roundIndex >= plugin.RulesEngine.GuessingStages.Length || plugin.TurnManager.PlayerNpcTurnIndex >= npcs.Count)
            {
                CompletePlayerModeNpcTurns();
                return;
            }

            var npc = npcs[plugin.TurnManager.PlayerNpcTurnIndex];
            int npcRoundIndex = npc.Hand.Count;
            if (npcRoundIndex >= plugin.RulesEngine.GuessingStages.Length)
            {
                plugin.TurnManager.PlayerNpcTurnIndex++;
                return;
            }

            if (plugin.TurnManager.NpcThinkingTimer < 0f)
            {
                plugin.TurnManager.NpcThinkingTimer = npcAi.GetThinkingTime(UIConstants.AiThinkingBaseDuration, UIConstants.AiThinkingVariance);
                plugin.EventBus.PublishRightSideMessage($"{npc.Name} is thinking...");
                return;
            }

            plugin.TurnManager.NpcThinkingTimer -= dt;
            if (plugin.TurnManager.NpcThinkingTimer > 0f)
            {
                return;
            }

            plugin.TurnManager.NpcThinkingTimer = -1f;
            ResolvePlayerModeNpcTurn(npc, plugin.RulesEngine.GuessingStages[npcRoundIndex]);
            if (npc.Hand.Count > roundIndex)
            {
                plugin.TurnManager.PlayerNpcTurnIndex++;
            }
            plugin.TurnManager.PlayerNpcOutcomeTimer = UIConstants.AiOutcomeHoldDuration;
        }
        private bool UpdatePendingPlayerNpcOutcome(float dt)
        {
            var pending = plugin.TurnManager.PendingPlayerNpcOutcome;
            if (pending == null)
            {
                return false;
            }

            if (plugin.GameCoordinator.HasBlockingAiProgressionVisuals())
            {
                return true;
            }

            pending.RevealTimer -= dt;
            if (pending.RevealTimer > 0f)
            {
                return true;
            }

            RevealPendingPlayerNpcOutcome(pending);
            plugin.TurnManager.PendingPlayerNpcOutcome = null;
            plugin.TurnManager.PlayerNpcOutcomeTimer = UIConstants.AiOutcomeHoldDuration;
            return true;
        }
        private void RevealPendingPlayerNpcOutcome(TurnManager.PendingPlayerNpcOutcomeState pending)
        {
            if (pending.Won)
            {
                if (pending.Target != null)
                {
                    pending.Npc.DrinksGiven += pending.Drinks;
                    pending.Target.DrinksTaken += pending.Drinks;
                }
            }
            else
            {
                pending.Npc.DrinksTaken += pending.Drinks;
            }

            plugin.EventBus.PublishSecondaryMessage(pending.SecondaryMessage);
            plugin.EventBus.PublishPlaySound(pending.SoundFile);
            plugin.GameState.ActionLog.Add(pending.ActionLogMessage);
        }
        private void ResolvePlayerModeNpcTurn(Player npc, IGuessingStage stage)
        {
            var handCopy = new List<Card>(npc.Hand);
            int guess = npcAi.DetermineAccumulationGuess(npc, stage);
            Card dealtCard = DrawCardForPlayer(npc);
            bool won = stage.EvaluateGuess(dealtCard, guess, handCopy);
            int drinks = stage.DrinksCount;
            string optionLabel = stage.Options[guess].Label;

            plugin.EventBus.PublishRightSideMessage($"{npc.Name} guessed {optionLabel}!");

            if (won)
            {
                var targetPlayer = SelectAutoDrinkTarget(npc);

                string targetText = targetPlayer == null
                    ? "No valid drink target."
                    : targetPlayer.IsLocal
                        ? $"Gives you {drinks} drink{(drinks == 1 ? "" : "s")}."
                        : $"Gives {targetPlayer.Name} {drinks} drink{(drinks == 1 ? "" : "s")}.";
                string outcomeText = targetPlayer == null
                    ? $"{npc.Name} wins! No one can take it!"
                    : targetPlayer.IsLocal
                        ? $"{npc.Name} gives you {drinks} drink{(drinks == 1 ? "" : "s")}!"
                        : $"{npc.Name} gives {targetPlayer.Name} {drinks} drink{(drinks == 1 ? "" : "s")}!";
                plugin.TurnManager.PendingPlayerNpcOutcome = new TurnManager.PendingPlayerNpcOutcomeState(
                    npc,
                    targetPlayer,
                    drinks,
                    true,
                    outcomeText,
                    plugin.Configuration.WinSound,
                    $"{npc.Name} guessed {optionLabel}. Correct! Dealt {dealtCard}. {targetText}",
                    UIConstants.AiResultRevealDelay);
            }
            else
            {
                plugin.TurnManager.PendingPlayerNpcOutcome = new TurnManager.PendingPlayerNpcOutcomeState(
                    npc,
                    null,
                    drinks,
                    false,
                    $"{npc.Name} takes {drinks} drink{(drinks == 1 ? "" : "s")}!",
                    plugin.Configuration.LoseSound,
                    $"{npc.Name} guessed {optionLabel}. Wrong! Dealt {dealtCard}. {npc.Name} takes {drinks} drink{(drinks == 1 ? "" : "s")}.",
                    UIConstants.AiResultRevealDelay);
            }
        }
        private Card DrawCardForPlayer(Player player)
        {
            var newCard = plugin.RulesEngine.DrawFromDeck();
            player.Hand.Add(newCard);
            return newCard;
        }
        private void CompletePlayerModeNpcTurns()
        {
            plugin.TurnManager.PlayerNpcTurnsPending = false;
            plugin.TurnManager.PlayerNpcTurnIndex = 0;
            plugin.TurnManager.PlayerNpcTurnRound = -1;
            plugin.TurnManager.NpcThinkingTimer = -1f;
            plugin.TurnManager.PlayerNpcOutcomeTimer = 0f;
            plugin.TurnManager.PendingPlayerNpcOutcome = null;
            plugin.TurnManager.PendingPlayerBusRideGuess = -1;
            plugin.TurnManager.PlayerBusRideResultTimer = -1f;

            if (plugin.GameState.DisplayedHand.Count >= 4 && AreAllNonDealersFinishedAccumulation())
            {
                plugin.GameState.ActivePhase = GamePhase.Pyramid;
                plugin.RulesEngine.SetupPyramid();
                plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
            }
            else if (plugin.RulesEngine.GetCurrentStage() != null)
            {
                plugin.GameCoordinator.GrowPromptIfHidden();
            }
        }
        private bool AreAllNonDealersFinishedAccumulation()
        {
            var nonDealers = plugin.GameState.Players.Where(p => !p.IsDealer).ToList();
            return nonDealers.Count > 0 && nonDealers.All(p => p.Hand.Count >= 4);
        }
        public void AddNpcPlayer()
        {
            if (!plugin.IsLocalDealer || plugin.GameState.ActiveMode == GameMode.Undecided)
            {
                return;
            }

            if (plugin.GameState.Players.Count >= GameConstants.MaxPlayers)
            {
                plugin.EventBus.PublishSecondaryMessage("Player limit reached.");
                return;
            }

            string? npcName = GameConstants.ScionNames
                .Where(name => !plugin.GameState.Players.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(_ => rng.Next())
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(npcName))
            {
                plugin.EventBus.PublishSecondaryMessage("No unused NPCs remain.");
                return;
            }

            bool eligibleForBusRide = plugin.GameState.ActivePhase != GamePhase.TieChoice
                                      && plugin.GameState.ActivePhase != GamePhase.BusRide;
            var npc = new Player
            {
                Name = npcName,
                IsLocal = false,
                IsDealer = false,
                IsHuman = false,
                IsEligibleForCurrentBusRide = eligibleForBusRide
            };

            plugin.GameState.Players.Add(npc);
            DealStartingHandForLateNpc(npc);
            EnsureDealerActivePlayerAfterNpcAdd(npc);

            plugin.GameState.ActionLog.Add($"{npc.Name} joined as an NPC.");
            plugin.EventBus.PublishSecondaryMessage($"{npc.Name} joined the game.");
        }
        private void DealStartingHandForLateNpc(Player npc)
        {
            int targetHandCount = plugin.GameState.ActivePhase switch
            {
                GamePhase.Accumulation => GetAccumulationCatchUpHandCount(npc.Name),
                GamePhase.Pyramid => 4,
                _ => 0
            };

            while (npc.Hand.Count < targetHandCount)
            {
                npc.Hand.Add(plugin.RulesEngine.DrawFromDeck());
            }
        }
        private int GetAccumulationCatchUpHandCount(string npcName)
        {
            var nonDealers = plugin.GameState.Players
                .Where(p => !p.IsDealer
                            && p.IsEligibleForCurrentBusRide
                            && !string.Equals(p.Name, npcName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nonDealers.Count == 0)
            {
                return 0;
            }

            return Math.Min(4, nonDealers.Min(p => p.Hand.Count));
        }
        private void EnsureDealerActivePlayerAfterNpcAdd(Player npc)
        {
            if (plugin.GameState.ActiveMode != GameMode.Dealer || plugin.GameState.ActivePhase != GamePhase.Accumulation)
            {
                return;
            }

            bool hasActivePlayer = plugin.GameState.Players.Any(p => !p.IsDealer && p.Name == plugin.GameState.DealerActivePlayerName);
            if (!hasActivePlayer)
            {
                plugin.GameState.DealerActivePlayerName = npc.Name;
                plugin.TurnManager.DealerCurrentPlayerIndex = plugin.GameState.Players.IndexOf(npc);
                plugin.TurnManager.NpcThinkingTimer = -1f;
            }
            else if (plugin.TurnManager.DealerCurrentPlayerIndex < 0
                     || plugin.TurnManager.DealerCurrentPlayerIndex >= plugin.GameState.Players.Count)
            {
                plugin.TurnManager.DealerCurrentPlayerIndex = plugin.GameState.Players.FindIndex(p => p.Name == plugin.GameState.DealerActivePlayerName);
            }
        }
    }
}

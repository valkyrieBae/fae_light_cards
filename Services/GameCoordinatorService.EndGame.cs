using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class GameCoordinatorService
    {
        public void DebugSkipToPyramid()
        {
#if FAELIGHTCARDS_DEV_TOOLS
            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Networked)
            {
                plugin.GameController?.DebugSkipToPyramid();
                return;
            }

            var hand = plugin.GameState.GetRequiredDisplayedHand(nameof(DebugSkipToPyramid));
            while (hand.Count < 4)
            {
                plugin.RulesEngine.DrawCard();
            }

            plugin.GameState.ActivePhase = GamePhase.Pyramid;
            plugin.RulesEngine.SetupPyramid();
            plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);

            plugin.PromptWindow.ResetPromptState();
            plugin.HasTriggeredEndGame = false;
            plugin.GameController?.Dispose();
            plugin.GameController = new OfflineController(plugin);
            plugin.TurnManager.PendingLogAnnouncements.Clear();
            plugin.UiState.ActiveOverlayMessages.Clear();
            plugin.UiState.OverlayMessageQueue.Clear();
            plugin.UiState.SecondaryMessageQueue.Clear();
            plugin.UiState.SecondaryMessage = null;
            plugin.UiState.SecondaryMessageAnimTime = -1f;
            plugin.HandWindow.ClearAnimationsAndParticles();
#endif
        }
        public void DebugSkipToLastCard()
        {
#if FAELIGHTCARDS_DEV_TOOLS
            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Networked)
            {
                plugin.GameController?.DebugSkipToLastCard();
                return;
            }

            DebugSkipToPyramid();

            for (int i = 0; i < 14; i++)
            {
                plugin.GameState.PyramidFlipped[i] = true;
                plugin.GameState.PyramidRequiredMatchers[i].Clear();
            }

            plugin.GameState.CurrentFlipIndex = 14;
            plugin.GameState.ActiveRow = 1;

            plugin.PromptWindow.ResetPromptState();
            plugin.HasTriggeredEndGame = false;
            plugin.GameController?.Dispose();
            plugin.GameController = new OfflineController(plugin);
            plugin.TurnManager.PendingLogAnnouncements.Clear();
            plugin.UiState.ActiveOverlayMessages.Clear();
            plugin.UiState.OverlayMessageQueue.Clear();
            plugin.UiState.SecondaryMessageQueue.Clear();
            plugin.UiState.SecondaryMessage = null;
            plugin.UiState.SecondaryMessageAnimTime = -1f;
            plugin.HandWindow.ClearAnimationsAndParticles();

            plugin.GameState.ActionLog.Add("Debug: Skipped to the last card of the pyramid.");
#endif
        }
        public void ChooseBusRider(string playerName)
        {
            ClearDealerPhaseChangePrompt();

            var chosenPlayer = plugin.GameState.Players.FirstOrDefault(p => p.Name == playerName);
            if (chosenPlayer == null || chosenPlayer.IsDealer || !chosenPlayer.IsEligibleForCurrentBusRide)
            {
                return;
            }

            var eligiblePlayers = plugin.GameState.Players
                .Where(p => !p.IsDealer && p.IsEligibleForCurrentBusRide)
                .ToList();
            if (eligiblePlayers.Count == 0)
            {
                return;
            }

            int maxCards = eligiblePlayers.Max(p => p.Hand.Count);
            if (chosenPlayer.Hand.Count != maxCards)
            {
                return;
            }

            plugin.GameState.BusRiderName = playerName;
            var localName = plugin.GameState.Players.FirstOrDefault(p => p.IsLocal)?.Name;
            bool isLocal = playerName == GameConstants.LocalPlayerName || (localName != null && string.Equals(playerName, localName, StringComparison.OrdinalIgnoreCase));

            string displayName = isLocal ? GameConstants.LocalPlayerName : playerName;
            string wasWere = isLocal ? "were" : "was";

            QueueConveyorMessage(
                $"{displayName} {wasWere} chosen and must Ride the Bus!",
                completionAction: UIState.OverlayMessageCompletionAction.StartBusRide);
            plugin.GameState.ActionLog.Add($"{displayName} {wasWere} chosen to Ride the Bus.");
            plugin.GameState.ActivePhase = GamePhase.TieChoice;
        }
        public void CheckEndGame()
        {
            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Networked) return;
            if (plugin.GameState.ActivePhase != GamePhase.Pyramid || plugin.GameState.CurrentFlipIndex < 15) return;
            if (plugin.HasTriggeredEndGame) return;
            if (HasBlockingAiProgressionVisuals() || plugin.GameController.HasPendingActions || plugin.TurnManager.PendingLogAnnouncements.Count > 0) return;

            var eligiblePlayers = plugin.GameState.Players
                .Where(p => !p.IsDealer && p.IsEligibleForCurrentBusRide)
                .ToList();
            if (eligiblePlayers.Count == 0) return;

            var playersOrdered = eligiblePlayers.OrderByDescending(p => p.Hand.Count).ToList();
            int maxCards = playersOrdered[0].Hand.Count;
            var losers = playersOrdered.Where(p => p.Hand.Count == maxCards).ToList();

            if (plugin.GameState.ActiveMode == GameMode.Dealer && losers.Count >= 1)
            {
                if (IsLocalOnlyDealer())
                {
                    plugin.HasTriggeredEndGame = true;
                    ShowDealerPhaseChangePrompt(UIState.DealerPhaseChangePromptState.Phase2Complete);
                    plugin.GameState.ActionLog.Add("Pyramid complete. Choose whether to Ride the Bus or end the game.");
                    return;
                }

                plugin.GameState.ActivePhase = GamePhase.TieChoice;
                GrowPromptIfHidden();

                if (losers.Count > 1)
                {
                    plugin.GameState.ActionLog.Add("It's a tie! Choose a player from the player list to Ride the Bus.");
                }
                else
                {
                    string dealerDisplay = losers[0].IsLocal ? GameConstants.LocalPlayerName : losers[0].Name;
                    plugin.GameState.ActionLog.Add($"It's time to ride the bus! {dealerDisplay} must Ride the Bus.");
                }
                return;
            }

            plugin.HasTriggeredEndGame = true;

            Player loser;
            if (plugin.Configuration.BusRiderRigType == BusRiderRigType.PlayerRigged)
            {
                loser = eligiblePlayers.FirstOrDefault(p => p.IsHuman || p.IsLocal) ?? losers[0];
            }
            else if (plugin.Configuration.BusRiderRigType == BusRiderRigType.NpcRigged)
            {
                var npcs = eligiblePlayers.Where(p => !p.IsHuman && !p.IsLocal).ToList();
                if (npcs.Count > 0)
                {
                    loser = npcs[Random.Shared.Next(npcs.Count)];
                }
                else
                {
                    loser = losers[0];
                }
            }
            else
            {
                loser = losers[Random.Shared.Next(losers.Count)];
            }

            plugin.GameState.BusRiderName = loser.Name;

            bool wasTied = losers.Count > 1 && losers.Contains(loser);
            string displayName = loser.IsLocal ? GameConstants.LocalPlayerName : loser.Name;
            string wasWere = loser.IsLocal ? "were" : "was";

            if (wasTied)
            {
                QueueConveyorMessage($"Tie! {maxCards} cards left!");
                QueueConveyorMessage(
                    $"{displayName} {wasWere} picked at random and must Ride the Bus!",
                    completionAction: UIState.OverlayMessageCompletionAction.StartBusRide);
            }
            else
            {
                QueueConveyorMessage(
                    $"{displayName} must Ride the Bus!",
                    completionAction: UIState.OverlayMessageCompletionAction.StartBusRide);
            }

            string hasHave = loser.IsLocal ? "have" : "has";
            plugin.GameState.ActionLog.Add($"Game Over! {displayName} {hasHave} {loser.Hand.Count} cards left and must Ride the Bus.");
        }
    }
}

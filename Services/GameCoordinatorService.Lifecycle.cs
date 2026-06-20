using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class GameCoordinatorService
    {
        public void Dispose()
        {
            plugin.EventBus.OverlayMessageTriggered -= QueueConveyorMessage;
            plugin.EventBus.SecondaryMessageRequested -= ShowSecondaryMessage;
            plugin.EventBus.RightSideMessageRequested -= SetRightSideMessage;
        }
        public void ResetGame()
        {
            plugin.GameState.Reset();
            plugin.HandWindow.ClearAnimationsAndParticles();
            plugin.PromptWindow.ResetPromptState();
            plugin.HasTriggeredEndGame = false;
            plugin.GameController?.Dispose();
            plugin.AppState.ActiveConnectionMode = ConnectionMode.Undecided;
            plugin.AppState.ChosenGameMode = GameMode.Undecided;
            plugin.AppState.CurrentRoomId = string.Empty;
            plugin.AppState.ConnectionFailureMessage = string.Empty;
            plugin.GameController = new OfflineController(plugin);
            plugin.TurnManager.PendingLogAnnouncements.Clear();
            plugin.UiState.ActiveOverlayMessages.Clear();
            plugin.UiState.OverlayMessageQueue.Clear();
            plugin.UiState.SecondaryMessageQueue.Clear();
            plugin.TurnManager.DeferredNetworkPhase = null;
            plugin.GameState.PendingDrinkGiverName = string.Empty;
            plugin.GameState.PendingDrinkAmount = 0;
            plugin.UiState.BusRideResetTimer = -1f;
            plugin.UiState.BusRidePromptGrowTimer = -1f;
            plugin.UiState.BusRideVictoryResetTimer = -1f;
            plugin.UiState.BusRideEndConfirmationPending = false;
            plugin.UiState.NetworkDealerActionPending = false;
            ClearDealerPhaseChangePrompt();
            plugin.UiState.SecondaryMessage = null;
            plugin.UiState.SecondaryMessageAnimTime = -1f;
            plugin.UiState.CurrentRightSideMessage = "";
            plugin.UiState.TargetRightSideMessage = "";
            plugin.UiState.RightSideMessageScale = 1.0f;
            plugin.UiState.RightSideMessageAnimTimer = -1f;
            plugin.UiState.RightSideMessageState = UIState.RightSideAnimState.Normal;
            plugin.TurnManager.NpcThinkingTimer = -1f;
            plugin.TurnManager.DealerNpcHasGuessed = false;
            plugin.TurnManager.DealerCurrentNpcGuess = -1;
            plugin.TurnManager.DealerTransitionTimer = 0f;
            plugin.TurnManager.DealerNeedNextPlayer = false;
            plugin.TurnManager.PlayerNpcTurnsPending = false;
            plugin.TurnManager.PlayerNpcTurnIndex = 0;
            plugin.TurnManager.PlayerNpcTurnRound = -1;
            plugin.TurnManager.PlayerNpcOutcomeTimer = 0f;
            plugin.TurnManager.PendingPlayerNpcOutcome = null;
            plugin.TurnManager.PendingPlayerBusRideGuess = -1;
            plugin.TurnManager.PlayerBusRideResultTimer = -1f;
            ResetPyramidDealerAutomation();
            plugin.AppState.ResetToModeSelectionTimer = 0f;
            plugin.TurnManager.DealerPhaseTransitionTimer = 0f;
            plugin.TurnManager.DealerNextPhasePending = null;
        }
        public void RestartLocalDealerGame()
        {
            ResetGame();
            plugin.AppState.ChosenGameMode = GameMode.Dealer;
            plugin.AppState.ActiveConnectionMode = ConnectionMode.LocalOnly;
            SetGameMode(GameMode.Dealer);
            plugin.PromptWindow.ResetPromptState();
        }
        public void SetGameMode(GameMode mode)
        {
            plugin.UiState.BusRideEndConfirmationPending = false;
            plugin.UiState.NetworkDealerActionPending = false;
            ClearDealerPhaseChangePrompt();
            plugin.GameState.ActiveMode = mode;
            if (mode == GameMode.Dealer)
            {
                plugin.GameState.Players.Clear();
                var chosenScions = PickLocalNpcNames().ToList();
                foreach (var scion in chosenScions)
                {
                    plugin.GameState.Players.Add(new Player { Name = scion, IsLocal = false });
                }

                plugin.TurnManager.DealerCurrentPlayerIndex = 0;
                plugin.GameState.DealerActivePlayerName = chosenScions[0];
                plugin.TurnManager.DealerTransitionStarted = false;
                plugin.TurnManager.DealerNeedNextPlayer = false;
                plugin.GameState.PendingDrinkGiverName = string.Empty;
                plugin.GameState.PendingDrinkAmount = 0;
                plugin.TurnManager.HandTransitionTimer = 0f;
                plugin.TurnManager.TransitionPrevHand.Clear();
                plugin.TurnManager.TransitionNextHand.Clear();

                plugin.TurnManager.DealerNpcHasGuessed = false;
                plugin.TurnManager.DealerCurrentNpcGuess = -1;
                plugin.TurnManager.DealerTransitionTimer = 0f;
                plugin.TurnManager.PendingPlayerNpcOutcome = null;
                plugin.TurnManager.PendingPlayerBusRideGuess = -1;
                plugin.TurnManager.PlayerBusRideResultTimer = -1f;
                plugin.AppState.ResetToModeSelectionTimer = 0f;
                plugin.TurnManager.DealerPhaseTransitionTimer = 0f;
                plugin.TurnManager.DealerNextPhasePending = null;
                ResetPyramidDealerAutomation();
                var activePlayer = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.DealerActivePlayerName);
                bool isNpc = activePlayer != null ? (!activePlayer.IsHuman && !activePlayer.IsLocal) : true;
                if (isNpc)
                {
                    plugin.TurnManager.NpcThinkingTimer = UIConstants.AiThinkingBaseDuration + (float)Random.Shared.NextDouble() * UIConstants.AiThinkingVariance;
                    SetRightSideMessage($"{plugin.GameState.DealerActivePlayerName} is thinking...");
                }
                else
                {
                    plugin.TurnManager.NpcThinkingTimer = -1f;
                    SetRightSideMessage(string.Empty);
                }
            }
            else if (mode == GameMode.Player)
            {
                var activeConnectionMode = plugin.AppState.ActiveConnectionMode;
                ResetGame();
                plugin.AppState.ChosenGameMode = GameMode.Player;
                plugin.AppState.ActiveConnectionMode = activeConnectionMode;
                plugin.GameState.ActiveMode = GameMode.Player;
                ConfigureLocalPlayerModeNpcs();
            }
        }
        private IEnumerable<string> PickLocalNpcNames()
        {
            int npcCount = Math.Clamp(plugin.Configuration.NpcCount, 1, GameConstants.ScionNames.Length);
            return GameConstants.ScionNames
                .OrderBy(_ => Random.Shared.Next())
                .Take(npcCount);
        }
        private void ConfigureLocalPlayerModeNpcs()
        {
            string localName = plugin.GameState.Players.FirstOrDefault(p => p.IsLocal)?.Name ?? GameConstants.LocalPlayerName;
            var chosenScions = PickLocalNpcNames().ToList();

            plugin.GameState.Players.Clear();
            plugin.GameState.Players.Add(new Player
            {
                Name = localName,
                IsLocal = true,
                IsEligibleForCurrentBusRide = true
            });

            foreach (var scion in chosenScions)
            {
                plugin.GameState.Players.Add(new Player
                {
                    Name = scion,
                    IsLocal = false,
                    IsDealer = false,
                    IsHuman = false,
                    IsEligibleForCurrentBusRide = true
                });
            }
        }
    }
}

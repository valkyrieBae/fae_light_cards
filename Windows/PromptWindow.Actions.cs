using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class PromptWindow
    {
        private void HandleChoiceClick(int optionIndex)
        {
            plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);

            if (plugin.AppState.ChosenGameMode == GameMode.Undecided)
            {
                HandleModeSelectionClick(optionIndex);
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Undecided)
            {
                HandleConnectionSelectionClick(optionIndex);
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.ConnectionFailed)
            {
                HandleConnectionFailedClick(optionIndex);
            }
            else if (plugin.GameState.ActiveMode == GameMode.Dealer && plugin.IsLocalDealer)
            {
                HandleDealerControlClick(optionIndex);
            }
            else
            {
                plugin.GameController.HandlePlayerGuess(optionIndex);
            }
        }

        private void HandleModeSelectionClick(int optionIndex)
        {
            if (optionIndex == 0)
            {
                plugin.AppState.ChosenGameMode = GameMode.Player;
            }
            else if (optionIndex == 1)
            {
                plugin.AppState.ChosenGameMode = GameMode.Dealer;
            }
        }

        private void HandleConnectionSelectionClick(int optionIndex)
        {
            if (optionIndex == 0)
            {
                plugin.AppState.ActiveConnectionMode = ConnectionMode.LocalOnly;
                plugin.SetGameMode(plugin.AppState.ChosenGameMode);
            }
            else if (optionIndex == 1)
            {
                StartNetworkController();
            }
        }

        private void HandleConnectionFailedClick(int optionIndex)
        {
            if (optionIndex == 0)
            {
                StartNetworkController();
            }
            else if (optionIndex == 1)
            {
                plugin.AppState.ActiveConnectionMode = ConnectionMode.LocalOnly;
                plugin.SetGameMode(plugin.AppState.ChosenGameMode);
            }
        }

        private void HandleDealerControlClick(int optionIndex)
        {
            if (plugin.UiState.DealerPhaseChangePrompt != UIState.DealerPhaseChangePromptState.None)
            {
                HandleDealerPhaseChangePromptClick(optionIndex);
            }
            else if (plugin.GameState.ActivePhase == GamePhase.Accumulation)
            {
                HandleDealerAccumulationClick();
            }
            else if (plugin.GameState.ActivePhase == GamePhase.BusRide)
            {
                HandleDealerBusRideClick(optionIndex);
            }
        }

        private void HandleDealerAccumulationClick()
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

        private void HandleDealerBusRideClick(int optionIndex)
        {
            bool canUseBusRideControls = CanUseDealerBusRideControls();
            if (plugin.UiState.BusRideEndConfirmationPending)
            {
                if (optionIndex == 0 && canUseBusRideControls)
                {
                    plugin.UiState.BusRideEndConfirmationPending = false;
                    plugin.GameController.EndGame();
                }
                else if (optionIndex == 1 && canUseBusRideControls)
                {
                    plugin.UiState.BusRideEndConfirmationPending = false;
                }
            }
            else if (canUseBusRideControls)
            {
                if (optionIndex == 0)
                {
                    plugin.GameController.HandleDealerDealBusCard();
                }
                else if (optionIndex == 1)
                {
                    plugin.UiState.BusRideEndConfirmationPending = true;
                }
            }
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

        private void HandleJoinRoom()
        {
            plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
            if (plugin.GameController is NetworkController nc)
            {
                nc.JoinRoom(roomInput);
            }
        }

        private void HandleCancelNetworkFlow()
        {
            plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
            plugin.ResetGame();
        }

        private void HandleCopyRoomCode(string roomId)
        {
            plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
            ImGui.SetClipboardText(roomId);
            copiedTimer = 2.0f;
        }

        private void HandleStartNetworkGame(bool includeNpcs)
        {
            plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
            if (plugin.GameController is NetworkController nc)
            {
                nc.StartGame(includeNpcs);
            }
        }

        private void HandleDisconnectNetworkGame()
        {
            plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
            plugin.ResetGame();
        }

        private void StartNetworkController()
        {
            plugin.AppState.ActiveConnectionMode = ConnectionMode.Connecting;
            plugin.GameController?.Dispose();
            plugin.GameController = new NetworkController(plugin);
        }
    }
}

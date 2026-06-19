using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class GameCoordinatorService
    {
        public void DrawCardWithAnimation()
        {
            var hand = plugin.GameState.GetRequiredDisplayedHand(nameof(DrawCardWithAnimation));
            int oldCount = hand.Count;
            plugin.RulesEngine.DrawCard();
            int newCount = hand.Count;

            if (newCount > oldCount)
            {
                var card = hand[newCount - 1];
                plugin.EventBus.PublishCardDealt(card, newCount - 1);
            }
        }
        public void ResetPyramidDealerAutomation()
        {
            plugin.TurnManager.PyramidDealerTimer = -1f;
            plugin.TurnManager.PyramidDealerPaused = false;
            plugin.TurnManager.PyramidDealerHasStarted = false;
        }
        public void ShowDealerPhaseChangePrompt(UIState.DealerPhaseChangePromptState promptState)
        {
            if (promptState == UIState.DealerPhaseChangePromptState.None)
            {
                ClearDealerPhaseChangePrompt();
                return;
            }

            if (!IsLocalOnlyDealer())
            {
                return;
            }

            plugin.UiState.DealerPhaseChangePrompt = promptState;
            plugin.UiState.DealerPhaseChangeEndGameConfirmationPending = false;
            plugin.UiState.BusRideEndConfirmationPending = false;
            GrowPromptIfHidden();
        }
        public void ClearDealerPhaseChangePrompt()
        {
            plugin.UiState.DealerPhaseChangePrompt = UIState.DealerPhaseChangePromptState.None;
            plugin.UiState.DealerPhaseChangeEndGameConfirmationPending = false;
        }
        public void BeginDealerBusRiderSelection()
        {
            if (!IsLocalOnlyDealer() || plugin.GameState.ActivePhase != GamePhase.Pyramid)
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
            var candidates = eligiblePlayers.Where(p => p.Hand.Count == maxCards).ToList();

            ClearDealerPhaseChangePrompt();
            plugin.HasTriggeredEndGame = true;

            if (candidates.Count == 1)
            {
                ChooseBusRider(candidates[0].Name);
                return;
            }

            plugin.GameState.ActivePhase = GamePhase.TieChoice;
            if (candidates.Count > 1)
            {
                plugin.GameState.ActionLog.Add("It's a tie! Choose a player from the player list to Ride the Bus.");
            }
        }
        private bool IsLocalOnlyDealer()
        {
            return plugin.AppState.ActiveConnectionMode == ConnectionMode.LocalOnly
                   && plugin.GameState.ActiveMode == GameMode.Dealer
                   && plugin.IsLocalDealer;
        }
        private void ValidateDealerPhaseChangePrompt()
        {
            var promptState = plugin.UiState.DealerPhaseChangePrompt;
            if (promptState == UIState.DealerPhaseChangePromptState.None)
            {
                return;
            }

            bool isValidPhase = promptState switch
            {
                UIState.DealerPhaseChangePromptState.Phase1Complete => plugin.GameState.ActivePhase == GamePhase.Accumulation
                                                                          && plugin.TurnManager.DealerNeedNextPlayer
                                                                          && AreAllNonDealersFinishedAccumulation(),
                UIState.DealerPhaseChangePromptState.Phase2Complete => plugin.GameState.ActivePhase == GamePhase.Pyramid
                                                                          && plugin.GameState.CurrentFlipIndex >= 15,
                _ => false
            };

            if (!IsLocalOnlyDealer() || !isValidPhase)
            {
                ClearDealerPhaseChangePrompt();
            }
        }
        private void EnsureDealerPhaseChangePrompt()
        {
            if (plugin.UiState.DealerPhaseChangePrompt != UIState.DealerPhaseChangePromptState.None)
            {
                return;
            }

            if (IsLocalOnlyDealer()
                && plugin.GameState.ActivePhase == GamePhase.Accumulation
                && plugin.TurnManager.DealerNeedNextPlayer
                && AreAllNonDealersFinishedAccumulation())
            {
                ShowDealerPhaseChangePrompt(UIState.DealerPhaseChangePromptState.Phase1Complete);
            }
        }
        private bool AreAllNonDealersFinishedAccumulation()
        {
            var nonDealers = plugin.GameState.Players.Where(p => !p.IsDealer).ToList();
            return nonDealers.Count > 0 && nonDealers.All(p => p.Hand.Count >= 4);
        }
    }
}

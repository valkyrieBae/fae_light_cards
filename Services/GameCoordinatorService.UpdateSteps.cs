using System;
using System.Linq;

namespace FaeLightCards
{
    public partial class GameCoordinatorService
    {
        private const float RightSideMessagePopDuration = 0.15f;

        private void ClearBusRideEndConfirmationOutsideBusRide()
        {
            if (plugin.GameState.ActivePhase != GamePhase.BusRide)
            {
                plugin.UiState.BusRideEndConfirmationPending = false;
            }
        }

        private void ApplyDeferredNetworkPhase()
        {
            if (!plugin.TurnManager.DeferredNetworkPhase.HasValue)
            {
                return;
            }

            if (!plugin.IsAnimationPlaying && plugin.UiState.OverlayMessageQueue.Count == 0)
            {
                plugin.GameState.ActivePhase = plugin.TurnManager.DeferredNetworkPhase.Value;
                plugin.TurnManager.DeferredNetworkPhase = null;
            }
        }

        private void UpdatePendingLogAnnouncements(float dt)
        {
            for (int i = plugin.TurnManager.PendingLogAnnouncements.Count - 1; i >= 0; i--)
            {
                var ann = plugin.TurnManager.PendingLogAnnouncements[i];
                ann.DelayTimer -= dt;
                if (ann.DelayTimer <= 0f)
                {
                    plugin.GameState.ActionLog.Add(ann.Message);
                    plugin.TurnManager.PendingLogAnnouncements.RemoveAt(i);
                }
            }
        }

        private void ApplyPendingLocalHandCardRemoval()
        {
            if (!plugin.GameState.PendingPlaySlotIndex.HasValue)
            {
                return;
            }

            plugin.RulesEngine.PlayHandCard(plugin.GameState.PendingPlaySlotIndex.Value);
            plugin.GameState.PendingPlaySlotIndex = null;
        }

        private void UpdateHandTransitionTimer(float dt)
        {
            if (plugin.TurnManager.HandTransitionTimer <= 0f)
            {
                return;
            }

            plugin.TurnManager.HandTransitionTimer -= dt;
            if (plugin.TurnManager.HandTransitionTimer <= 0f)
            {
                plugin.TurnManager.HandTransitionTimer = 0f;
                plugin.TurnManager.TransitionPrevHand.Clear();
                plugin.TurnManager.TransitionNextHand.Clear();
            }
        }

        private void UpdateDealerPhaseTransitionTimer(float dt)
        {
            if (plugin.TurnManager.DealerPhaseTransitionTimer <= 0f || plugin.GameState.HasPendingDrinkTarget)
            {
                return;
            }

            plugin.TurnManager.DealerPhaseTransitionTimer -= dt;
            if (plugin.TurnManager.DealerPhaseTransitionTimer > 0f)
            {
                return;
            }

            plugin.TurnManager.DealerPhaseTransitionTimer = 0f;
            if (plugin.TurnManager.DealerNextPhasePending == GamePhase.Pyramid)
            {
                plugin.GameState.ActivePhase = GamePhase.Pyramid;
                plugin.RulesEngine.SetupPyramid();
                plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
                SetPromptNormal(resetTimer: false);
                ResetDealerGuessState();
            }
            plugin.TurnManager.DealerNextPhasePending = null;
        }

        private void UpdateRightSideMessageAndPhaseTimers(float dt)
        {
            if (!ShouldUpdateRightSideMessageAndPhaseTimers())
            {
                return;
            }

            UpdateRightSideMessageAnimation(dt);
            UpdateModePhaseTimers(dt);
        }

        private bool ShouldUpdateRightSideMessageAndPhaseTimers()
        {
            return plugin.GameState.ActivePhase == GamePhase.BusRide
                   || (plugin.GameState.ActivePhase == GamePhase.Accumulation
                       && (plugin.GameState.ActiveMode == GameMode.Dealer
                           || (plugin.GameState.ActiveMode == GameMode.Player && plugin.TurnManager.PlayerNpcTurnsPending)));
        }

        private void UpdateRightSideMessageAnimation(float dt)
        {
            if (plugin.UiState.RightSideMessageState == UIState.RightSideAnimState.PoppingOut)
            {
                plugin.UiState.RightSideMessageAnimTimer += dt;
                float t = Math.Clamp(plugin.UiState.RightSideMessageAnimTimer / RightSideMessagePopDuration, 0f, 1f);
                plugin.UiState.RightSideMessageScale = 1.0f - t;
                if (plugin.UiState.RightSideMessageAnimTimer >= RightSideMessagePopDuration)
                {
                    plugin.UiState.CurrentRightSideMessage = plugin.UiState.TargetRightSideMessage;
                    plugin.UiState.RightSideMessageState = UIState.RightSideAnimState.PoppingIn;
                    plugin.UiState.RightSideMessageAnimTimer = 0f;
                }
            }
            else if (plugin.UiState.RightSideMessageState == UIState.RightSideAnimState.PoppingIn)
            {
                plugin.UiState.RightSideMessageAnimTimer += dt;
                float t = Math.Clamp(plugin.UiState.RightSideMessageAnimTimer / RightSideMessagePopDuration, 0f, 1f);
                plugin.UiState.RightSideMessageScale = t;
                if (plugin.UiState.RightSideMessageAnimTimer >= RightSideMessagePopDuration)
                {
                    plugin.UiState.RightSideMessageState = UIState.RightSideAnimState.Normal;
                    plugin.UiState.RightSideMessageScale = 1.0f;
                    plugin.UiState.RightSideMessageAnimTimer = -1f;
                }
            }
        }

        private void UpdateModePhaseTimers(float dt)
        {
            if (plugin.GameState.ActiveMode == GameMode.Player && plugin.GameState.ActivePhase == GamePhase.BusRide)
            {
                UpdatePlayerBusRideTimers(dt);
            }
            else if (plugin.GameState.ActiveMode == GameMode.Dealer && plugin.GameState.ActivePhase == GamePhase.Accumulation)
            {
                UpdateDealerAccumulationTimers(dt);
            }
            else if (plugin.GameState.ActiveMode == GameMode.Dealer && plugin.GameState.ActivePhase == GamePhase.BusRide)
            {
                UpdateDealerBusRideTimers(dt);
            }
        }

        private void UpdatePlayerBusRideTimers(float dt)
        {
            UpdateBusRideVictoryResetTimer(dt);
            UpdatePlayerBusRideResetTimer(dt);
            UpdatePlayerBusRidePromptGrowTimer(dt);
        }

        private void UpdatePlayerBusRideResetTimer(float dt)
        {
            if (plugin.UiState.BusRideResetTimer <= 0f)
            {
                return;
            }

            plugin.UiState.BusRideResetTimer -= dt;
            if (plugin.UiState.BusRideResetTimer > 0f)
            {
                return;
            }

            plugin.UiState.BusRideResetTimer = -1f;
            SlideCurrentBusRideCardRight();
            plugin.GameState.BusRideCurrentCard = null;

            var newCard = plugin.RulesEngine.DrawBusRideCard();
            plugin.GameState.BusRideCurrentCard = newCard;
            plugin.HandWindow.TriggerBusRideDeal(newCard);

            SetBusRiderRightSideMessage(updateNpcThinkingTimer: false);
            GrowPromptForLocalBusRider();
        }

        private void UpdatePlayerBusRidePromptGrowTimer(float dt)
        {
            if (plugin.UiState.BusRidePromptGrowTimer <= 0f)
            {
                return;
            }

            plugin.UiState.BusRidePromptGrowTimer -= dt;
            if (plugin.UiState.BusRidePromptGrowTimer > 0f)
            {
                return;
            }

            plugin.UiState.BusRidePromptGrowTimer = -1f;
            SetBusRiderRightSideMessage(updateNpcThinkingTimer: false);
            GrowPromptForLocalBusRider();
        }

        private void UpdateDealerAccumulationTimers(float dt)
        {
            if (plugin.TurnManager.DealerTransitionTimer > 0f)
            {
                UpdateDealerAccumulationTransitionTimer(dt);
            }
            else if (plugin.AppState.ResetToModeSelectionTimer > 0f)
            {
                UpdateResetToModeSelectionTimer(dt);
            }
        }

        private void UpdateDealerAccumulationTransitionTimer(float dt)
        {
            plugin.TurnManager.DealerTransitionTimer -= dt;
            if (plugin.TurnManager.DealerTransitionTimer > 0f)
            {
                return;
            }

            plugin.TurnManager.DealerTransitionTimer = 0f;
            ResetDealerGuessState();
            SetActiveDealerPlayerThinkingMessage();
            plugin.TurnManager.DealerTransitionStarted = false;
            ShrinkPrompt();
        }

        private void UpdateResetToModeSelectionTimer(float dt)
        {
            plugin.AppState.ResetToModeSelectionTimer -= dt;
            if (plugin.AppState.ResetToModeSelectionTimer <= 0f)
            {
                plugin.AppState.ResetToModeSelectionTimer = 0f;
                plugin.ResetGame();
            }
        }

        private void UpdateDealerBusRideTimers(float dt)
        {
            UpdateBusRideVictoryResetTimer(dt);
            UpdateDealerBusRideResetTimer(dt);
            UpdateDealerBusRideTransitionTimer(dt);
        }

        private void UpdateDealerBusRideResetTimer(float dt)
        {
            if (plugin.UiState.BusRideResetTimer <= 0f)
            {
                return;
            }

            plugin.UiState.BusRideResetTimer -= dt;
            if (plugin.UiState.BusRideResetTimer > 0f)
            {
                return;
            }

            plugin.UiState.BusRideResetTimer = -1f;
            SlideCurrentBusRideCardRight();
            plugin.GameState.BusRideCurrentCard = null;
            ResetDealerGuessState();
            StopNpcThinking();
            SetPromptNormal(resetTimer: true);
        }

        private void UpdateDealerBusRideTransitionTimer(float dt)
        {
            if (plugin.TurnManager.DealerTransitionTimer <= 0f)
            {
                return;
            }

            plugin.TurnManager.DealerTransitionTimer -= dt;
            if (plugin.TurnManager.DealerTransitionTimer > 0f)
            {
                return;
            }

            plugin.TurnManager.DealerTransitionTimer = 0f;
            if (plugin.UiState.BusRideVictoryResetTimer > 0f)
            {
                return;
            }

            ResetDealerGuessState();
            if (plugin.GameState.BusRideCurrentCard == null)
            {
                StopNpcThinking();
                SetPromptNormal(resetTimer: true);
            }
            else
            {
                SetBusRiderRightSideMessage(updateNpcThinkingTimer: true);
                ShrinkPrompt();
            }
        }

        private void UpdateBusRideVictoryResetTimer(float dt)
        {
            if (plugin.UiState.BusRideVictoryResetTimer <= 0f)
            {
                return;
            }

            plugin.UiState.BusRideVictoryResetTimer -= dt;
            if (plugin.UiState.BusRideVictoryResetTimer <= 0f)
            {
                plugin.UiState.BusRideVictoryResetTimer = -1f;
                plugin.ResetGame();
            }
        }

        private void SlideCurrentBusRideCardRight()
        {
            var oldCard = plugin.GameState.BusRideCurrentCard;
            if (oldCard != null)
            {
                plugin.HandWindow.TriggerBusRideSlideRight(oldCard);
            }
        }

        private void ResetDealerGuessState()
        {
            plugin.TurnManager.DealerNpcHasGuessed = false;
            plugin.TurnManager.DealerCurrentNpcGuess = -1;
        }

        private void SetPromptNormal(bool resetTimer)
        {
            plugin.UiState.PromptState = UIState.PromptAnimState.Normal;
            plugin.UiState.PromptScale = 1.0f;
            if (resetTimer)
            {
                plugin.UiState.PromptAnimTimer = 0f;
            }
        }

        private void ShrinkPrompt()
        {
            plugin.UiState.PromptState = UIState.PromptAnimState.Shrinking;
            plugin.UiState.PromptAnimTimer = 0f;
        }

        private void StopNpcThinking()
        {
            plugin.TurnManager.NpcThinkingTimer = -1f;
            SetRightSideMessage(string.Empty);
        }

        private void SetActiveDealerPlayerThinkingMessage()
        {
            var activePlayer = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.DealerActivePlayerName);
            bool isNpc = activePlayer != null ? (!activePlayer.IsHuman && !activePlayer.IsLocal) : true;
            if (isNpc)
            {
                plugin.TurnManager.NpcThinkingTimer = UIConstants.AiThinkingBaseDuration;
                SetRightSideMessage($"{plugin.GameState.DealerActivePlayerName} is thinking...");
            }
            else
            {
                StopNpcThinking();
            }
        }

        private void SetBusRiderRightSideMessage(bool updateNpcThinkingTimer)
        {
            var rider = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.BusRiderName);
            bool isNpc = rider != null
                ? (!rider.IsHuman && !rider.IsLocal)
                : (plugin.GameState.BusRiderName != GameConstants.LocalPlayerName);

            if (isNpc)
            {
                if (updateNpcThinkingTimer)
                {
                    plugin.TurnManager.NpcThinkingTimer = UIConstants.AiThinkingBaseDuration;
                }
                SetRightSideMessage($"{plugin.GameState.BusRiderName} is thinking...");
            }
            else
            {
                if (updateNpcThinkingTimer)
                {
                    plugin.TurnManager.NpcThinkingTimer = -1f;
                }
                SetRightSideMessage(string.Empty);
            }
        }

        private void GrowPromptForLocalBusRider()
        {
            if (plugin.GameState.BusRiderName == GameConstants.LocalPlayerName)
            {
                GrowPromptIfHidden();
            }
        }
    }
}

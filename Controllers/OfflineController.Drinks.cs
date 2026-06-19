using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class OfflineController
    {
        public void GivePendingDrinkToPlayer(string targetPlayerName)
        {
            if (!ApplyPendingDrinkTarget(targetPlayerName, requireLocalGiver: true)) return;

            plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
            ResumeAfterPendingDrinkTarget();
        }
        private bool SetPendingDrinkTarget(Player giver, int amount)
        {
            bool hasValidTarget = plugin.GameState.Players.Any(target => !target.IsDealer && target.Name != giver.Name);
            if (hasValidTarget)
            {
                plugin.GameState.PendingDrinkGiverName = giver.Name;
                plugin.GameState.PendingDrinkAmount = amount;
                return true;
            }
            else
            {
                ClearPendingDrinkTarget();
                return false;
            }
        }
        private void ClearPendingDrinkTarget()
        {
            plugin.GameState.PendingDrinkGiverName = string.Empty;
            plugin.GameState.PendingDrinkAmount = 0;
        }
        private Player? SelectAutoDrinkTarget(Player giver)
        {
            if (plugin.Configuration.RigNpcsAlwaysGiveToPlayer)
            {
                var localTarget = plugin.GameState.Players.FirstOrDefault(target => target.IsLocal && !target.IsDealer && target.Name != giver.Name);
                if (localTarget != null)
                {
                    return localTarget;
                }
            }

            var targets = plugin.GameState.Players
                .Where(target => !target.IsDealer && target.Name != giver.Name)
                .ToList();

            return targets.Count > 0 ? targets[rng.Next(targets.Count)] : null;
        }
        private bool ApplyPendingDrinkTarget(string targetPlayerName, bool requireLocalGiver)
        {
            if (!plugin.GameState.HasPendingDrinkTarget) return false;

            var giver = plugin.GameState.Players.FirstOrDefault(p => p.Name == plugin.GameState.PendingDrinkGiverName);
            if (giver == null || giver.IsDealer) return false;

            if (requireLocalGiver)
            {
                var localPlayer = plugin.GameState.Players.FirstOrDefault(p => p.IsLocal);
                if (localPlayer == null || localPlayer.Name != giver.Name)
                {
                    return false;
                }
            }

            var target = plugin.GameState.Players.FirstOrDefault(p => p.Name == targetPlayerName);
            if (target == null || target.IsDealer || target.Name == giver.Name) return false;

            int drinks = plugin.GameState.PendingDrinkAmount;
            giver.DrinksGiven += drinks;
            target.DrinksTaken += drinks;

            string giverDisplay = giver.IsLocal ? GameConstants.LocalPlayerName : giver.Name;
            plugin.GameState.ActionLog.Add($"{giverDisplay} gave {drinks} drink{(drinks == 1 ? "" : "s")} to {target.Name}.");
            plugin.EventBus.PublishSecondaryMessage($"{giverDisplay} gave {drinks} drink{(drinks == 1 ? "" : "s")} to {target.Name}!");

            ClearPendingDrinkTarget();
            return true;
        }
        private void ResumeAfterPendingDrinkTarget()
        {
            if (plugin.GameState.ActivePhase != GamePhase.Accumulation) return;

            bool hasUnshownOverlay = plugin.UiState.OverlayMessageQueue.Count > 0
                                      || plugin.UiState.PendingWinLoseMessage != null;
            if (hasUnshownOverlay) return;

            if (plugin.GameState.ActiveMode == GameMode.Player)
            {
                if (plugin.TurnManager.PlayerNpcTurnsPending)
                {
                    return;
                }

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
            else if (plugin.GameState.ActiveMode == GameMode.Dealer)
            {
                plugin.TurnManager.DealerNeedNextPlayer = true;
                plugin.EventBus.PublishRightSideMessage(string.Empty);
                if (AreAllNonDealersFinishedAccumulation())
                {
                    plugin.GameCoordinator.ShowDealerPhaseChangePrompt(UIState.DealerPhaseChangePromptState.Phase1Complete);
                }
                plugin.GameCoordinator.GrowPromptIfHidden();
            }
        }
    }
}

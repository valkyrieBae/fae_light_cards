using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class OfflineController
    {
        public void HandleFlipPyramidCard()
        {
            if (plugin.GameState.ActivePhase != GamePhase.Pyramid) return;
            if (plugin.RulesEngine.HasPendingMatches()) return;
            if (plugin.IsAnimationPlaying || plugin.TurnManager.DealerPhaseTransitionTimer > 0f || plugin.GameState.CurrentFlipIndex >= 15) return;

            int idx = plugin.GameState.CurrentFlipIndex;
            if (!plugin.GameState.PyramidFlipped[idx])
            {
                plugin.GameState.PyramidFlipped[idx] = true;
                plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);

                plugin.GameState.CurrentFlipIndex++;

                plugin.RulesEngine.ProcessMatchesForFlippedCard(idx);
            }
        }
        public void HandleAdvancePyramidRow()
        {
            if (plugin.GameState.ActivePhase != GamePhase.Pyramid) return;

            if (plugin.GameState.ActiveRow > 1 && RulesEngine.GetRowIndex(plugin.GameState.CurrentFlipIndex) < plugin.GameState.ActiveRow)
            {
                plugin.RulesEngine.AdvanceToNextRow();
                plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
            }
        }
        public void PlayLocalMatch(string targetPlayerName)
        {
            int slotIdx = plugin.GameState.PendingLocalMatchSlotIndex;
            if (slotIdx == -1) return;

            Card flippedCard = plugin.GameState.Pyramid[slotIdx];
            int row = RulesEngine.GetRowIndex(slotIdx);
            int multiplier = RulesEngine.GetRowMultiplier(row);

            var localPlayer = plugin.GameState.Players.FirstOrDefault(p => p.IsLocal);
            if (localPlayer == null) return;

            Card? matchedCard = localPlayer.Hand.FirstOrDefault(c => c != null && c.Rank == flippedCard.Rank);
            if (matchedCard == null) return;

            int slotIndex = localPlayer.Hand.IndexOf(matchedCard);

            // Play the draw sound
            plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);

            // Trigger discard animation from hand slot to pyramid slot via EventBus
            plugin.EventBus.PublishLocalCardMatched(localPlayer, matchedCard, slotIndex, slotIdx, targetPlayerName);

            // Defer removal from hand
            plugin.GameState.PendingPlaySlotIndex = slotIndex;

            var targetPlayer = plugin.GameState.Players.FirstOrDefault(p => p.Name == targetPlayerName);
            if (targetPlayer != null)
            {
                targetPlayer.DrinksTaken += multiplier;
            }
            localPlayer.DrinksGiven += multiplier;

            // Add action log entry
            plugin.GameState.ActionLog.Add($"You matched {flippedCard.Rank}! Gave {multiplier} {(multiplier == 1 ? "drink" : "drinks")} to {targetPlayerName}.");

            // Clear pending match
            plugin.GameState.PendingLocalMatchSlotIndex = -1;
        }
        private void UpdatePendingScionMatches()
        {
            if (!plugin.RulesEngine.HasPendingScionMatches) return;

            // Don't process the next play until all active discard animations have completely finished
            if (plugin.AnimationManager.HasActiveDiscardAnimations) return;

            if (plugin.RulesEngine.TryDequeuePendingScionMatch(ImGui.GetIO().DeltaTime, out var match))
            {
                ExecuteScionMatch(match.Player, match.Card, match.SlotIndex);
            }
        }
        private void ExecuteScionMatch(Player p, Card scionCard, int slotIdx)
        {
            if (!p.Hand.Contains(scionCard)) return;

            Card flippedCard = plugin.GameState.Pyramid[slotIdx];
            int row = RulesEngine.GetRowIndex(slotIdx);
            int multiplier = RulesEngine.GetRowMultiplier(row);

            // Remove from Scion's hand immediately to prevent double matching
            p.Hand.Remove(scionCard);

            // Select target player who receives the drinks
            Player? targetPlayer = null;
            if (plugin.Configuration.RigNpcsAlwaysGiveToPlayer)
            {
                targetPlayer = plugin.GameState.Players.FirstOrDefault(target => target.IsLocal);
            }

            if (targetPlayer == null)
            {
                var targets = plugin.GameState.Players.Where(target => target.Name != p.Name).ToList();
                if (targets.Count > 0)
                {
                    targetPlayer = targets[rng.Next(targets.Count)];
                }
                else
                {
                    targetPlayer = plugin.GameState.Players.FirstOrDefault();
                }
            }

            if (targetPlayer == null) return;

            // Update stats
            targetPlayer.DrinksTaken += multiplier;
            p.DrinksGiven += multiplier;

            // Defer action log entry by 300ms from the start of the card's movement
            plugin.GameCoordinator.AddPendingLogAnnouncement(
                $"{p.Name} matched {flippedCard.Rank}! Gave {multiplier} {(multiplier == 1 ? "drink" : "drinks")} to {targetPlayer.Name}.",
                0.30f
            );

            // Trigger discard glide via EventBus
            plugin.EventBus.PublishScionCardMatched(p, scionCard, slotIdx, targetPlayer.Name);
        }
    }
}

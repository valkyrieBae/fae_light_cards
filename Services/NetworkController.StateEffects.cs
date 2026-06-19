using System.Collections.Generic;
using System.Linq;

namespace FaeLightCards
{
    public partial class NetworkController
    {
        private bool PublishAccumulationOutcome(GameStateDto dto, NetworkStateApplyContext context)
        {
            if (dto.ActivePhase != (int)GamePhase.Accumulation)
            {
                return false;
            }

            var oldState = context.OldState;
            bool hasNewPendingTarget = !oldState.HasPendingDrinkTarget
                                       && !string.IsNullOrWhiteSpace(dto.PendingDrinkGiverName)
                                       && dto.PendingDrinkAmount > 0;
            if (hasNewPendingTarget)
            {
                var giver = dto.Players.FirstOrDefault(p => p.Name == dto.PendingDrinkGiverName);
                if (giver?.IsLocal == true)
                {
                    plugin.EventBus.PublishSecondaryMessage("Correct! You win!");
                }
                else
                {
                    plugin.EventBus.PublishSecondaryMessage($"Correct! {dto.PendingDrinkGiverName} wins!");
                }
                plugin.EventBus.PublishPlaySound(plugin.Configuration.WinSound);
                return true;
            }

            PlayerDto? giverChange = null;
            int givenDelta = 0;
            var takenChanges = new List<(PlayerDto Player, int Delta)>();

            foreach (var dtoPlayer in dto.Players)
            {
                var oldPlayer = oldState.Players.FirstOrDefault(p => p.Name == dtoPlayer.Name);
                if (oldPlayer == null) continue;

                int currentGivenDelta = dtoPlayer.DrinksGiven - oldPlayer.DrinksGiven;
                if (currentGivenDelta > 0 && giverChange == null)
                {
                    giverChange = dtoPlayer;
                    givenDelta = currentGivenDelta;
                }

                int takenDelta = dtoPlayer.DrinksTaken - oldPlayer.DrinksTaken;
                if (takenDelta > 0)
                {
                    takenChanges.Add((dtoPlayer, takenDelta));
                }
            }

            if (giverChange != null)
            {
                var targetChange = takenChanges.FirstOrDefault(change => change.Player.Name != giverChange.Name);
                string drinks = FormatDrinkCount(givenDelta);
                string message;
                if (giverChange.IsLocal)
                {
                    message = targetChange.Player != null
                        ? $"You gave {drinks} to {targetChange.Player.Name}!"
                        : $"You gave {drinks}!";
                }
                else if (targetChange.Player?.IsLocal == true)
                {
                    message = $"{giverChange.Name} gave you {drinks}!";
                }
                else
                {
                    message = targetChange.Player != null
                        ? $"{giverChange.Name} gave {targetChange.Player.Name} {drinks}!"
                        : $"{giverChange.Name} gave {drinks}!";
                }

                plugin.EventBus.PublishSecondaryMessage(message);
                plugin.EventBus.PublishPlaySound(plugin.Configuration.WinSound);
                return true;
            }

            if (takenChanges.Count > 0)
            {
                var firstTaken = takenChanges[0];
                string drinks = FormatDrinkCount(firstTaken.Delta);
                string message = firstTaken.Player.IsLocal
                    ? $"Wrong! Take {drinks}!"
                    : $"Wrong! {firstTaken.Player.Name} takes {drinks}!";
                plugin.EventBus.PublishSecondaryMessage(message);
                plugin.EventBus.PublishPlaySound(plugin.Configuration.LoseSound);
                return true;
            }

            return false;
        }

        private void PublishDealAnimations(GameStateDto dto, NetworkStateApplyContext context)
        {
            var oldState = context.OldState;
            foreach (var dtoPlayer in dto.Players)
            {
                var localPlayer = oldState.Players.FirstOrDefault(p => p.Name == dtoPlayer.Name);
                int currentHandCount = localPlayer?.Hand.Count ?? 0;

                if (dtoPlayer.Hand.Count > currentHandCount)
                {
                    bool shouldAnimate = false;
                    var localMe = oldState.Players.FirstOrDefault(p => p.IsLocal);
                    bool isLocalDealer = localMe != null && localMe.IsDealer;
                    if (isLocalDealer)
                    {
                        shouldAnimate = dtoPlayer.Name == dto.DealerActivePlayerName;
                    }
                    else
                    {
                        shouldAnimate = dtoPlayer.IsLocal;
                    }

                    for (int j = currentHandCount; j < dtoPlayer.Hand.Count; j++)
                    {
                        var newCard = dtoPlayer.Hand[j].ToCard();
                        if (!context.IsTransitionToPyramid && dto.ActivePhase == (int)GamePhase.Accumulation && shouldAnimate)
                        {
                            plugin.EventBus.PublishCardDealt(newCard, j);

                            if (!context.HandledAccumulationOutcome)
                            {
                                plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
                            }
                        }
                    }
                }
            }
        }

        private void PublishPyramidMatchAnimations(GameStateDto dto, NetworkStateApplyContext context)
        {
            var oldState = context.OldState;
            for (int i = 0; i < 15; i++)
            {
                int oldMatchCount = i < oldState.PyramidMatchedCardsLists.Length ? oldState.PyramidMatchedCardsLists[i].Count : 0;
                int pendingCount = plugin.HandWindow.GetPendingDiscardCount(i);
                int currentTotal = oldMatchCount + pendingCount;
                int newMatchCount = i < dto.PyramidMatchedCardsLists.Count ? dto.PyramidMatchedCardsLists[i].Count : 0;

                if (newMatchCount > currentTotal)
                {
                    for (int j = currentTotal; j < newMatchCount; j++)
                    {
                        var matchedCard = dto.PyramidMatchedCardsLists[i][j].ToCard();
                        string matcherName = dto.PyramidMatchedPlayerNamesLists[i][j];
                        string targetName = ResolveTargetPlayer(matcherName, oldState, dto);

                        System.Numerics.Vector2 endPos = plugin.PyramidWindow.GetPyramidCardScreenPos(i);
                        float rowScale = plugin.PyramidWindow.GetPyramidCardScale(i);
                        float w = plugin.PyramidWindow.GetPyramidCardWidth(i);
                        float h = plugin.PyramidWindow.GetPyramidCardHeight(i);

                        bool isLocalMatcher = matcherName == GameConstants.LocalPlayerName || matcherName == this.localPlayerName;

                        if (!context.IsTransitionToPyramid)
                        {
                            if (isLocalMatcher)
                            {
                                var localPlayer = oldState.Players.FirstOrDefault(p => p.IsLocal);
                                int handIndex = localPlayer?.Hand.FindIndex(c => c.Rank == matchedCard.Rank) ?? 0;
                                plugin.HandWindow.QueueDiscardAnimation(matchedCard, handIndex, i, endPos, rowScale, w, h, matcherName, targetName);
                            }
                            else
                            {
                                System.Numerics.Vector2 startPos = plugin.PyramidWindow.GetPlayerRowScreenPos(matcherName);
                                plugin.HandWindow.QueueScionDiscardAnimation(matchedCard, startPos, i, endPos, rowScale, w, h, matcherName, targetName);
                            }
                        }
                    }
                }
            }
        }

        private void PublishPyramidFlipSounds(GameStateDto dto, NetworkStateApplyContext context)
        {
            var oldState = context.OldState;
            for (int i = 0; i < 15; i++)
            {
                bool oldFlipped = i < oldState.PyramidFlipped.Length && oldState.PyramidFlipped[i];
                bool newFlipped = i < dto.PyramidFlipped.Count && dto.PyramidFlipped[i];
                if (newFlipped && !oldFlipped)
                {
                    if (!context.IsTransitionToPyramid)
                    {
                        plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
                    }
                }
            }
        }

        private void PublishBusRideEffects(GameStateDto dto, NetworkStateApplyContext context)
        {
            if (dto.ActivePhase != (int)GamePhase.BusRide)
            {
                return;
            }

            var oldState = context.OldState;
            var oldCard = oldState.BusRideCurrentCard;
            var newCardDto = dto.BusRideCurrentCard;
            if (newCardDto == null)
            {
                return;
            }

            var newCard = newCardDto.ToCard();
            if (oldCard == null)
            {
                plugin.HandWindow.TriggerBusRideDeal(newCard);
                plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
            }
            else if (oldCard.Rank != newCard.Rank || oldCard.Suit != newCard.Suit)
            {
                if (dto.BusRideCorrectStreak == 0)
                {
                    plugin.HandWindow.TriggerBusRideSlideDown(oldCard);
                    plugin.HandWindow.TriggerBusRideDeal(newCard);

                    plugin.EventBus.PublishSecondaryMessage($"{dto.BusRiderName} drinks!");
                    plugin.EventBus.PublishPlaySound(plugin.Configuration.LoseSound);
                }
                else if (dto.BusRideCorrectStreak > oldState.BusRideCorrectStreak)
                {
                    plugin.HandWindow.TriggerBusRideSlideRight(oldCard);
                    plugin.HandWindow.TriggerBusRideDeal(newCard);

                    if (dto.BusRideCorrectStreak >= plugin.Configuration.BusSize)
                    {
                        plugin.EventBus.PublishSecondaryMessage($"Victory! {dto.BusRiderName} survived the bus!");
                        plugin.EventBus.PublishPlaySound(plugin.Configuration.WinSound);
                    }
                    else
                    {
                        plugin.EventBus.PublishSecondaryMessage("Correct! Next!");
                        plugin.EventBus.PublishPlaySound(plugin.Configuration.ClickSound);
                    }
                }
            }
        }

        private static string FormatDrinkCount(int drinks)
        {
            return drinks == 1 ? "1 drink" : $"{drinks} drinks";
        }

        private string ResolveTargetPlayer(string matcherName, GameState oldState, GameStateDto newState)
        {
            foreach (var newP in newState.Players)
            {
                var oldP = oldState.Players.FirstOrDefault(p => p.Name == newP.Name);
                if (oldP != null && newP.DrinksTaken > oldP.DrinksTaken && newP.Name != matcherName)
                {
                    return newP.Name;
                }
            }
            return matcherName;
        }
    }
}

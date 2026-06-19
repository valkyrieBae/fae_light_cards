using System;
using System.Linq;

namespace FaeLightCards
{
    public partial class NetworkController
    {
        private void UpdateActionState(GameStateDto dto)
        {
            pendingMatchId = dto.RequiredAction?.Action.Equals("PlayLocalMatch", StringComparison.OrdinalIgnoreCase) == true
                ? dto.RequiredAction.MatchId
                : dto.PendingMatchId;
            pendingDrinkId = dto.RequiredAction?.Action.Equals("GivePendingDrinkToPlayer", StringComparison.OrdinalIgnoreCase) == true
                ? dto.RequiredAction.DrinkId
                : dto.PendingDrinkId;
        }

        private void ApplyScalarState(GameStateDto dto, NetworkStateApplyContext context)
        {
            var oldState = context.OldState;
            oldState.ActiveMode = (GameMode)dto.ActiveMode;
            GamePhase newPhase = (GamePhase)dto.ActivePhase;
            if (newPhase == GamePhase.TieChoice && oldState.ActivePhase == GamePhase.Pyramid)
            {
                plugin.TurnManager.DeferredNetworkPhase = GamePhase.TieChoice;
            }
            else if (newPhase == GamePhase.BusRide && oldState.ActivePhase == GamePhase.TieChoice)
            {
                bool isLocal = dto.BusRiderName == GameConstants.LocalPlayerName || dto.BusRiderName == this.localPlayerName;
                string wasWere = isLocal ? "were" : "was";
                string displayName = isLocal ? GameConstants.LocalPlayerName : dto.BusRiderName;
                plugin.GameCoordinator.QueueConveyorMessage(
                    $"{displayName} {wasWere} chosen and must Ride the Bus!",
                    completionAction: UIState.OverlayMessageCompletionAction.StartBusRide);
                // Defer setting oldState.ActivePhase to BusRide until the overlay message finishes in OnMessageFinished
            }
            else
            {
                oldState.ActivePhase = newPhase;
            }

            oldState.DealerActivePlayerName = dto.DealerActivePlayerName;
            oldState.CurrentFlipIndex = dto.CurrentFlipIndex;
            oldState.ActiveRow = dto.ActiveRow;

            int pendingSlot = CalculatePendingLocalMatchSlot(dto);
            oldState.PendingLocalMatchSlotIndex = pendingSlot;
            oldState.PendingPlaySlotIndex = pendingSlot >= 0 ? pendingSlot : null;
            oldState.PendingDrinkGiverName = dto.PendingDrinkGiverName;
            oldState.PendingDrinkAmount = dto.PendingDrinkAmount;
            oldState.PendingDrinkId = dto.PendingDrinkId;
            oldState.BusRideCurrentCard = dto.BusRideCurrentCard?.ToCard();
            oldState.BusRideCorrectStreak = dto.BusRideCorrectStreak;
            oldState.BusRiderName = dto.BusRiderName;

            context.HasPendingDrinkTarget = !string.IsNullOrWhiteSpace(dto.PendingDrinkGiverName)
                                            && dto.PendingDrinkAmount > 0;
        }

        private int CalculatePendingLocalMatchSlot(GameStateDto dto)
        {
            if (dto.RequiredAction?.Action.Equals("PlayLocalMatch", StringComparison.OrdinalIgnoreCase) == true &&
                dto.RequiredAction.SlotIndex.HasValue)
            {
                return dto.RequiredAction.SlotIndex.Value;
            }

            if (dto.ActivePhase != (int)GamePhase.Pyramid)
            {
                return -1;
            }

            var localPlayer = dto.Players.FirstOrDefault(p => p.IsLocal);
            if (localPlayer == null || localPlayer.IsDealer)
            {
                return -1;
            }

            for (int i = 0; i < 15; i++)
            {
                if (i < dto.PyramidFlipped.Count && dto.PyramidFlipped[i])
                {
                    int requiredCount = i < dto.PyramidRequiredMatchersLists.Count ? dto.PyramidRequiredMatchersLists[i].Count(name =>
                        string.Equals(name, this.localPlayerName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, GameConstants.LocalPlayerName, StringComparison.OrdinalIgnoreCase)) : 0;

                    int matchedCount = i < dto.PyramidMatchedPlayerNamesLists.Count ? dto.PyramidMatchedPlayerNamesLists[i].Count(name =>
                        string.Equals(name, this.localPlayerName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, GameConstants.LocalPlayerName, StringComparison.OrdinalIgnoreCase)) : 0;

                    if (requiredCount > matchedCount)
                    {
                        CardDto? pyrCard = i < dto.Pyramid.Count ? dto.Pyramid[i] : null;
                        if (pyrCard != null && localPlayer.Hand.Any(c => c.Rank == pyrCard.Rank))
                        {
                            return i;
                        }
                    }
                }
            }

            return -1;
        }

        private void ApplyDealerTurnState(GameStateDto dto, NetworkStateApplyContext context)
        {
            var oldState = context.OldState;
            plugin.TurnManager.DealerNpcHasGuessed = dto.DealerHasGuessed;
            plugin.TurnManager.DealerCurrentNpcGuess = dto.CurrentPlayerGuess;

            if (dto.ActiveMode == (int)GameMode.Dealer && dto.ActivePhase == (int)GamePhase.Accumulation)
            {
                plugin.TurnManager.DealerNeedNextPlayer = false;
                var nonDealers = dto.Players.Where(p => !p.IsDealer).ToList();
                if (nonDealers.Count > 0)
                {
                    int totalCards = nonDealers.Sum(p => p.Hand.Count);
                    int k = nonDealers.Count;
                    var activePlayer = nonDealers.FirstOrDefault(p => p.Name == dto.DealerActivePlayerName);
                    if (activePlayer != null)
                    {
                        int activeIdx = nonDealers.IndexOf(activePlayer);
                        if (k == 1)
                        {
                            var oldActivePlayer = oldState.Players.FirstOrDefault(p => p.Name == dto.DealerActivePlayerName);
                            plugin.TurnManager.DealerNeedNextPlayer = !context.HasPendingDrinkTarget
                                                                       && oldActivePlayer != null
                                                                       && activePlayer.Hand.Count > oldActivePlayer.Hand.Count;
                        }
                        else
                        {
                            plugin.TurnManager.DealerNeedNextPlayer = !context.HasPendingDrinkTarget
                                                                       && (totalCards % k) == ((activeIdx + 1) % k);
                        }
                    }
                }
            }
            else
            {
                plugin.TurnManager.DealerNeedNextPlayer = false;
            }
        }

        private void PublishNetworkStatusMessages(GameStateDto dto, NetworkStateApplyContext context)
        {
            if (dto.ActiveMode != (int)GameMode.Dealer)
            {
                return;
            }

            if (dto.ActivePhase == (int)GamePhase.Accumulation)
            {
                PublishAccumulationStatusMessage(dto, context);
            }
            else if (dto.ActivePhase == (int)GamePhase.BusRide)
            {
                PublishBusRideStatusMessage(dto);
            }
        }

        private void PublishAccumulationStatusMessage(GameStateDto dto, NetworkStateApplyContext context)
        {
            if (context.HasPendingDrinkTarget)
            {
                plugin.EventBus.PublishRightSideMessage(string.Empty);
            }
            else if (plugin.TurnManager.DealerNeedNextPlayer)
            {
                bool isLocalDealer = plugin.AppState.ChosenGameMode == GameMode.Dealer
                                     || dto.Players.Any(p => p.IsDealer
                                                             && (p.IsLocal || string.Equals(p.Name, this.localPlayerName, StringComparison.OrdinalIgnoreCase)));
                string message = isLocalDealer ? string.Empty : "Dealer's turn";
                plugin.EventBus.PublishRightSideMessage(message);
            }
            else if (dto.DealerHasGuessed)
            {
                var activePlayer = dto.Players.FirstOrDefault(p => p.Name == dto.DealerActivePlayerName);
                int round = activePlayer?.Hand?.Count ?? 0;
                if (round >= 0 && round < plugin.RulesEngine.GuessingStages.Length)
                {
                    var stage = plugin.RulesEngine.GuessingStages[round];
                    if (dto.CurrentPlayerGuess >= 0 && dto.CurrentPlayerGuess < stage.Options.Count)
                    {
                        string optionLabel = stage.Options[dto.CurrentPlayerGuess].Label;
                        plugin.EventBus.PublishRightSideMessage($"{dto.DealerActivePlayerName} guessed {optionLabel}!");
                    }
                }
            }
            else
            {
                var nonDealers = dto.Players.Where(p => !p.IsDealer).ToList();
                int minHand = nonDealers.Count > 0 ? nonDealers.Min(p => p.Hand.Count) : 0;
                var activePlayer = dto.Players.FirstOrDefault(p => p.Name == dto.DealerActivePlayerName);

                if (activePlayer != null && activePlayer.Hand.Count > minHand)
                {
                    plugin.EventBus.PublishRightSideMessage("Dealer's turn");
                }
                else
                {
                    bool isNpc = activePlayer != null ? (!activePlayer.IsHuman && !activePlayer.IsLocal) : (dto.DealerActivePlayerName != this.localPlayerName && dto.DealerActivePlayerName != GameConstants.LocalPlayerName);
                    if (isNpc)
                    {
                        plugin.EventBus.PublishRightSideMessage($"{dto.DealerActivePlayerName} is thinking...");
                    }
                    else
                    {
                        plugin.EventBus.PublishRightSideMessage(string.Empty);
                    }
                }
            }
        }

        private void PublishBusRideStatusMessage(GameStateDto dto)
        {
            if (dto.BusRideCurrentCard == null)
            {
                bool isLocalDealer = plugin.AppState.ChosenGameMode == GameMode.Dealer
                                     || dto.Players.Any(p => p.IsDealer
                                                             && (p.IsLocal || string.Equals(p.Name, this.localPlayerName, StringComparison.OrdinalIgnoreCase)));
                plugin.EventBus.PublishRightSideMessage(isLocalDealer ? string.Empty : "Dealer's turn");
            }
            else if (dto.DealerHasGuessed)
            {
                string guessedStr = dto.CurrentPlayerGuess == 0 ? "higher" : "lower";
                plugin.EventBus.PublishRightSideMessage($"{dto.BusRiderName} guessed {guessedStr}!");
            }
            else
            {
                var targetPlayer = dto.Players.FirstOrDefault(p => p.Name == dto.BusRiderName);
                bool isNpc = targetPlayer != null ? (!targetPlayer.IsHuman && !targetPlayer.IsLocal) : (dto.BusRiderName != this.localPlayerName && dto.BusRiderName != GameConstants.LocalPlayerName);
                if (isNpc)
                {
                    plugin.EventBus.PublishRightSideMessage($"{dto.BusRiderName} is thinking...");
                }
                else
                {
                    plugin.EventBus.PublishRightSideMessage(string.Empty);
                }
            }
        }

        private void CopyActionLog(GameStateDto dto, NetworkStateApplyContext context)
        {
            var oldState = context.OldState;
            oldState.ActionLog.Clear();
            if (dto.ActionLog != null)
            {
                oldState.ActionLog.AddRange(dto.ActionLog);
            }
        }

        private void CopyPlayers(GameStateDto dto, NetworkStateApplyContext context)
        {
            var oldState = context.OldState;
            oldState.Players.Clear();
            foreach (var dtoPlayer in dto.Players)
            {
                var p = new Player
                {
                    Name = dtoPlayer.Name,
                    DrinksGiven = dtoPlayer.DrinksGiven,
                    DrinksTaken = dtoPlayer.DrinksTaken,
                    IsLocal = dtoPlayer.IsLocal,
                    IsDealer = dtoPlayer.IsDealer,
                    IsHuman = dtoPlayer.IsHuman,
                    IsEligibleForCurrentBusRide = dtoPlayer.IsEligibleForCurrentBusRide
                };
                foreach (var cDto in dtoPlayer.Hand)
                {
                    p.Hand.Add(cDto.ToCard());
                }
                oldState.Players.Add(p);
            }
        }

        private void CopyPyramidState(GameStateDto dto, NetworkStateApplyContext context)
        {
            var oldState = context.OldState;
            oldState.Pyramid.Clear();
            foreach (var cDto in dto.Pyramid)
            {
                oldState.Pyramid.Add(cDto!.ToCard());
            }

            for (int i = 0; i < 15; i++)
            {
                oldState.PyramidFlipped[i] = i < dto.PyramidFlipped.Count && dto.PyramidFlipped[i];

                bool shouldSyncMatchedLists = true;
                if (dto.ActivePhase == (int)GamePhase.Pyramid && !context.IsTransitionToPyramid)
                {
                    int oldMatchCount = i < oldState.PyramidMatchedCardsLists.Length ? oldState.PyramidMatchedCardsLists[i].Count : 0;
                    int newMatchCount = i < dto.PyramidMatchedCardsLists.Count ? dto.PyramidMatchedCardsLists[i].Count : 0;
                    if (newMatchCount >= oldMatchCount)
                    {
                        shouldSyncMatchedLists = false;
                    }
                }

                if (shouldSyncMatchedLists)
                {
                    oldState.PyramidMatchedCardsLists[i].Clear();
                    if (i < dto.PyramidMatchedCardsLists.Count)
                    {
                        foreach (var cDto in dto.PyramidMatchedCardsLists[i])
                        {
                            oldState.PyramidMatchedCardsLists[i].Add(cDto.ToCard());
                        }
                    }

                    oldState.PyramidMatchedPlayerNamesLists[i].Clear();
                    if (i < dto.PyramidMatchedPlayerNamesLists.Count)
                    {
                        oldState.PyramidMatchedPlayerNamesLists[i].AddRange(dto.PyramidMatchedPlayerNamesLists[i]);
                    }

                    oldState.PyramidMatchedCardsRotationsLists[i].Clear();
                    if (i < dto.PyramidMatchedCardsRotationsLists.Count)
                    {
                        oldState.PyramidMatchedCardsRotationsLists[i].AddRange(dto.PyramidMatchedCardsRotationsLists[i]);
                    }
                }

                oldState.PyramidRequiredMatchers[i].Clear();
                if (dto.PyramidRequiredMatchersLists != null && i < dto.PyramidRequiredMatchersLists.Count)
                {
                    oldState.PyramidRequiredMatchers[i].AddRange(dto.PyramidRequiredMatchersLists[i]);
                }
            }
        }

        private void CopyBusRideDeck(GameStateDto dto, NetworkStateApplyContext context)
        {
            var oldState = context.OldState;
            oldState.BusRideDeck.Clear();
            foreach (var cDto in dto.BusRideDeck)
            {
                oldState.BusRideDeck.Add(cDto.ToCard());
            }
        }
    }
}

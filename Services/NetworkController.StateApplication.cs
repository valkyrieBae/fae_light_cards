using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FaeLightCards
{
    public partial class NetworkController
    {
        private void ProcessMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                if (!root.TryGetProperty("event", out var eventProp)) return;

                string eventType = eventProp.GetString() ?? string.Empty;
                if (eventType.Equals("RoomSnapshot", StringComparison.OrdinalIgnoreCase))
                {
                    var dataElement = root.GetProperty("data");
                    var stateElement = dataElement.GetProperty("snapshot");
                    var stateDto = JsonSerializer.Deserialize<GameStateDto>(stateElement.GetRawText(), jsonOptions);
                    if (stateDto != null)
                    {
                        ApplyStateIfValid(stateDto);
                    }
                }
                else if (eventType.Equals("GameEvent", StringComparison.OrdinalIgnoreCase))
                {
                    var dataElement = root.GetProperty("data");
                    if (dataElement.TryGetProperty("eventType", out var eventTypeElement))
                    {
                        Plugin.Log.Debug($"Network event: {eventTypeElement.GetString() ?? "unknown"}");
                    }

                    var stateElement = dataElement.GetProperty("snapshot");
                    var stateDto = JsonSerializer.Deserialize<GameStateDto>(stateElement.GetRawText(), jsonOptions);
                    if (stateDto != null)
                    {
                        ApplyStateIfValid(stateDto);
                    }
                }
                else if (eventType.Equals("Welcome", StringComparison.OrdinalIgnoreCase))
                {
                    if (root.TryGetProperty("data", out var dataElement) &&
                        dataElement.TryGetProperty("roomId", out var roomIdElement))
                    {
                        plugin.AppState.CurrentRoomId = roomIdElement.GetString()?.Trim().ToUpperInvariant() ?? plugin.AppState.CurrentRoomId;
                        if (dataElement.TryGetProperty("resumeToken", out var resumeTokenElement))
                        {
                            resumeToken = resumeTokenElement.GetString();
                        }

                        if (dataElement.TryGetProperty("capabilities", out var capabilitiesElement) &&
                            capabilitiesElement.TryGetProperty("debugTools", out var debugToolsElement))
                        {
                            serverDebugToolsEnabled = debugToolsElement.GetBoolean();
                            plugin.GameState.NetworkDebugToolsEnabled = serverDebugToolsEnabled;
                        }
                    }

                    Plugin.Log.Info("Joined room successfully.");
                    plugin.AppState.ConnectionFailureMessage = string.Empty;
                    plugin.AppState.ActiveConnectionMode = ConnectionMode.Networked;
                }
                else if (eventType.Equals("Error", StringComparison.OrdinalIgnoreCase))
                {
                    var dataElement = root.GetProperty("data");
                    string errorMsg = dataElement.GetProperty("message").GetString() ?? "Unknown error";
                    plugin.EventBus.PublishSecondaryMessage($"Error: {errorMsg}");
                    Plugin.Log.Warning($"Server returned error: {errorMsg}");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Error processing WebSocket message: {e}");
            }
        }
        private void ApplyStateIfValid(GameStateDto dto)
        {
            if (!TryValidateCards(dto, out var validationError))
            {
                Plugin.Log.Warning($"Skipped network snapshot with invalid card payload: {validationError}");
                RequestResyncAfterBadSnapshot();
                return;
            }

            ApplyState(dto);
        }
        private void RequestResyncAfterBadSnapshot()
        {
            var now = DateTime.UtcNow;
            if (now - lastBadSnapshotResyncUtc < TimeSpan.FromSeconds(1))
            {
                return;
            }

            lastBadSnapshotResyncUtc = now;
            QueueAction("RequestResync");
        }
        private void ApplyState(GameStateDto dto)
        {
            UpdateActionState(dto);

            var oldState = plugin.GameState;
            bool isTransitionToPyramid = oldState.ActivePhase != GamePhase.Pyramid && dto.ActivePhase == (int)GamePhase.Pyramid;

            if (isTransitionToPyramid)
            {
                plugin.HandWindow.ClearAnimationsAndParticles();
            }

            bool handledAccumulationOutcome = PublishAccumulationOutcome(dto, oldState);

            // 1. Trigger Deal animations if hand sizes increase
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
                        if (!isTransitionToPyramid && dto.ActivePhase == (int)GamePhase.Accumulation && shouldAnimate)
                        {
                            // Trigger deal animation on main UI window
                            plugin.EventBus.PublishCardDealt(newCard, j);

                            if (!handledAccumulationOutcome)
                            {
                                plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
                            }
                        }
                    }
                }
            }

            // 2. Trigger Match animations if cards matched lists grow
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

                        if (!isTransitionToPyramid)
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

            // 3. Trigger Pyramid card flip sounds
            for (int i = 0; i < 15; i++)
            {
                bool oldFlipped = i < oldState.PyramidFlipped.Length && oldState.PyramidFlipped[i];
                bool newFlipped = i < dto.PyramidFlipped.Count && dto.PyramidFlipped[i];
                if (newFlipped && !oldFlipped)
                {
                    if (!isTransitionToPyramid)
                    {
                        plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
                    }
                }
            }

            // 3.5. Trigger Bus Ride card flip/slide animations
            if (dto.ActivePhase == (int)GamePhase.BusRide)
            {
                var oldCard = oldState.BusRideCurrentCard;
                var newCardDto = dto.BusRideCurrentCard;
                if (newCardDto != null)
                {
                    var newCard = newCardDto.ToCard();
                    if (oldCard == null)
                    {
                        // Initial deal
                        plugin.HandWindow.TriggerBusRideDeal(newCard);
                        plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);
                    }
                    else if (oldCard.Rank != newCard.Rank || oldCard.Suit != newCard.Suit)
                    {
                        // New card dealt
                        if (dto.BusRideCorrectStreak == 0)
                        {
                            // Reset/Wrong guess
                            plugin.HandWindow.TriggerBusRideSlideDown(oldCard);
                            plugin.HandWindow.TriggerBusRideDeal(newCard);

                            plugin.EventBus.PublishSecondaryMessage($"{dto.BusRiderName} drinks!");
                            plugin.EventBus.PublishPlaySound(plugin.Configuration.LoseSound);
                        }
                        else if (dto.BusRideCorrectStreak > oldState.BusRideCorrectStreak)
                        {
                            // Correct guess
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
            }

            // 4. Copy standard properties
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
            int pendingSlot = -1;
            if (dto.RequiredAction?.Action.Equals("PlayLocalMatch", StringComparison.OrdinalIgnoreCase) == true &&
                dto.RequiredAction.SlotIndex.HasValue)
            {
                pendingSlot = dto.RequiredAction.SlotIndex.Value;
            }
            else if (dto.ActivePhase == (int)GamePhase.Pyramid)
            {
                var localPlayer = dto.Players.FirstOrDefault(p => p.IsLocal);
                if (localPlayer != null && !localPlayer.IsDealer)
                {
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
                                    pendingSlot = i;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            oldState.PendingLocalMatchSlotIndex = pendingSlot;
            oldState.PendingPlaySlotIndex = pendingSlot >= 0 ? pendingSlot : null;
            oldState.PendingDrinkGiverName = dto.PendingDrinkGiverName;
            oldState.PendingDrinkAmount = dto.PendingDrinkAmount;
            oldState.PendingDrinkId = dto.PendingDrinkId;
            oldState.BusRideCurrentCard = dto.BusRideCurrentCard?.ToCard();
            oldState.BusRideCorrectStreak = dto.BusRideCorrectStreak;
            oldState.BusRiderName = dto.BusRiderName;

            // Mapping dealer guess state
            plugin.TurnManager.DealerNpcHasGuessed = dto.DealerHasGuessed;
            plugin.TurnManager.DealerCurrentNpcGuess = dto.CurrentPlayerGuess;

            bool hasPendingDrinkTarget = !string.IsNullOrWhiteSpace(dto.PendingDrinkGiverName)
                                         && dto.PendingDrinkAmount > 0;

            // Determine if dealer needs to advance player
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
                            plugin.TurnManager.DealerNeedNextPlayer = !hasPendingDrinkTarget
                                                                       && oldActivePlayer != null
                                                                       && activePlayer.Hand.Count > oldActivePlayer.Hand.Count;
                        }
                        else
                        {
                            plugin.TurnManager.DealerNeedNextPlayer = !hasPendingDrinkTarget
                                                                       && (totalCards % k) == ((activeIdx + 1) % k);
                        }
                    }
                }
            }
            else
            {
                plugin.TurnManager.DealerNeedNextPlayer = false;
            }

            // Set the right-side message dynamically in Networked Mode
            if (dto.ActiveMode == (int)GameMode.Dealer)
            {
                if (dto.ActivePhase == (int)GamePhase.Accumulation)
                {
                    if (hasPendingDrinkTarget)
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
                else if (dto.ActivePhase == (int)GamePhase.BusRide)
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
            }

            // 5. Copy ActionLog
            oldState.ActionLog.Clear();
            if (dto.ActionLog != null)
            {
                oldState.ActionLog.AddRange(dto.ActionLog);
            }

            // 6. Copy Players list
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

            // 7. Copy Pyramid list
            oldState.Pyramid.Clear();
            foreach (var cDto in dto.Pyramid)
            {
                oldState.Pyramid.Add(cDto!.ToCard());
            }

            for (int i = 0; i < 15; i++)
            {
                oldState.PyramidFlipped[i] = i < dto.PyramidFlipped.Count && dto.PyramidFlipped[i];

                bool shouldSyncMatchedLists = true;
                if (dto.ActivePhase == (int)GamePhase.Pyramid && !isTransitionToPyramid)
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

            // 8. Copy BusRide deck
            oldState.BusRideDeck.Clear();
            foreach (var cDto in dto.BusRideDeck)
            {
                oldState.BusRideDeck.Add(cDto.ToCard());
            }
        }
        private void UpdateActionState(GameStateDto dto)
        {
            pendingMatchId = dto.RequiredAction?.Action.Equals("PlayLocalMatch", StringComparison.OrdinalIgnoreCase) == true
                ? dto.RequiredAction.MatchId
                : dto.PendingMatchId;
            pendingDrinkId = dto.RequiredAction?.Action.Equals("GivePendingDrinkToPlayer", StringComparison.OrdinalIgnoreCase) == true
                ? dto.RequiredAction.DrinkId
                : dto.PendingDrinkId;
        }
        private bool PublishAccumulationOutcome(GameStateDto dto, GameState oldState)
        {
            if (dto.ActivePhase != (int)GamePhase.Accumulation)
            {
                return false;
            }

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
            return matcherName; // fallback
        }
    }
}

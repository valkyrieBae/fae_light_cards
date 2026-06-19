using System;
using System.Collections.Generic;

namespace FaeLightCards
{
    public partial class NetworkController
    {
        private static bool TryValidateCards(GameStateDto dto, out string error)
        {
            for (int playerIndex = 0; playerIndex < dto.Players.Count; playerIndex++)
            {
                var player = dto.Players[playerIndex];
                for (int cardIndex = 0; cardIndex < player.Hand.Count; cardIndex++)
                {
                    if (!TryValidateCard(player.Hand[cardIndex], $"players[{playerIndex}].hand[{cardIndex}]", out error))
                    {
                        return false;
                    }
                }
            }

            for (int cardIndex = 0; cardIndex < dto.Pyramid.Count; cardIndex++)
            {
                if (!TryValidateCard(dto.Pyramid[cardIndex], $"pyramid[{cardIndex}]", out error))
                {
                    return false;
                }
            }

            for (int slotIndex = 0; slotIndex < dto.PyramidMatchedCardsLists.Count; slotIndex++)
            {
                var matchedCards = dto.PyramidMatchedCardsLists[slotIndex];
                for (int cardIndex = 0; cardIndex < matchedCards.Count; cardIndex++)
                {
                    if (!TryValidateCard(matchedCards[cardIndex], $"pyramidMatchedCardsLists[{slotIndex}][{cardIndex}]", out error))
                    {
                        return false;
                    }
                }
            }

            if (dto.BusRideCurrentCard != null && !TryValidateCard(dto.BusRideCurrentCard, "busRideCurrentCard", out error))
            {
                return false;
            }

            for (int cardIndex = 0; cardIndex < dto.BusRideDeck.Count; cardIndex++)
            {
                if (!TryValidateCard(dto.BusRideDeck[cardIndex], $"busRideDeck[{cardIndex}]", out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateCard(CardDto? cardDto, string context, out string error)
        {
            if (cardDto == null)
            {
                error = $"{context} is null";
                return false;
            }

            if (!cardDto.TryToCard(out _, out var cardError))
            {
                error = $"{context}: {cardError}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private sealed record OutboundGameAction(string ActionType, object? Data);

        // Nested DTO mappings for JSON parsing
        private class CardDto
        {
            public int Suit { get; set; }
            public int Rank { get; set; }

            public bool TryToCard(out Card? card, out string error)
            {
                if (!Enum.IsDefined<Suit>((Suit)Suit))
                {
                    card = null;
                    error = $"invalid suit {Suit}, rank {Rank}";
                    return false;
                }

                if (!Enum.IsDefined<Rank>((Rank)Rank))
                {
                    card = null;
                    error = $"suit {Suit}, invalid rank {Rank}";
                    return false;
                }

                if (Card.TryCreate((Suit)Suit, (Rank)Rank, out card))
                {
                    error = string.Empty;
                    return true;
                }

                card = null;
                error = "card value is not valid";
                return false;
            }

            public Card ToCard()
            {
                if (TryToCard(out var card, out var error) && card != null)
                {
                    return card;
                }

                throw new InvalidOperationException($"Invalid network card payload: {error}");
            }

        }

        private class PlayerDto
        {
            public string Name { get; set; } = string.Empty;
            public List<CardDto> Hand { get; set; } = new();
            public int HandCount { get; set; }
            public int DrinksGiven { get; set; }
            public int DrinksTaken { get; set; }
            public bool IsLocal { get; set; }
            public bool IsDealer { get; set; }
            public bool IsHuman { get; set; }
            public bool IsEligibleForCurrentBusRide { get; set; } = true;
        }

        private class GameStateDto
        {
            public long Revision { get; set; }
            public List<PlayerDto> Players { get; set; } = new();
            public List<string> ActionLog { get; set; } = new();
            public int ActiveMode { get; set; }
            public string DealerActivePlayerName { get; set; } = string.Empty;
            public List<CardDto?> Pyramid { get; set; } = new();
            public List<bool> PyramidFlipped { get; set; } = new();
            public List<List<CardDto>> PyramidMatchedCardsLists { get; set; } = new();
            public List<List<string>> PyramidMatchedPlayerNamesLists { get; set; } = new();
            public List<List<string>> PyramidRequiredMatchersLists { get; set; } = new();
            public List<List<float>> PyramidMatchedCardsRotationsLists { get; set; } = new();
            public int CurrentFlipIndex { get; set; }
            public int ActiveRow { get; set; }
            public int ActivePhase { get; set; }
            public int PendingLocalMatchSlotIndex { get; set; }
            public string? PendingMatchId { get; set; }
            public string PendingDrinkGiverName { get; set; } = string.Empty;
            public int PendingDrinkAmount { get; set; }
            public string? PendingDrinkId { get; set; }
            public CardDto? BusRideCurrentCard { get; set; }
            public int BusRideCorrectStreak { get; set; }
            public List<CardDto> BusRideDeck { get; set; } = new();
            public string BusRiderName { get; set; } = string.Empty;
            public bool DealerHasGuessed { get; set; }
            public int CurrentPlayerGuess { get; set; }
            public List<ActionDto> AvailableActions { get; set; } = new();
            public ActionDto? RequiredAction { get; set; }
        }

        private class ActionDto
        {
            public string Action { get; set; } = string.Empty;
            public string? MatchId { get; set; }
            public string? DrinkId { get; set; }
            public int? SlotIndex { get; set; }
            public int? Amount { get; set; }
            public List<int> Choices { get; set; } = new();
            public List<string> TargetPlayers { get; set; } = new();
        }
    }
}

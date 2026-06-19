using System;

namespace FaeLightCards
{
    public enum Suit
    {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }

    public enum Rank
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }

    public record Card(Suit Suit, Rank Rank)
    {
        public static bool TryCreate(Suit suit, Rank rank, out Card? card)
        {
            if (!Enum.IsDefined<Suit>(suit) || !Enum.IsDefined<Rank>(rank))
            {
                card = null;
                return false;
            }

            card = new Card(suit, rank);
            return true;
        }

        public static Card CreateValidated(Suit suit, Rank rank)
        {
            if (TryCreate(suit, rank, out var card) && card != null)
            {
                return card;
            }

            throw new ArgumentOutOfRangeException(nameof(rank), $"Invalid card value: {suit}/{rank}");
        }

        public string GetFileName()
        {
            string suitStr = Suit.ToString().ToLower();
            string rankStr = Rank switch
            {
                Rank.Ace => "A",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                _ => ((int)Rank).ToString()
            };
            return $"{suitStr}_{rankStr}.png";
        }

        public string GetDisplayName()
        {
            string rankStr = Rank switch
            {
                Rank.Ace => "A",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                _ => ((int)Rank).ToString()
            };

            char suitChar = Suit switch
            {
                Suit.Clubs => '♣',
                Suit.Diamonds => '♦',
                Suit.Hearts => '♥',
                Suit.Spades => '♠',
                _ => '?'
            };

            return $"{rankStr}{suitChar}";
        }
    }
}

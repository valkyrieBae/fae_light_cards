using System;
using System.Collections.Generic;
using System.Numerics;

namespace FaeLightCards
{
    public class ButtonTheme
    {
        public Vector4 FillColor { get; }
        public Vector4 OutlineColor { get; }
        public Vector4 TextColor { get; }
        public Vector4 TextOutlineColor { get; }

        public ButtonTheme(Vector4 fillColor, Vector4 outlineColor, Vector4 textColor, Vector4 textOutlineColor)
        {
            FillColor = fillColor;
            OutlineColor = outlineColor;
            TextColor = textColor;
            TextOutlineColor = textOutlineColor;
        }

        public Vector4 GetFill(float opacity) => new Vector4(FillColor.X, FillColor.Y, FillColor.Z, FillColor.W * opacity);
        public Vector4 GetOutline(float opacity) => new Vector4(OutlineColor.X, OutlineColor.Y, OutlineColor.Z, OutlineColor.W * opacity);
        public Vector4 GetText(float opacity) => new Vector4(TextColor.X, TextColor.Y, TextColor.Z, TextColor.W * opacity);
        public Vector4 GetTextOutline(float opacity) => new Vector4(TextOutlineColor.X, TextOutlineColor.Y, TextOutlineColor.Z, TextOutlineColor.W * opacity);
    }

    public class GuessOption
    {
        public string Label { get; }
        public ButtonTheme Theme { get; }

        public GuessOption(string label, ButtonTheme theme)
        {
            Label = label;
            Theme = theme;
        }
    }

    public interface IGuessingStage
    {
        string PromptText { get; }
        int DrinksCount { get; }
        IReadOnlyList<GuessOption> Options { get; }
        bool EvaluateGuess(Card nextCard, int pickedOptionIndex, List<Card> currentHand);
    }

    public class RedOrBlackStage : IGuessingStage
    {
        public string PromptText => "Red or Black?";
        public int DrinksCount => 1;

        public IReadOnlyList<GuessOption> Options { get; } = new List<GuessOption>
        {
            new GuessOption("Red", new ButtonTheme(
                new Vector4(0.85f, 0.15f, 0.2f, 1.0f), // Red fill
                new Vector4(0f, 0f, 0f, 1.0f),         // Black outline
                new Vector4(1f, 1f, 1f, 1.0f),         // White text
                new Vector4(0f, 0f, 0f, 1.0f)          // Black text outline
            )),
            new GuessOption("Black", new ButtonTheme(
                new Vector4(0.08f, 0.08f, 0.1f, 1.0f), // Black fill
                new Vector4(0.6f, 0.1f, 0.15f, 1.0f),  // Dark red outline
                new Vector4(1f, 1f, 1f, 1.0f),         // White text
                new Vector4(0.6f, 0.1f, 0.15f, 1.0f)   // Dark red text outline
            ))
        };

        public bool EvaluateGuess(Card nextCard, int pickedOptionIndex, List<Card> currentHand)
        {
            bool isCardRed = nextCard.Suit == Suit.Hearts || nextCard.Suit == Suit.Diamonds;
            // Option 0 is "Red", Option 1 is "Black"
            return pickedOptionIndex == 0 ? isCardRed : !isCardRed;
        }
    }

    public class HigherOrLowerStage : IGuessingStage
    {
        public string PromptText => "Higher or Lower?";
        public int DrinksCount => 2;

        public IReadOnlyList<GuessOption> Options { get; } = new List<GuessOption>
        {
            new GuessOption("Higher", new ButtonTheme(
                new Vector4(0.2f, 0.4f, 0.6f, 1.0f),   // Slate blue fill
                new Vector4(0.1f, 0.2f, 0.3f, 1.0f),   // Dark blue outline
                new Vector4(1f, 1f, 1f, 1.0f),         // White text
                new Vector4(0.1f, 0.2f, 0.3f, 1.0f)    // Dark blue text outline
            )),
            new GuessOption("Lower", new ButtonTheme(
                new Vector4(0.25f, 0.25f, 0.3f, 1.0f), // Charcoal fill
                new Vector4(0.15f, 0.15f, 0.2f, 1.0f), // Dark charcoal outline
                new Vector4(1f, 1f, 1f, 1.0f),         // White text
                new Vector4(0.15f, 0.15f, 0.2f, 1.0f)  // Dark charcoal text outline
            ))
        };

        public bool EvaluateGuess(Card nextCard, int pickedOptionIndex, List<Card> currentHand)
        {
            if (currentHand.Count == 0) return false;
            var previousCard = currentHand[currentHand.Count - 1];

            // Option 0 is "Higher", Option 1 is "Lower"
            if (pickedOptionIndex == 0)
            {
                return nextCard.Rank > previousCard.Rank;
            }
            else
            {
                return nextCard.Rank < previousCard.Rank;
            }
        }
    }

    public class InsideOrOutsideStage : IGuessingStage
    {
        public string PromptText => "Inside or Outside?";
        public int DrinksCount => 3;

        public IReadOnlyList<GuessOption> Options { get; } = new List<GuessOption>
        {
            new GuessOption("Inside", new ButtonTheme(
                new Vector4(0.2f, 0.5f, 0.4f, 1.0f),   // Slate green fill
                new Vector4(0.1f, 0.3f, 0.2f, 1.0f),   // Dark green outline
                new Vector4(1f, 1f, 1f, 1.0f),         // White text
                new Vector4(0.1f, 0.3f, 0.2f, 1.0f)    // Dark green text outline
            )),
            new GuessOption("Outside", new ButtonTheme(
                new Vector4(0.4f, 0.3f, 0.5f, 1.0f),   // Soft purple/violet fill
                new Vector4(0.2f, 0.15f, 0.3f, 1.0f),  // Dark purple outline
                new Vector4(1f, 1f, 1f, 1.0f),         // White text
                new Vector4(0.2f, 0.15f, 0.3f, 1.0f)   // Dark purple text outline
            ))
        };

        public bool EvaluateGuess(Card nextCard, int pickedOptionIndex, List<Card> currentHand)
        {
            if (currentHand.Count < 2) return false;
            var card1 = currentHand[0];
            var card2 = currentHand[1];

            int val1 = (int)card1.Rank;
            int val2 = (int)card2.Rank;
            int nextVal = (int)nextCard.Rank;

            if (nextVal == val1 || nextVal == val2)
            {
                return false;
            }

            int min = Math.Min(val1, val2);
            int max = Math.Max(val1, val2);

            bool isInside = nextVal > min && nextVal < max;

            // Option 0 is "Inside", Option 1 is "Outside"
            return pickedOptionIndex == 0 ? isInside : !isInside;
        }
    }

    public class GuessTheSuitStage : IGuessingStage
    {
        public string PromptText => "Guess the Suit?";
        public int DrinksCount => 4;

        public IReadOnlyList<GuessOption> Options { get; } = new List<GuessOption>
        {
            new GuessOption("♣", new ButtonTheme(
                new Vector4(0.12f, 0.12f, 0.14f, 1.0f), // Clubs charcoal fill
                new Vector4(0.4f, 0.4f, 0.4f, 1.0f),   // Medium gray outline
                new Vector4(0.85f, 0.85f, 0.85f, 1.0f),// Off-white text
                new Vector4(0.2f, 0.2f, 0.2f, 1.0f)    // Dark text outline
            )),
            new GuessOption("♦", new ButtonTheme(
                new Vector4(0.80f, 0.15f, 0.2f, 1.0f),  // Vibrant red fill
                new Vector4(0.4f, 0.05f, 0.08f, 1.0f), // Dark red outline
                new Vector4(1f, 1f, 1f, 1.0f),         // White text
                new Vector4(0.4f, 0.05f, 0.08f, 1.0f)  // Dark red text outline
            )),
            new GuessOption("♥", new ButtonTheme(
                new Vector4(0.80f, 0.15f, 0.2f, 1.0f),  // Vibrant red fill
                new Vector4(0.4f, 0.05f, 0.08f, 1.0f), // Dark red outline
                new Vector4(1f, 1f, 1f, 1.0f),         // White text
                new Vector4(0.4f, 0.05f, 0.08f, 1.0f)  // Dark red text outline
            )),
            new GuessOption("♠", new ButtonTheme(
                new Vector4(0.12f, 0.12f, 0.14f, 1.0f), // Spades charcoal fill
                new Vector4(0.4f, 0.4f, 0.4f, 1.0f),   // Medium gray outline
                new Vector4(0.85f, 0.85f, 0.85f, 1.0f),// Off-white text
                new Vector4(0.2f, 0.2f, 0.2f, 1.0f)    // Dark text outline
            ))
        };

        public bool EvaluateGuess(Card nextCard, int pickedOptionIndex, List<Card> currentHand)
        {
            Suit targetSuit = pickedOptionIndex switch
            {
                0 => Suit.Clubs,
                1 => Suit.Diamonds,
                2 => Suit.Hearts,
                3 => Suit.Spades,
                _ => Suit.Clubs
            };
            return nextCard.Suit == targetSuit;
        }
    }


}

using System;
using System.Linq;

namespace FaeLightCards
{
    public class NpcAiService
    {
        private readonly Random rng = new Random();

        public float GetThinkingTime(float baseTime, float variance)
        {
            return baseTime + (float)rng.NextDouble() * variance;
        }

        public int DetermineAccumulationGuess(Player npc, IGuessingStage stage)
        {
            if (stage is RedOrBlackStage)
            {
                return rng.Next(2);
            }
            else if (stage is HigherOrLowerStage)
            {
                if (npc.Hand.Count > 0)
                {
                    var prev = npc.Hand[0];
                    return prev.Rank <= Rank.Eight ? 0 : 1;
                }
                return rng.Next(2);
            }
            else if (stage is InsideOrOutsideStage)
            {
                if (npc.Hand.Count > 1)
                {
                    var c1 = npc.Hand[0];
                    var c2 = npc.Hand[1];
                    int diff = Math.Abs((int)c1.Rank - (int)c2.Rank);
                    return diff >= 6 ? 0 : 1;
                }
                return rng.Next(2);
            }
            else // GuessTheSuitStage
            {
                return rng.Next(4);
            }
        }

        public int DetermineBusRideGuess(Card currentCard)
        {
            int rank = (int)currentCard.Rank;
            if (rank == 2) return 0; // 2 is lowest, always guess higher
            if (rank == 14) return 1; // Ace is highest, always guess lower

            // 70% chance to make the statistically "correct" guess
            bool correctGuess = rng.Next(100) < 70;
            if (correctGuess)
            {
                return rank <= 8 ? 0 : 1; // 8 is middle. If <= 8, guess higher (0).
            }
            else
            {
                return rank <= 8 ? 1 : 0;
            }
        }
    }
}

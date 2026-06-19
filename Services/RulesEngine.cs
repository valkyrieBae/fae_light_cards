using System;
using System.Collections.Generic;
using System.Linq;

namespace FaeLightCards
{
    public class RulesEngine
    {
        public readonly GameState State;
        public const float ScionMatchDelay = 0.5f;

        private readonly Queue<PendingScionMatch> PendingScionMatches = new();
        private float ScionMatchTimer = 0f;
        private readonly Plugin plugin;

        public RulesEngine(Plugin plugin, GameState state)
        {
            this.plugin = plugin;
            this.State = state;
        }

        public readonly IGuessingStage[] GuessingStages = new IGuessingStage[]
        {
            new RedOrBlackStage(),
            new HigherOrLowerStage(),
            new InsideOrOutsideStage(),
            new GuessTheSuitStage()
        };

        public bool HasPendingScionMatches => PendingScionMatches.Count > 0;

        public bool TryDequeuePendingScionMatch(float deltaTime, out PendingScionMatch match)
        {
            match = null!;
            if (PendingScionMatches.Count == 0)
            {
                return false;
            }

            ScionMatchTimer -= deltaTime;
            if (ScionMatchTimer > 0f)
            {
                return false;
            }

            match = PendingScionMatches.Dequeue();
            ScionMatchTimer = ScionMatchDelay;
            return true;
        }

        public IGuessingStage? GetCurrentStage(Player? targetPlayer = null)
        {
            var localPlayer = State.Players.FirstOrDefault(p => p.IsLocal);
            var player = targetPlayer ?? localPlayer;
            if (player == null) return null;
            if (player.IsDealer) return null;
            if (State.HasPendingDrinkTarget) return null;
            if (State.ActiveMode == GameMode.Undecided) return null;

            if (State.ActivePhase == GamePhase.Accumulation)
            {
                if (State.ActiveMode == GameMode.Dealer)
                {
                    if (State.DealerActivePlayerName != player.Name)
                    {
                        return null;
                    }

                    if (player.Hand.Count > GetCurrentRoundIndex())
                    {
                        return null;
                    }
                }

                int index = player.Hand.Count;
                if (index >= 0 && index < GuessingStages.Length)
                {
                    return GuessingStages[index];
                }
            }
            else if (State.ActivePhase == GamePhase.BusRide)
            {
                if (State.BusRiderName == player.Name)
                {
                    if (State.BusRideCurrentCard != null && State.BusRideCorrectStreak < plugin.Configuration.BusSize)
                    {
                        return GuessingStages[1]; // HigherOrLowerStage
                    }
                }
            }
            return null;
        }

        public List<Card> CreateShuffledDeck()
        {
            var deck = new List<Card>();
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    deck.Add(new Card(suit, rank));
                }
            }
            return deck.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        public Card DrawFromDeck()
        {
            if (State.DrawPile.Count == 0)
            {
                State.DrawPile.AddRange(CreateShuffledDeck());
            }

            var card = State.DrawPile[0];
            State.DrawPile.RemoveAt(0);
            return card;
        }

        /// <summary>
        /// Sets up the 15-card pyramid for Phase 2 from the local draw shoe.
        /// </summary>
        public void SetupPyramid()
        {
            State.Pyramid.Clear();
            Array.Clear(State.PyramidFlipped, 0, State.PyramidFlipped.Length);
            Array.Clear(State.PyramidMatchedCards, 0, State.PyramidMatchedCards.Length);
            State.CurrentFlipIndex = 0;
            State.ActiveRow = 5;
            State.PendingLocalMatchSlotIndex = -1;
            State.PendingDrinkGiverName = string.Empty;
            State.PendingDrinkAmount = 0;
            plugin.GameCoordinator.ClearDealerPhaseChangePrompt();
            plugin.GameCoordinator.ResetPyramidDealerAutomation();

            for (int i = 0; i < 15; i++)
            {
                State.PyramidRequiredMatchers[i].Clear();
                State.PyramidMatchedCardsLists[i].Clear();
                State.PyramidMatchedPlayerNamesLists[i].Clear();
                State.PyramidMatchedCardsRotationsLists[i].Clear();
            }

            // Setup / ensure players based on mode
            if (State.ActiveMode == GameMode.Dealer)
            {
                // If there are no players (e.g. started directly in Phase 2 for testing), choose local NPCs.
                if (State.Players.Count == 0)
                {
                    var scionPool = GameConstants.ScionNames.ToList();
                    int npcCount = Math.Clamp(plugin.Configuration.NpcCount, 1, GameConstants.ScionNames.Length);
                    var chosenScions = scionPool.OrderBy(_ => Random.Shared.Next()).Take(npcCount).ToList();
                    foreach (var scion in chosenScions)
                    {
                        State.Players.Add(new Player { Name = scion, IsLocal = false });
                    }
                }
            }

            foreach (var p in State.Players)
            {
                p.IsEligibleForCurrentBusRide = !p.IsDealer;
            }

            // Preserve existing non-dealer hands and top up any new or partial hands.
            foreach (var p in State.Players)
            {
                if (p.IsDealer)
                {
                    p.Hand.Clear();
                    continue;
                }

                while (p.Hand.Count < 4)
                {
                    p.Hand.Add(DrawFromDeck());
                }
            }

            for (int i = 0; i < 15; i++)
            {
                State.Pyramid.Add(DrawFromDeck());
            }

            State.ActionLog.Add("Pyramid deal complete. Phase 2 started.");
        }

        public void SetupBusRide(int busSize)
        {
            State.ActivePhase = GamePhase.BusRide;
            State.BusRideCorrectStreak = 0;
            State.BusRideCurrentCard = null;
            plugin.UiState.BusRideEndConfirmationPending = false;
            plugin.GameCoordinator.ClearDealerPhaseChangePrompt();

            State.BusRideDeck.Clear();

            State.ActionLog.Clear();
            string rider = State.BusRiderName == GameConstants.LocalPlayerName ? "You are" : $"{State.BusRiderName} is";
            State.ActionLog.Add($"{rider} Riding the Bus! Guess Higher or Lower {busSize} times in a row.");
        }

        public Card DrawBusRideCard()
        {
            return DrawFromDeck();
        }

        /// <summary>
        /// Adds a single card from a shuffled deck to the hand.
        /// </summary>
        public void DrawCard()
        {
            var hand = State.GetRequiredDisplayedHand(nameof(DrawCard));
            if (hand.Count >= 4) return;

            hand.Add(DrawFromDeck());
        }

        /// <summary>
        /// Simulates playing a card from hand.
        /// </summary>
        public void PlayHandCard(int index)
        {
            var hand = State.GetRequiredDisplayedHand(nameof(PlayHandCard));
            if (index >= 0 && index < hand.Count)
            {
                hand.RemoveAt(index);
            }
        }

        public bool IsActiveRowFullyFlipped()
        {
            for (int i = 0; i < 15; i++)
            {
                if (GetRowIndex(i) == State.ActiveRow)
                {
                    if (!State.PyramidFlipped[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool HasNextRow()
        {
            return State.ActiveRow > 1;
        }

        public void AdvanceToNextRow()
        {
            if (HasNextRow())
            {
                State.ActiveRow--;
            }
        }

        // Row/multiplier helper methods
        public static int GetRowIndex(int cardIndex)
        {
            if (cardIndex >= 0 && cardIndex <= 4) return 5;
            if (cardIndex >= 5 && cardIndex <= 8) return 4;
            if (cardIndex >= 9 && cardIndex <= 11) return 3;
            if (cardIndex >= 12 && cardIndex <= 13) return 2;
            if (cardIndex == 14) return 1;
            return -1;
        }

        public static int GetRowMultiplier(int row)
        {
            return 6 - row; // Row 5 -> 1, Row 4 -> 2, Row 3 -> 3, Row 2 -> 4, Row 1 -> 5
        }

        public static (int row, int col) GetCardPos(int index)
        {
            if (index >= 0 && index <= 4) return (5, index);
            if (index >= 5 && index <= 8) return (4, index - 5);
            if (index >= 9 && index <= 11) return (3, index - 9);
            if (index >= 12 && index <= 13) return (2, index - 12);
            if (index == 14) return (1, 0);
            return (-1, -1);
        }

        public bool HasPendingMatches()
        {
            if (State.ActivePhase != GamePhase.Pyramid)
            {
                return false;
            }

            if (PendingScionMatches.Count > 0)
            {
                return true;
            }

            for (int i = 0; i < 15; i++)
            {
                if (i < State.PyramidFlipped.Length && State.PyramidFlipped[i])
                {
                    foreach (var matcher in State.PyramidRequiredMatchers[i])
                    {
                        int requiredCount = State.PyramidRequiredMatchers[i].Count(m => m == matcher);
                        int matchedCount = State.PyramidMatchedPlayerNamesLists[i].Count(m => m == matcher);
                        if (matchedCount < requiredCount)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public int GetCurrentRoundIndex()
        {
            var nonDealers = State.Players.Where(p => !p.IsDealer).ToList();
            if (nonDealers.Count == 0) return 0;
            return nonDealers.Min(p => p.Hand.Count);
        }
        public void ProcessMatchesForFlippedCard(int slotIdx)
        {
            Card flippedCard = State.Pyramid[slotIdx];
            bool localHasMatch = false;

            foreach (var p in State.Players)
            {
                if (p.IsDealer)
                {
                    continue;
                }

                var matchingCards = p.Hand.Where(c => c.Rank == flippedCard.Rank).ToList();
                if (p.IsLocal)
                {
                    if (matchingCards.Count > 0)
                    {
                        State.PyramidRequiredMatchers[slotIdx].Add(p.Name);
                        localHasMatch = true;
                    }
                    continue;
                }

                foreach (var scionCard in matchingCards)
                {
                    State.PyramidRequiredMatchers[slotIdx].Add(p.Name);
                    PendingScionMatches.Enqueue(new PendingScionMatch(p, scionCard, slotIdx));
                }
            }

            if (State.ActiveMode == GameMode.Player)
            {
                if (localHasMatch)
                {
                    State.PendingLocalMatchSlotIndex = slotIdx;
                }
                else
                {
                    var localPlayer = State.Players.FirstOrDefault(p => p.IsLocal);
                    if (localPlayer != null)
                    {
                        State.PyramidRequiredMatchers[slotIdx].RemoveAll(name => name == localPlayer.Name);
                    }
                }
            }

            if (PendingScionMatches.Count > 0)
            {
                ScionMatchTimer = ScionMatchDelay;
            }

        }
    }
}

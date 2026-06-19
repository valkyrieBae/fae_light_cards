using System;
using System.Collections.Generic;
using System.Linq;

namespace FaeLightCards
{
    public enum GamePhase
    {
        Accumulation = 0,
        Pyramid = 1,
        BusRide = 2,
        TieChoice = 3
    }

    public enum GameMode
    {
        Undecided = 0,
        Player = 1,
        Dealer = 2
    }

    public class Player
    {
        public string Name { get; set; } = string.Empty;
        public List<Card> Hand { get; } = new List<Card>();
        public int DrinksGiven { get; set; } = 0;
        public int DrinksTaken { get; set; } = 0;
        public bool IsLocal { get; set; } = false;
        public bool IsDealer { get; set; } = false;
        public bool IsHuman { get; set; } = false;
        public bool IsEligibleForCurrentBusRide { get; set; } = true;
    }

    public class GameState
    {
        // List of all active players in the game (local player + Scions)
        public List<Player> Players { get; } = new List<Player>();
        public List<string> ActionLog { get; } = new List<string>();

        public GameMode ActiveMode { get; set; } = GameMode.Undecided;

        public string LocalPlayerName => Players.FirstOrDefault(p => p.IsLocal)?.Name ?? GameConstants.LocalPlayerName;
        private static readonly IReadOnlyList<Card> EmptyDisplayedHand = Array.Empty<Card>();

        private string dealerActivePlayerName = "";
        public string DealerActivePlayerName
        {
            get
            {
                if (string.IsNullOrEmpty(dealerActivePlayerName))
                {
                    var firstNpc = Players.FirstOrDefault(p => !p.IsLocal);
                    return firstNpc?.Name ?? "Estinien";
                }
                return dealerActivePlayerName;
            }
            set => dealerActivePlayerName = value;
        }

        public bool TryGetDisplayedHand(out List<Card> hand)
        {
            var localPlayer = Players.FirstOrDefault(p => p.IsLocal);
            bool showDealerActiveHand = ActiveMode == GameMode.Dealer && (localPlayer?.IsDealer == true || localPlayer == null);
            var owner = showDealerActiveHand
                ? Players.FirstOrDefault(p => p.Name == DealerActivePlayerName)
                : localPlayer;

            if (owner != null)
            {
                hand = owner.Hand;
                return true;
            }

            hand = null!;
            return false;
        }

        public IReadOnlyList<Card> DisplayedHand => TryGetDisplayedHand(out var hand) ? hand : EmptyDisplayedHand;

        public List<Card> GetRequiredDisplayedHand(string caller)
        {
            if (TryGetDisplayedHand(out var hand))
            {
                return hand;
            }

            throw new InvalidOperationException($"{caller} requires a displayed hand, but no owning player is available.");
        }

        // Keep Hand property mapping to active player's hand for compatibility with rule paths.
        public List<Card> Hand => GetRequiredDisplayedHand(nameof(Hand));

        // Pyramid cards (Phase 2)
        public List<Card> Pyramid { get; } = new List<Card>();
        public bool[] PyramidFlipped { get; } = new bool[15];
        public Card?[] PyramidMatchedCards { get; } = new Card?[15];

        // Track all matched cards and players per pyramid slot (allows stacking)
        public List<Card>[] PyramidMatchedCardsLists { get; } = new List<Card>[15];
        public List<string>[] PyramidMatchedPlayerNamesLists { get; } = new List<string>[15];
        public List<float>[] PyramidMatchedCardsRotationsLists { get; } = new List<float>[15];
        public List<string>[] PyramidRequiredMatchers { get; } = new List<string>[15];

        public int CurrentFlipIndex { get; set; } = 0;
        public int ActiveRow { get; set; } = 5;
        public GamePhase ActivePhase { get; set; } = GamePhase.Accumulation;

        public int PendingLocalMatchSlotIndex { get; set; } = -1;
        public int? PendingPlaySlotIndex { get; set; } = null;
        public string PendingDrinkGiverName { get; set; } = string.Empty;
        public int PendingDrinkAmount { get; set; } = 0;
        public string? PendingDrinkId { get; set; }
        public bool HasPendingDrinkTarget => !string.IsNullOrWhiteSpace(PendingDrinkGiverName) && PendingDrinkAmount > 0;
        public bool NetworkDebugToolsEnabled { get; set; }

        // Bus Ride State
        public Card? BusRideCurrentCard { get; set; }
        public int BusRideCorrectStreak { get; set; }
        public List<Card> BusRideDeck { get; } = new List<Card>();

        // Local-only draw shoe. Networked games continue to use server-provided state.
        public List<Card> DrawPile { get; } = new List<Card>();

        public string BusRiderName { get; set; } = "";

        public GameState()
        {
            for (int i = 0; i < 15; i++)
            {
                PyramidMatchedCardsLists[i] = new List<Card>();
                PyramidMatchedPlayerNamesLists[i] = new List<string>();
                PyramidMatchedCardsRotationsLists[i] = new List<float>();
                PyramidRequiredMatchers[i] = new List<string>();
            }
            Reset();
        }

        public void Reset()
        {
            Pyramid.Clear();
            Array.Clear(PyramidFlipped, 0, PyramidFlipped.Length);
            Array.Clear(PyramidMatchedCards, 0, PyramidMatchedCards.Length);
            CurrentFlipIndex = 0;
            ActiveRow = 5;
            ActivePhase = GamePhase.Accumulation;
            ActiveMode = GameMode.Undecided;
            PendingLocalMatchSlotIndex = -1;
            PendingPlaySlotIndex = null;
            PendingDrinkGiverName = string.Empty;
            PendingDrinkAmount = 0;
            PendingDrinkId = null;
            NetworkDebugToolsEnabled = false;
            BusRideCurrentCard = null;
            BusRideCorrectStreak = 0;
            BusRideDeck.Clear();
            DrawPile.Clear();
            BusRiderName = "";
            ActionLog.Clear();
            ActionLog.Add("Game started.");

            for (int i = 0; i < 15; i++)
            {
                PyramidMatchedCardsLists[i].Clear();
                PyramidMatchedPlayerNamesLists[i].Clear();
                PyramidMatchedCardsRotationsLists[i].Clear();
                PyramidRequiredMatchers[i].Clear();
            }

            Players.Clear();
            // Local player is GameConstants.LocalPlayerName
            Players.Add(new Player { Name = GameConstants.LocalPlayerName, IsLocal = true });

            // Scions as fake other players
            var scions = GameConstants.ScionNames.OrderBy(_ => Random.Shared.Next()).Take(3).ToList();
            foreach (var scion in scions)
            {
                Players.Add(new Player { Name = scion, IsLocal = false });
            }
        }

        /// <summary>
        /// Generates a standard 52-card deck, shuffled.
        /// </summary>
    }
}

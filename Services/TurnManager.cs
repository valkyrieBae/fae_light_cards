using System.Collections.Generic;

namespace FaeLightCards
{
    public class TurnManager
    {
        public float NpcThinkingTimer { get; set; } = -1f;
        public bool DealerNpcHasGuessed { get; set; } = false;
        public int DealerCurrentNpcGuess { get; set; } = -1;
        public float DealerTransitionTimer { get; set; } = 0f;
        public float HandTransitionTimer { get; set; } = 0f;
        public float HandTransitionDuration { get; } = 0.8f;
        public List<Card> TransitionPrevHand { get; set; } = new();
        public List<Card> TransitionNextHand { get; set; } = new();
        public int DealerCurrentPlayerIndex { get; set; } = 0;
        public bool DealerTransitionStarted { get; set; } = false;
        public bool DealerNeedNextPlayer { get; set; } = false;
        public float DealerPhaseTransitionTimer { get; set; } = 0f;
        public GamePhase? DealerNextPhasePending { get; set; } = null;
        public GamePhase? DeferredNetworkPhase { get; set; } = null;
        public bool PlayerNpcTurnsPending { get; set; } = false;
        public int PlayerNpcTurnIndex { get; set; } = 0;
        public int PlayerNpcTurnRound { get; set; } = -1;
        public float PlayerNpcOutcomeTimer { get; set; } = 0f;
        public PendingPlayerNpcOutcomeState? PendingPlayerNpcOutcome { get; set; } = null;
        public int PendingPlayerBusRideGuess { get; set; } = -1;
        public float PlayerBusRideResultTimer { get; set; } = -1f;
        public float PyramidDealerTimer { get; set; } = -1f;
        public bool PyramidDealerPaused { get; set; } = false;
        public bool PyramidDealerHasStarted { get; set; } = false;

        public class PendingPlayerNpcOutcomeState
        {
            public Player Npc { get; }
            public Player? Target { get; }
            public int Drinks { get; }
            public bool Won { get; }
            public string SecondaryMessage { get; }
            public string SoundFile { get; }
            public string ActionLogMessage { get; }
            public float RevealTimer { get; set; }

            public PendingPlayerNpcOutcomeState(
                Player npc,
                Player? target,
                int drinks,
                bool won,
                string secondaryMessage,
                string soundFile,
                string actionLogMessage,
                float revealTimer)
            {
                Npc = npc;
                Target = target;
                Drinks = drinks;
                Won = won;
                SecondaryMessage = secondaryMessage;
                SoundFile = soundFile;
                ActionLogMessage = actionLogMessage;
                RevealTimer = revealTimer;
            }
        }

        public class PendingLogAnnouncement
        {
            public string Message { get; }
            public float DelayTimer { get; set; }

            public PendingLogAnnouncement(string message, float delayTimer)
            {
                Message = message;
                DelayTimer = delayTimer;
            }
        }

        public List<PendingLogAnnouncement> PendingLogAnnouncements { get; } = new();
    }
}

using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace FaeLightCards
{
    public class UIState
    {
        public enum MessageIntent
        {
            Conveyor,
            Secondary,
            RightSide,
            InstructionBanner
        }

        public enum OverlayMessageCompletionAction
        {
            None,
            StartBusRide
        }

        public class ActiveOverlayMessage
        {
            public string Text { get; set; } = string.Empty;
            public bool ShowFireworks { get; set; }
            public MessageIntent Intent { get; set; } = MessageIntent.Conveyor;
            public OverlayMessageCompletionAction CompletionAction { get; set; } = OverlayMessageCompletionAction.None;
            public bool CompletionActionHandled { get; set; }
            public float CompletionActionDelayTimer { get; set; } = -1f;
            public float VisualPosition { get; set; } = 2.0f;
            public float TargetPosition { get; set; } = 2.0f;
        }

        public class OverlayMessage
        {
            public string Message { get; }
            public bool ShowFireworks { get; }
            public MessageIntent Intent { get; }
            public OverlayMessageCompletionAction CompletionAction { get; }

            public OverlayMessage(
                string message,
                bool showFireworks = false,
                OverlayMessageCompletionAction completionAction = OverlayMessageCompletionAction.None,
                MessageIntent intent = MessageIntent.Conveyor)
            {
                Message = message;
                ShowFireworks = showFireworks;
                CompletionAction = completionAction;
                Intent = intent;
            }
        }

        public enum PromptAnimState
        {
            Normal,
            ButtonClick,
            Shrinking,
            Hidden,
            Growing
        }

        public enum DealerPhaseChangePromptState
        {
            None,
            Phase1Complete,
            Phase2Complete
        }

        public List<ActiveOverlayMessage> ActiveOverlayMessages { get; } = new();
        public Queue<OverlayMessage> OverlayMessageQueue { get; } = new();
        public Queue<string> SecondaryMessageQueue { get; } = new();

        public string? WinLoseMessage => ActiveOverlayMessages.FirstOrDefault(m => m.TargetPosition == 1.0f)?.Text;
        public float WinLoseAnimTime => ActiveOverlayMessages.Count > 0 ? 1.0f : -1f;
        public bool WinLoseShowFireworks => ActiveOverlayMessages.FirstOrDefault(m => m.TargetPosition == 1.0f)?.ShowFireworks ?? false;
        public string? PendingWinLoseMessage { get; set; }
        public float WinLoseDelayTimer { get; set; } = -1f;

        public float ConveyorTimer { get; set; } = 0f;

        public float BusRideResetTimer { get; set; } = -1f;
        public float BusRidePromptGrowTimer { get; set; } = -1f;
        public float BusRideVictoryResetTimer { get; set; } = -1f;
        public bool BusRideEndConfirmationPending { get; set; } = false;
        public DealerPhaseChangePromptState DealerPhaseChangePrompt { get; set; } = DealerPhaseChangePromptState.None;
        public bool DealerPhaseChangeEndGameConfirmationPending { get; set; } = false;
        public bool DealerPhaseChangeRestartConfirmationPending { get; set; } = false;
        public bool NetworkDealerActionPending { get; set; } = false;

        public string? SecondaryMessage { get; set; }
        public float SecondaryMessageAnimTime { get; set; } = -1f;

        public string CurrentRightSideMessage { get; set; } = "";
        public string TargetRightSideMessage { get; set; } = "";
        public float RightSideMessageScale { get; set; } = 1.0f;
        public float RightSideMessageAnimTimer { get; set; } = -1f;

        public enum RightSideAnimState { Normal, PoppingOut, PoppingIn }
        public RightSideAnimState RightSideMessageState { get; set; } = RightSideAnimState.Normal;

        public PromptAnimState PromptState { get; set; } = PromptAnimState.Normal;
        public string CachedPromptText { get; set; } = string.Empty;
        public List<GuessOption> CachedOptions { get; set; } = new();
        public float PromptAnimTimer { get; set; } = 0f;
        public int ClickedButtonIndex { get; set; } = -1;
        public Vector2 PromptCenterPos { get; set; } = Vector2.Zero;
        public float PromptScale { get; set; } = 1.0f;

        public Vector2 PyramidPosition { get; set; } = Vector2.Zero;
        public Vector2 PyramidSize { get; set; } = Vector2.Zero;
        public bool IsPyramidVisible { get; set; } = false;

        public Vector2 HandPosition { get; set; } = Vector2.Zero;
        public Vector2 HandSize { get; set; } = Vector2.Zero;
        public bool IsHandVisible { get; set; } = false;


        public string LastMovedElementName { get; set; } = "None";
        public Vector2 LastMovedElementCoords { get; set; } = Vector2.Zero;
        public bool IsPromptPositionCustom { get; set; } = false;
    }
}

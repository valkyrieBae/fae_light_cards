using System;

namespace FaeLightCards
{
    public class EventBus
    {
        public event Action<Card, int>? CardDealt;
        public event Action<Card>? BusRideCardDealt;
        public event Action<Player, Card, int, int, string>? LocalCardMatched;
        public event Action<Player, Card, int, string>? ScionCardMatched;
        public event Action<Card>? BusRideSlideDown;
        public event Action<Card, int>? PyramidCardFlipped;
        public event Action<GamePhase>? PhaseChanged;
        public event Action<string, bool, UIState.OverlayMessageCompletionAction>? OverlayMessageTriggered;
        public event Action<string, float>? LogMessageTriggered;
        public event Action<string>? SecondaryMessageRequested;
        public event Action<string>? RightSideMessageRequested;
        public event Action<UIState.PromptAnimState, float>? PromptStateChangeRequested;
        public event Action<string>? PlaySoundRequested;

        public void PublishCardDealt(Card card, int slotIndex) => CardDealt?.Invoke(card, slotIndex);
        public void PublishBusRideCardDealt(Card card) => BusRideCardDealt?.Invoke(card);
        public void PublishLocalCardMatched(Player source, Card card, int handSlotIndex, int pyramidIndex, string targetPlayerName) => LocalCardMatched?.Invoke(source, card, handSlotIndex, pyramidIndex, targetPlayerName);
        public void PublishScionCardMatched(Player source, Card card, int pyramidIndex, string targetPlayerName) => ScionCardMatched?.Invoke(source, card, pyramidIndex, targetPlayerName);
        public void PublishBusRideSlideDown(Card card) => BusRideSlideDown?.Invoke(card);
        public void PublishPyramidCardFlipped(Card card, int pyramidIndex) => PyramidCardFlipped?.Invoke(card, pyramidIndex);
        public void PublishPhaseChanged(GamePhase newPhase) => PhaseChanged?.Invoke(newPhase);
        public void PublishOverlayMessage(
            string message,
            bool fireworks = false,
            UIState.OverlayMessageCompletionAction completionAction = UIState.OverlayMessageCompletionAction.None)
            => OverlayMessageTriggered?.Invoke(message, fireworks, completionAction);
        public void PublishLogMessage(string message, float delay = 0f) => LogMessageTriggered?.Invoke(message, delay);
        public void PublishSecondaryMessage(string message) => SecondaryMessageRequested?.Invoke(message);
        public void PublishRightSideMessage(string message) => RightSideMessageRequested?.Invoke(message);
        public void PublishPromptStateChange(UIState.PromptAnimState state, float scale = 0f) => PromptStateChangeRequested?.Invoke(state, scale);
        public void PublishPlaySound(string soundFile) => PlaySoundRequested?.Invoke(soundFile);
    }
}

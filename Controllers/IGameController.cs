using System;

namespace FaeLightCards
{
    public interface IGameController : IDisposable
    {
        bool HasPendingActions { get; }
        void Update(float dt);
        void HandlePlayerGuess(int pickedOptionIndex);
        void HandleDealerDeal();
        void HandleDealerAdvanceNextPlayer();
        void HandleFlipPyramidCard();
        void HandleAdvancePyramidRow();
        void PlayLocalMatch(string targetPlayerName);
        void GivePendingDrinkToPlayer(string targetPlayerName);
        void HandleDealerDealBusCard();
        void EndGame();
        void RestartGame();
        void ChooseBusRider(string playerName);
        void AddNpcPlayer();
        void DebugSkipToPyramid();
        void DebugSkipToLastCard();
    }
}

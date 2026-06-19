using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class GameCoordinatorService
    {
        private readonly Plugin plugin;

        public GameCoordinatorService(Plugin plugin)
        {
            this.plugin = plugin;
            plugin.EventBus.OverlayMessageTriggered += QueueConveyorMessage;
            plugin.EventBus.SecondaryMessageRequested += ShowSecondaryMessage;
            plugin.EventBus.RightSideMessageRequested += SetRightSideMessage;
        }

        public bool HasBlockingAiProgressionVisuals(bool includePromptTransitions = true)
        {
            bool hasPromptTransition = includePromptTransitions
                                       && (plugin.UiState.PromptState == UIState.PromptAnimState.ButtonClick
                                           || plugin.UiState.PromptState == UIState.PromptAnimState.Shrinking
                                           || plugin.UiState.PromptState == UIState.PromptAnimState.Growing);

            return plugin.HandWindow.HasActiveAnimations
                   || plugin.UiState.ActiveOverlayMessages.Count > 0
                   || plugin.UiState.OverlayMessageQueue.Count > 0
                   || plugin.UiState.PendingWinLoseMessage != null
                   || plugin.UiState.SecondaryMessage != null
                   || plugin.UiState.SecondaryMessageQueue.Count > 0
                   || hasPromptTransition
                   || plugin.GameState.HasPendingDrinkTarget
                   || plugin.GameState.PendingLocalMatchSlotIndex != -1
                   || plugin.RulesEngine.HasPendingScionMatches;
        }

        public void Update(float dt)
        {
            ValidateDealerPhaseChangePrompt();
            EnsureDealerPhaseChangePrompt();
            ClearBusRideEndConfirmationOutsideBusRide();
            ApplyDeferredNetworkPhase();
            UpdatePendingLogAnnouncements(dt);
            ApplyPendingLocalHandCardRemoval();
            UpdateHandTransitionTimer(dt);
            UpdateDealerPhaseTransitionTimer(dt);
            UpdateRightSideMessageAndPhaseTimers(dt);
        }
    }
}

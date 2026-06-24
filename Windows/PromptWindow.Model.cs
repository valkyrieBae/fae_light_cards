using System;
using System.Collections.Generic;
using System.Linq;

namespace FaeLightCards
{
    public partial class PromptWindow
    {
        private enum PromptScreenKind
        {
            StandardChoices,
            EnterRoomCode,
            Lobby,
            Hidden
        }

        private readonly record struct PromptRenderModel(
            PromptScreenKind ScreenKind,
            string PromptText,
            IReadOnlyList<GuessOption> Options,
            bool IsLockedOut);

        private PromptRenderModel BuildRenderModel()
        {
            bool isLocalDealer = plugin.IsLocalDealer;
            bool isDealerPhaseChangePrompt = plugin.GameState.ActiveMode == GameMode.Dealer
                                             && isLocalDealer
                                             && plugin.UiState.DealerPhaseChangePrompt != UIState.DealerPhaseChangePromptState.None;
            bool isPromptTransitioning = plugin.UiState.PromptState != UIState.PromptAnimState.Normal;
            bool isLockedOut = plugin.GameState.HasPendingDrinkTarget
                               || isPromptTransitioning
                               || plugin.UiState.NetworkDealerActionPending
                               || (!isDealerPhaseChangePrompt && plugin.IsAnimationPlaying);

            if (plugin.AppState.ChosenGameMode == GameMode.Undecided)
            {
                return Standard("Select Game Mode", CreateModeSelectionOptions(), isLockedOut);
            }

            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Undecided)
            {
                return Standard("Select Connection Mode", CreateConnectionSelectionOptions(), isLockedOut);
            }

            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Connecting)
            {
                return Standard("Looking for server...", Array.Empty<GuessOption>(), isLockedOut);
            }

            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.ConnectionFailed)
            {
                string failureMessage = FormatConnectionFailurePromptMessage(plugin.AppState.ConnectionFailureMessage);
                string promptText = string.IsNullOrWhiteSpace(failureMessage)
                    ? "Connection Failed"
                    : $"Connection Failed\n{failureMessage}";
                return Standard(promptText, CreateConnectionFailedOptions(), isLockedOut);
            }

            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Connected)
            {
                return new PromptRenderModel(PromptScreenKind.EnterRoomCode, string.Empty, Array.Empty<GuessOption>(), isLockedOut);
            }

            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Networked && plugin.GameState.ActiveMode == GameMode.Undecided)
            {
                return new PromptRenderModel(PromptScreenKind.Lobby, string.Empty, Array.Empty<GuessOption>(), isLockedOut);
            }

            if (plugin.GameState.ActivePhase == GamePhase.TieChoice || plugin.GameState.HasPendingDrinkTarget)
            {
                return Hidden();
            }

            if (plugin.GameState.ActiveMode == GameMode.Dealer && isLocalDealer)
            {
                return BuildDealerRenderModel(isLockedOut);
            }

            if (plugin.UiState.PromptState == UIState.PromptAnimState.ButtonClick || plugin.UiState.PromptState == UIState.PromptAnimState.Shrinking)
            {
                return Standard(plugin.UiState.CachedPromptText, plugin.UiState.CachedOptions, isLockedOut);
            }

            var stage = plugin.RulesEngine.GetCurrentStage();
            return stage == null
                ? Hidden()
                : Standard(stage.PromptText, stage.Options, isLockedOut);
        }

        private PromptRenderModel BuildDealerRenderModel(bool isLockedOut)
        {
            if (plugin.UiState.DealerPhaseChangePrompt != UIState.DealerPhaseChangePromptState.None)
            {
                return Standard(GetDealerPhaseChangePromptText(), CreateDealerPhaseOptions(), isLockedOut);
            }

            if (plugin.GameState.ActivePhase == GamePhase.Accumulation)
            {
                return BuildDealerAccumulationRenderModel(isLockedOut);
            }

            if (plugin.GameState.ActivePhase == GamePhase.BusRide)
            {
                return Standard(CreateBusRidePromptText(), CreateBusRideOptions(), isLockedOut);
            }

            return Hidden();
        }

        private PromptRenderModel BuildDealerAccumulationRenderModel(bool isLockedOut)
        {
            if (plugin.TurnManager.DealerNeedNextPlayer)
            {
                var nonDealers = plugin.GameState.Players.Where(p => !p.IsDealer).ToList();
                bool allFinished = nonDealers.Count > 0 && nonDealers.All(p => p.Hand.Count == 4);
                string promptText = allFinished ? "Phase 1 Complete" : "Next Player Up";
                string buttonLabel = allFinished ? "Build Pyramid" : "Next Player";
                return Standard(promptText, new List<GuessOption> { new(buttonLabel, UITheme.Primary) }, isLockedOut);
            }

            bool isEnabled = plugin.TurnManager.DealerNpcHasGuessed
                             && !plugin.IsAnimationPlaying
                             && plugin.TurnManager.DealerTransitionTimer <= 0f;
            var options = new List<GuessOption>
            {
                new("Deal Card", UITheme.ForEnabled(isEnabled, UITheme.Primary))
            };

            return Standard($"Deal to {plugin.GameState.DealerActivePlayerName}?", options, isLockedOut);
        }

        private List<GuessOption> CreateModeSelectionOptions()
        {
            return new List<GuessOption>
            {
                new("Player", UITheme.Secondary),
                new("Dealer", UITheme.Danger)
            };
        }

        private List<GuessOption> CreateConnectionSelectionOptions()
        {
            return new List<GuessOption>
            {
                new("Local-Only", UITheme.Secondary),
                new("Networked", UITheme.Danger)
            };
        }

        private List<GuessOption> CreateConnectionFailedOptions()
        {
            return new List<GuessOption>
            {
                new("Retry", UITheme.Primary),
                new("Local-Only", UITheme.Danger)
            };
        }

        private List<GuessOption> CreateDealerPhaseOptions()
        {
            if (plugin.UiState.DealerPhaseChangeEndGameConfirmationPending)
            {
                return new List<GuessOption>
                {
                    new("Yes", UITheme.Danger),
                    new("No", UITheme.Secondary)
                };
            }

            if (plugin.UiState.DealerPhaseChangeRestartConfirmationPending)
            {
                return new List<GuessOption>
                {
                    new("Yes", UITheme.Secondary),
                    new("No", UITheme.Neutral)
                };
            }

            string primaryLabel = plugin.UiState.DealerPhaseChangePrompt == UIState.DealerPhaseChangePromptState.Phase1Complete
                ? "Build Pyramid"
                : "Ride Bus";

            return new List<GuessOption>
            {
                new(primaryLabel, UITheme.Primary),
                new("Restart", UITheme.Secondary),
                new("End Game", UITheme.Danger)
            };
        }

        private List<GuessOption> CreateBusRideOptions()
        {
            bool isEnabled = CanUseDealerBusRideControls();
            if (plugin.UiState.BusRideEndConfirmationPending)
            {
                return new List<GuessOption>
                {
                    new("Yes", UITheme.ForEnabled(isEnabled, UITheme.Danger)),
                    new("No", UITheme.ForEnabled(isEnabled, UITheme.Secondary))
                };
            }

            return new List<GuessOption>
            {
                new("Deal Card", UITheme.ForEnabled(isEnabled, UITheme.Primary)),
                new("End Phase", UITheme.ForEnabled(isEnabled, UITheme.Danger))
            };
        }

        private string CreateBusRidePromptText()
        {
            return plugin.UiState.BusRideEndConfirmationPending
                ? "End Bus Ride?"
                : $"Deal to {plugin.GameState.BusRiderName}?";
        }

        private string GetDealerPhaseChangePromptText()
        {
            if (plugin.UiState.DealerPhaseChangeEndGameConfirmationPending)
            {
                return "End Game?";
            }

            if (plugin.UiState.DealerPhaseChangeRestartConfirmationPending)
            {
                return "Restart Game?";
            }

            return plugin.UiState.DealerPhaseChangePrompt switch
            {
                UIState.DealerPhaseChangePromptState.Phase1Complete => "Phase 1 Complete",
                UIState.DealerPhaseChangePromptState.Phase2Complete => "Pyramid Complete",
                _ => string.Empty
            };
        }

        private static string FormatConnectionFailurePromptMessage(string message)
        {
            string formatted = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            if (string.IsNullOrWhiteSpace(formatted))
            {
                return string.Empty;
            }

            formatted = formatted
                .Replace("Update required: you are on version ", "Update required:\nyou are on version ", StringComparison.Ordinal)
                .Replace(", and you need at least version ", ",\nand you need at least\nversion ", StringComparison.Ordinal);

            const int maxLength = 160;
            if (formatted.Length > maxLength)
            {
                formatted = formatted[..maxLength].TrimEnd() + "...";
            }

            return formatted;
        }

        private static PromptRenderModel Standard(string promptText, IReadOnlyList<GuessOption> options, bool isLockedOut)
        {
            return new PromptRenderModel(PromptScreenKind.StandardChoices, promptText, options, isLockedOut);
        }

        private static PromptRenderModel Hidden()
        {
            return new PromptRenderModel(PromptScreenKind.Hidden, string.Empty, Array.Empty<GuessOption>(), true);
        }
    }
}

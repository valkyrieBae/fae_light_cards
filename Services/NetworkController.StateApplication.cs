using System;
using System.Text.Json;

namespace FaeLightCards
{
    public partial class NetworkController
    {
        private sealed class NetworkStateApplyContext
        {
            public NetworkStateApplyContext(GameState oldState, bool isTransitionToPyramid)
            {
                OldState = oldState;
                IsTransitionToPyramid = isTransitionToPyramid;
            }

            public GameState OldState { get; }
            public bool IsTransitionToPyramid { get; }
            public bool HandledAccumulationOutcome { get; set; }
            public bool HasPendingDrinkTarget { get; set; }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                if (!root.TryGetProperty("event", out var eventProp)) return;

                string eventType = eventProp.GetString() ?? string.Empty;
                if (eventType.Equals("RoomSnapshot", StringComparison.OrdinalIgnoreCase))
                {
                    var dataElement = root.GetProperty("data");
                    var stateElement = dataElement.GetProperty("snapshot");
                    var stateDto = JsonSerializer.Deserialize<GameStateDto>(stateElement.GetRawText(), jsonOptions);
                    if (stateDto != null)
                    {
                        ApplyStateIfValid(stateDto);
                    }
                }
                else if (eventType.Equals("GameEvent", StringComparison.OrdinalIgnoreCase))
                {
                    var dataElement = root.GetProperty("data");
                    if (dataElement.TryGetProperty("eventType", out var eventTypeElement))
                    {
                        Plugin.Log.Debug($"Network event: {eventTypeElement.GetString() ?? "unknown"}");
                    }

                    var stateElement = dataElement.GetProperty("snapshot");
                    var stateDto = JsonSerializer.Deserialize<GameStateDto>(stateElement.GetRawText(), jsonOptions);
                    if (stateDto != null)
                    {
                        ApplyStateIfValid(stateDto);
                    }
                }
                else if (eventType.Equals("Welcome", StringComparison.OrdinalIgnoreCase))
                {
                    if (root.TryGetProperty("data", out var dataElement) &&
                        dataElement.TryGetProperty("roomId", out var roomIdElement))
                    {
                        plugin.AppState.CurrentRoomId = roomIdElement.GetString()?.Trim().ToUpperInvariant() ?? plugin.AppState.CurrentRoomId;
                        if (dataElement.TryGetProperty("resumeToken", out var resumeTokenElement))
                        {
                            resumeToken = resumeTokenElement.GetString();
                        }

                        if (dataElement.TryGetProperty("capabilities", out var capabilitiesElement) &&
                            capabilitiesElement.TryGetProperty("debugTools", out var debugToolsElement))
                        {
                            serverDebugToolsEnabled = debugToolsElement.GetBoolean();
                            plugin.GameState.NetworkDebugToolsEnabled = serverDebugToolsEnabled;
                        }
                    }

                    Plugin.Log.Info("Joined room successfully.");
                    plugin.AppState.ConnectionFailureMessage = string.Empty;
                    plugin.AppState.ActiveConnectionMode = ConnectionMode.Networked;
                }
                else if (eventType.Equals("Error", StringComparison.OrdinalIgnoreCase))
                {
                    var dataElement = root.GetProperty("data");
                    string errorMsg = dataElement.GetProperty("message").GetString() ?? "Unknown error";
                    plugin.EventBus.PublishSecondaryMessage($"Error: {errorMsg}");
                    Plugin.Log.Warning($"Server returned error: {errorMsg}");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Error processing WebSocket message: {e}");
            }
        }

        private void ApplyStateIfValid(GameStateDto dto)
        {
            if (!TryValidateCards(dto, out var validationError))
            {
                Plugin.Log.Warning($"Skipped network snapshot with invalid card payload: {validationError}");
                RequestResyncAfterBadSnapshot();
                return;
            }

            ApplyState(dto);
        }

        private void RequestResyncAfterBadSnapshot()
        {
            var now = DateTime.UtcNow;
            if (now - lastBadSnapshotResyncUtc < TimeSpan.FromSeconds(1))
            {
                return;
            }

            lastBadSnapshotResyncUtc = now;
            QueueAction("RequestResync");
        }

        private void ApplyState(GameStateDto dto)
        {
            UpdateActionState(dto);

            var oldState = plugin.GameState;
            var context = new NetworkStateApplyContext(
                oldState,
                oldState.ActivePhase != GamePhase.Pyramid && dto.ActivePhase == (int)GamePhase.Pyramid);

            if (context.IsTransitionToPyramid)
            {
                plugin.HandWindow.ClearAnimationsAndParticles();
            }

            context.HandledAccumulationOutcome = PublishAccumulationOutcome(dto, context);
            PublishDealAnimations(dto, context);
            PublishPyramidMatchAnimations(dto, context);
            PublishPyramidFlipSounds(dto, context);
            PublishBusRideEffects(dto, context);

            ApplyScalarState(dto, context);
            ApplyDealerTurnState(dto, context);
            PublishNetworkStatusMessages(dto, context);
            CopyActionLog(dto, context);
            CopyPlayers(dto, context);
            CopyPyramidState(dto, context);
            CopyBusRideDeck(dto, context);
        }
    }
}

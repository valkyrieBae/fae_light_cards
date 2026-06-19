using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FaeLightCards
{
    public partial class NetworkController : IGameController
    {
        private readonly Plugin plugin;
        private ClientWebSocket? ws;
        private readonly CancellationTokenSource cts = new();
        private readonly ConcurrentQueue<string> incomingQueue = new();
        private readonly ConcurrentQueue<Action> mainThreadActions = new();
        private readonly Channel<OutboundGameAction> outboundActions = Channel.CreateUnbounded<OutboundGameAction>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        private readonly Task outboundSendTask;
        private bool isConnecting = false;
        private volatile bool isDisposed = false;
        private readonly string localPlayerName;
        private string? resumeToken;
        private string? pendingMatchId;
        private string? pendingDrinkId;
        private bool serverDebugToolsEnabled = false;
        private DateTime lastBadSnapshotResyncUtc = DateTime.MinValue;

        public bool DebugToolsAvailable => serverDebugToolsEnabled;

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public NetworkController(Plugin plugin)
        {
            this.plugin = plugin;
            string name = GameConstants.LocalPlayerName;
            try
            {
                name = Plugin.ObjectTable?.LocalPlayer?.Name?.TextValue ?? GameConstants.LocalPlayerName;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"Could not get player name in constructor: {ex.Message}");
            }
            this.localPlayerName = name;

            outboundSendTask = SendOutboundLoopAsync();
            _ = ConnectAsync();
        }

        public bool HasPendingActions => ws?.State == WebSocketState.Connecting || isConnecting;

        private async Task ConnectAsync()
        {
            if (isDisposed || isConnecting || ws?.State == WebSocketState.Open) return;

            isConnecting = true;
            var chosenMode = plugin.AppState.ChosenGameMode;
            var serverAddress = plugin.Configuration.ServerAddress;
            EnqueueMainThreadAction(() =>
            {
                plugin.AppState.ConnectionFailureMessage = string.Empty;
                plugin.EventBus.PublishSecondaryMessage("Connecting to server...");
            });
            ws = new ClientWebSocket();

            try
            {
                var uri = new Uri(serverAddress);
                await ws.ConnectAsync(uri, cts.Token);
                if (isDisposed)
                {
                    isConnecting = false;
                    return;
                }

                var compatibilityResult = await NetworkCompatibilityChecker.CheckAsync(ws, cts.Token);
                if (!compatibilityResult.IsCompatible)
                {
                    isConnecting = false;
                    Plugin.Log.Warning($"Server compatibility check failed: {compatibilityResult.ErrorMessage}");
                    await CloseWebSocketQuietlyAsync(ws);
                    ws = null;
                    EnqueueConnectionFailed(compatibilityResult.ErrorMessage);
                    return;
                }

                isConnecting = false;
                EnqueueMainThreadAction(() =>
                {
                    plugin.AppState.ConnectionFailureMessage = string.Empty;
                    plugin.EventBus.PublishSecondaryMessage("Connected to server!");
                });

                // Start background reader loop
                _ = ReceiveLoopAsync();

                if (chosenMode == GameMode.Dealer)
                {
                    EnqueueMainThreadAction(() =>
                    {
                        plugin.AppState.CurrentRoomId = string.Empty;
                        plugin.AppState.ConnectionFailureMessage = string.Empty;
                        plugin.AppState.ActiveConnectionMode = ConnectionMode.Connected;
                    });

                    QueueAction("CreateRoom", new
                    {
                        playerName = this.localPlayerName
                    });
                }
                else
                {
                    // For player, transition to Connected, waiting for entered room code
                    EnqueueMainThreadAction(() =>
                    {
                        plugin.AppState.CurrentRoomId = string.Empty;
                        plugin.AppState.ConnectionFailureMessage = string.Empty;
                        plugin.AppState.ActiveConnectionMode = ConnectionMode.Connected;
                    });
                }
            }
            catch (OperationCanceledException)
            {
                isConnecting = false;
                if (!isDisposed)
                {
                    EnqueueConnectionFailed();
                }
            }
            catch (Exception e) when (e is WebSocketException || e is System.Net.Http.HttpRequestException || e is System.Net.Sockets.SocketException || e is IOException)
            {
                isConnecting = false;
                if (isDisposed) return;

                Plugin.Log.Warning($"WebSocket connection failed (server offline?): {e.Message}");
                EnqueueConnectionFailed("Connection failed!");
            }
            catch (Exception e)
            {
                isConnecting = false;
                if (isDisposed) return;

                Plugin.Log.Error($"WebSocket Connection Failed with unexpected error: {e}");
                EnqueueConnectionFailed("Connection failed!");
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[16384];
            try
            {
                while (ws != null && ws.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
                {
                    using var messageStream = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            if (!isDisposed)
                            {
                                EnqueueConnectionFailed("Disconnected from server");
                            }
                            return;
                        }

                        if (result.MessageType != WebSocketMessageType.Text)
                        {
                            break;
                        }

                        messageStream.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        continue;
                    }

                    string jsonMessage = Encoding.UTF8.GetString(messageStream.ToArray());
                    incomingQueue.Enqueue(jsonMessage);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException)
            {
                if (isDisposed) return;

                Plugin.Log.Warning($"WebSocket connection closed: {e.Message}");
                EnqueueConnectionFailed();
            }
            catch (Exception e)
            {
                if (isDisposed) return;

                Plugin.Log.Error($"WebSocket receive error: {e}");
                EnqueueConnectionFailed();
            }
        }

        public void Update(float dt)
        {
            while (mainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Plugin.Log.Error($"Error processing queued network action: {e}");
                }
            }

            // Drain the concurrent queue on the game framework thread
            while (incomingQueue.TryDequeue(out var message))
            {
                ProcessMessage(message);
            }
        }

        private void EnqueueMainThreadAction(Action action)
        {
            mainThreadActions.Enqueue(() =>
            {
                if (!isDisposed)
                {
                    action();
                }
            });
        }

        private void EnqueueConnectionFailed(string? message = null)
        {
            string failureMessage = CleanConnectionFailureMessage(message);
            EnqueueMainThreadAction(() =>
            {
                plugin.AppState.ConnectionFailureMessage = failureMessage;
                if (!string.IsNullOrWhiteSpace(failureMessage))
                {
                    plugin.EventBus.PublishSecondaryMessage(failureMessage);
                }

                plugin.AppState.ActiveConnectionMode = ConnectionMode.ConnectionFailed;
            });
        }

        private static string CleanConnectionFailureMessage(string? message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static async Task CloseWebSocketQuietlyAsync(ClientWebSocket? socket)
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Compatibility check failed", closeCts.Token);
                }
            }
            catch (Exception)
            {
                // Best effort cleanup for a failed startup connection.
            }
            finally
            {
                socket.Dispose();
            }
        }

        public void Dispose()
        {
            isDisposed = true;
            outboundActions.Writer.TryComplete();
            cts.Cancel();
            try
            {
                outboundSendTask.Wait(TimeSpan.FromMilliseconds(250));
            }
            catch (Exception e) when (e is OperationCanceledException || e is AggregateException)
            {
                // The outbound loop observes cancellation during normal shutdown.
            }
            try
            {
                ws?.Dispose();
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Error disposing ClientWebSocket: {e}");
            }
            cts.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}

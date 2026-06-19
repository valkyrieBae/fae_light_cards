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
    public partial class NetworkController
    {
        private async Task SendActionRaw(object actionPayload)
        {
            if (ws?.State != WebSocketState.Open) return;

            try
            {
                string json = JsonSerializer.Serialize(actionPayload, jsonOptions);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
            }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException)
            {
                Plugin.Log.Warning($"Failed to send WebSocket payload (connection closed): {e.Message}");
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Failed to send WebSocket payload: {e}");
            }
        }
        private void QueueAction(string actionType, object? data = null)
        {
            if (isDisposed)
            {
                return;
            }

            if (!outboundActions.Writer.TryWrite(new OutboundGameAction(actionType, data)))
            {
                Plugin.Log.Warning($"Unable to queue outbound network action: {actionType}");
            }
        }
        private async Task SendOutboundLoopAsync()
        {
            try
            {
                while (await outboundActions.Reader.WaitToReadAsync(cts.Token))
                {
                    while (outboundActions.Reader.TryRead(out var queuedAction))
                    {
                        if (!await EnsureConnectedForSendAsync(queuedAction.ActionType))
                        {
                            continue;
                        }

                        var payload = new
                        {
                            action = queuedAction.ActionType,
                            data = queuedAction.Data
                        };
                        await SendActionRaw(payload);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal plugin shutdown.
            }
            catch (ChannelClosedException)
            {
                // Normal plugin shutdown.
            }
            catch (Exception e)
            {
                if (!isDisposed)
                {
                    Plugin.Log.Error($"Outbound network send loop failed: {e}");
                    EnqueueConnectionFailed();
                }
            }
        }
        private async Task<bool> EnsureConnectedForSendAsync(string actionType)
        {
            if (ws?.State == WebSocketState.Open)
            {
                return true;
            }

            if (isConnecting)
            {
                while (isConnecting && !cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, cts.Token);
                    if (ws?.State == WebSocketState.Open)
                    {
                        return true;
                    }
                }
            }

            if (ws?.State == WebSocketState.Open)
            {
                return true;
            }

            Plugin.Log.Warning($"WS not open, reconnecting before sending action: {actionType}");
            await ConnectAsync();
            if (ws?.State == WebSocketState.Open)
            {
                return true;
            }

            Plugin.Log.Warning($"Skipped outbound network action because the websocket is not open: {actionType}");
            return false;
        }
    }
}

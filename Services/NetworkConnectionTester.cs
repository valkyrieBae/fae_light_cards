using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FaeLightCards
{
    public sealed class NetworkConnectionTestResult
    {
        private NetworkConnectionTestResult(bool isSuccess, long elapsedMilliseconds, string errorMessage)
        {
            IsSuccess = isSuccess;
            ElapsedMilliseconds = elapsedMilliseconds;
            ErrorMessage = errorMessage;
        }

        public bool IsSuccess { get; }
        public long ElapsedMilliseconds { get; }
        public string ErrorMessage { get; }

        public static NetworkConnectionTestResult Success(long elapsedMilliseconds)
        {
            return new NetworkConnectionTestResult(true, elapsedMilliseconds, string.Empty);
        }

        public static NetworkConnectionTestResult Failed(string errorMessage)
        {
            return new NetworkConnectionTestResult(false, 0, errorMessage);
        }
    }

    public static class NetworkConnectionTester
    {
        public static async Task<NetworkConnectionTestResult> TestAsync(
            string serverAddress,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serverAddress))
            {
                return NetworkConnectionTestResult.Failed("Server address is empty");
            }

            if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out var uri))
            {
                return NetworkConnectionTestResult.Failed("Invalid websocket address");
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeWs, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeWss, StringComparison.OrdinalIgnoreCase))
            {
                return NetworkConnectionTestResult.Failed("Address must use ws:// or wss://");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            using var ws = new ClientWebSocket();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await ws.ConnectAsync(uri, timeoutCts.Token);

                var compatibilityResult = await NetworkCompatibilityChecker.CheckAsync(ws, timeoutCts.Token);
                stopwatch.Stop();

                return compatibilityResult.IsCompatible
                    ? NetworkConnectionTestResult.Success(stopwatch.ElapsedMilliseconds)
                    : NetworkConnectionTestResult.Failed(compatibilityResult.ErrorMessage);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return NetworkConnectionTestResult.Failed("Timed out");
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or SocketException or IOException)
            {
                return NetworkConnectionTestResult.Failed(ex.Message);
            }
            catch (Exception ex)
            {
                return NetworkConnectionTestResult.Failed(ex.Message);
            }
            finally
            {
                await CloseQuietlyAsync(ws);
            }
        }

        private static async Task CloseQuietlyAsync(ClientWebSocket ws)
        {
            if (ws.State != WebSocketState.Open && ws.State != WebSocketState.CloseReceived)
            {
                return;
            }

            try
            {
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", closeCts.Token);
            }
            catch (Exception)
            {
                // Best effort cleanup for a short-lived test socket.
            }
        }
    }
}

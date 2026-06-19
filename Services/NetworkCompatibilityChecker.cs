using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FaeLightCards
{
    public sealed class NetworkCompatibilityResult
    {
        private NetworkCompatibilityResult(
            bool isCompatible,
            string currentPluginVersion,
            string? serverVersion,
            string? minimumPluginVersion,
            string errorMessage)
        {
            IsCompatible = isCompatible;
            CurrentPluginVersion = currentPluginVersion;
            ServerVersion = serverVersion;
            MinimumPluginVersion = minimumPluginVersion;
            ErrorMessage = errorMessage;
        }

        public bool IsCompatible { get; }
        public string CurrentPluginVersion { get; }
        public string? ServerVersion { get; }
        public string? MinimumPluginVersion { get; }
        public string ErrorMessage { get; }

        public static NetworkCompatibilityResult Compatible(
            string currentPluginVersion,
            string? serverVersion,
            string? minimumPluginVersion)
        {
            return new NetworkCompatibilityResult(
                true,
                currentPluginVersion,
                serverVersion,
                minimumPluginVersion,
                string.Empty);
        }

        public static NetworkCompatibilityResult Failed(
            string currentPluginVersion,
            string errorMessage,
            string? serverVersion = null,
            string? minimumPluginVersion = null)
        {
            return new NetworkCompatibilityResult(
                false,
                currentPluginVersion,
                serverVersion,
                minimumPluginVersion,
                errorMessage);
        }
    }

    public static class NetworkCompatibilityChecker
    {
        private const int MaxResponseBytes = 64 * 1024;

        public static string CurrentPluginVersion => FormatVersion(typeof(Plugin).Assembly.GetName().Version);

        public static async Task<NetworkCompatibilityResult> CheckAsync(
            ClientWebSocket ws,
            CancellationToken cancellationToken)
        {
            byte[] pingBytes = Encoding.UTF8.GetBytes("{\"action\":\"Ping\"}");
            await ws.SendAsync(
                new ArraySegment<byte>(pingBytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);

            string response = await ReceiveTextMessageAsync(ws, cancellationToken);
            return ParsePong(response, CurrentPluginVersion);
        }

        internal static NetworkCompatibilityResult ParsePong(string response, string currentPluginVersion)
        {
            try
            {
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                if (!root.TryGetProperty("event", out var eventElement)
                    || !string.Equals(eventElement.GetString(), "Pong", StringComparison.OrdinalIgnoreCase))
                {
                    return NetworkCompatibilityResult.Failed(currentPluginVersion, "Unexpected response");
                }

                if (!root.TryGetProperty("data", out var dataElement)
                    || !dataElement.TryGetProperty("compatibility", out var compatibilityElement)
                    || compatibilityElement.ValueKind == JsonValueKind.Null)
                {
                    return NetworkCompatibilityResult.Compatible(currentPluginVersion, null, null);
                }

                if (compatibilityElement.ValueKind != JsonValueKind.Object
                    || !TryGetRequiredString(compatibilityElement, "serverVersion", out string serverVersion))
                {
                    return NetworkCompatibilityResult.Failed(
                        currentPluginVersion,
                        "Server compatibility metadata is invalid");
                }

                string? minimumPluginVersion = null;
                if (compatibilityElement.TryGetProperty("breakingMinimumPluginVersion", out var minimumElement)
                    && minimumElement.ValueKind != JsonValueKind.Null)
                {
                    if (minimumElement.ValueKind != JsonValueKind.String)
                    {
                        return NetworkCompatibilityResult.Failed(
                            currentPluginVersion,
                            "Server compatibility metadata is invalid",
                            serverVersion);
                    }

                    minimumPluginVersion = minimumElement.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(minimumPluginVersion)
                        || !TryParseVersion(minimumPluginVersion, out var requiredVersion))
                    {
                        return NetworkCompatibilityResult.Failed(
                            currentPluginVersion,
                            "Server compatibility metadata is invalid",
                            serverVersion,
                            minimumPluginVersion);
                    }

                    if (!TryParseVersion(currentPluginVersion, out var currentVersion))
                    {
                        return NetworkCompatibilityResult.Failed(
                            currentPluginVersion,
                            $"Plugin version is invalid: {currentPluginVersion}",
                            serverVersion,
                            minimumPluginVersion);
                    }

                    if (currentVersion.CompareTo(requiredVersion) < 0)
                    {
                        return NetworkCompatibilityResult.Failed(
                            currentPluginVersion,
                            FormatUpdateRequiredMessage(currentPluginVersion, minimumPluginVersion),
                            serverVersion,
                            minimumPluginVersion);
                    }
                }

                return NetworkCompatibilityResult.Compatible(
                    currentPluginVersion,
                    serverVersion,
                    minimumPluginVersion);
            }
            catch (JsonException)
            {
                return NetworkCompatibilityResult.Failed(currentPluginVersion, "Unexpected response");
            }
        }

        public static string FormatUpdateRequiredMessage(
            string currentPluginVersion,
            string minimumPluginVersion)
        {
            return $"Update required: you are on version {currentPluginVersion}, and you need at least version {minimumPluginVersion}.";
        }

        private static bool TryGetRequiredString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static async Task<string> ReceiveTextMessageAsync(
            ClientWebSocket ws,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            using var message = new MemoryStream();

            while (true)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new IOException("Connection closed before Pong");
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    throw new IOException("Unexpected non-text response");
                }

                message.Write(buffer, 0, result.Count);
                if (message.Length > MaxResponseBytes)
                {
                    throw new IOException("Response too large");
                }

                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(message.ToArray());
                }
            }
        }

        private static bool TryParseVersion(string versionText, out Version version)
        {
            version = new Version(0, 0, 0, 0);
            if (!Version.TryParse(versionText.Trim(), out var parsed))
            {
                return false;
            }

            version = new Version(
                Math.Max(0, parsed.Major),
                Math.Max(0, parsed.Minor),
                Math.Max(0, parsed.Build),
                Math.Max(0, parsed.Revision));
            return true;
        }

        private static string FormatVersion(Version? version)
        {
            if (version == null)
            {
                return "0.0.0.0";
            }

            if (version.Revision >= 0)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }

            if (version.Build >= 0)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }

            return $"{version.Major}.{version.Minor}";
        }
    }
}

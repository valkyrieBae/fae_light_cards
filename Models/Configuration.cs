using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace FaeLightCards
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public const string LegacyDefaultDeckDesignId = "default";
        public const string FaeDeckDesignId = "fae";
        public const string NormalDeckDesignId = "normal";
        public const string DefaultDeckDesignId = FaeDeckDesignId;
        public const string LocalhostServerAddress = "ws://localhost:8080/ws";
        // AWS-hosted network room server used by the Remote server option.
        public const string RemoteServerAddress = "ws://3.16.107.89:8080/ws";

        public int Version { get; set; } = 9;

        // Position lock settings
        public bool IsLocked { get; set; } = false;

        // Networking settings
        public ServerAddressMode ServerAddressMode { get; set; } = global::FaeLightCards.ServerAddressMode.Remote;
        public string ServerAddress { get; set; } = RemoteServerAddress;
        public string CustomServerAddress { get; set; } = string.Empty;
        public bool IncludeNpcs { get; set; } = false;

        public float DeckScale { get; set; } = 1.0f;
        public float HandScale { get; set; } = 0.20f;
        public float PyramidScale { get; set; } = 0.20f;
        public float TooltipDelay { get; set; } = 2.0f;
        public CardAnimationType AnimationType { get; set; } = CardAnimationType.ThreeDSpin;
        public CardParticleType ParticleType { get; set; } = CardParticleType.GoldSparkles;
        public bool ShowDebugBounds { get; set; } = false;
        public bool RigNpcsAlwaysGiveToPlayer { get; set; } = false;
        public int BusSize { get; set; } = 8;
        public int NpcCount { get; set; } = 4;
        public BusRiderRigType BusRiderRigType { get; set; } = BusRiderRigType.NoRig;
        public string SelectedDeckDesignId { get; set; } = DefaultDeckDesignId;
        public List<CustomDeckDesignConfig> CustomDeckDesigns { get; set; } = new();

        // Sound configuration settings
        public string DrawSound { get; set; } = "game:15";
        public string WinSound { get; set; } = "sfx:38";
        public string LoseSound { get; set; } = "game:11";
        public string ClickSound { get; set; } = "game:12";

        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            bool shouldSave = false;

            if (Version < 2)
            {
                ShowDebugBounds = false;
                Version = 2;
                shouldSave = true;
            }

            if (Version < 3)
            {
                SelectedDeckDesignId = DefaultDeckDesignId;
                CustomDeckDesigns ??= new List<CustomDeckDesignConfig>();
                Version = 3;
                shouldSave = true;
            }

            if (Version < 4)
            {
                CustomDeckDesigns ??= new List<CustomDeckDesignConfig>();
                foreach (var deck in CustomDeckDesigns)
                {
                    if (deck.CardArtScale <= 0f || float.IsNaN(deck.CardArtScale) || float.IsInfinity(deck.CardArtScale))
                    {
                        deck.CardArtScale = 1.0f;
                    }
                }
                Version = 4;
                shouldSave = true;
            }

            if (Version < 5)
            {
                MigrateServerAddressSelection();
                Version = 5;
                shouldSave = true;
            }

            if (Version < 6)
            {
                Version = 6;
                shouldSave = true;
            }

            if (Version < 7)
            {
                if (string.Equals(SelectedDeckDesignId, LegacyDefaultDeckDesignId, StringComparison.Ordinal))
                {
                    SelectedDeckDesignId = DefaultDeckDesignId;
                }

                Version = 7;
                shouldSave = true;
            }

            if (Version < 8)
            {
                if (Math.Abs(HandScale - 1.0f) < 0.001f)
                {
                    HandScale = 0.20f;
                }

                ShowDebugBounds = false;
                Version = 8;
                shouldSave = true;
            }

            if (Version < 9)
            {
                NpcCount = 4;
                Version = 9;
                shouldSave = true;
            }

            int clampedNpcCount = Math.Clamp(NpcCount, 1, GameConstants.ScionNames.Length);
            if (NpcCount != clampedNpcCount)
            {
                NpcCount = clampedNpcCount;
                shouldSave = true;
            }

            if (CustomDeckDesigns == null)
            {
                CustomDeckDesigns = new List<CustomDeckDesignConfig>();
                shouldSave = true;
            }

            if (string.IsNullOrWhiteSpace(SelectedDeckDesignId) ||
                string.Equals(SelectedDeckDesignId, LegacyDefaultDeckDesignId, StringComparison.Ordinal))
            {
                SelectedDeckDesignId = DefaultDeckDesignId;
                shouldSave = true;
            }

            foreach (var deck in CustomDeckDesigns)
            {
                if (deck.CardArtScale <= 0f || float.IsNaN(deck.CardArtScale) || float.IsInfinity(deck.CardArtScale))
                {
                    deck.CardArtScale = 1.0f;
                    shouldSave = true;
                }
            }

            if (EnsureServerAddressSelection())
            {
                shouldSave = true;
            }

            if (shouldSave)
            {
                Save();
            }
        }

        public void SetServerAddressMode(ServerAddressMode mode)
        {
            ServerAddressMode = Enum.IsDefined<global::FaeLightCards.ServerAddressMode>(mode)
                ? mode
                : global::FaeLightCards.ServerAddressMode.Remote;
            ServerAddress = GetEffectiveServerAddress();
        }

        public void SetCustomServerAddress(string serverAddress)
        {
            CustomServerAddress = serverAddress ?? string.Empty;
            if (ServerAddressMode == global::FaeLightCards.ServerAddressMode.Custom)
            {
                ServerAddress = CustomServerAddress;
            }
        }

        public string GetEffectiveServerAddress()
        {
            return ServerAddressMode switch
            {
                global::FaeLightCards.ServerAddressMode.Localhost => LocalhostServerAddress,
                global::FaeLightCards.ServerAddressMode.Remote => RemoteServerAddress,
                global::FaeLightCards.ServerAddressMode.Custom => CustomServerAddress ?? string.Empty,
                _ => RemoteServerAddress
            };
        }

        private void MigrateServerAddressSelection()
        {
            ServerAddressMode = InferServerAddressMode(ServerAddress);
            if (ServerAddressMode == global::FaeLightCards.ServerAddressMode.Custom)
            {
                CustomServerAddress = ServerAddress ?? string.Empty;
            }
            ServerAddress = GetEffectiveServerAddress();
        }

        private bool EnsureServerAddressSelection()
        {
            bool changed = false;

            if (!Enum.IsDefined<global::FaeLightCards.ServerAddressMode>(ServerAddressMode))
            {
                ServerAddressMode = InferServerAddressMode(ServerAddress);
                changed = true;
            }

            if (CustomServerAddress == null)
            {
                CustomServerAddress = string.Empty;
                changed = true;
            }

            if (ServerAddressMode == global::FaeLightCards.ServerAddressMode.Custom
                && string.IsNullOrWhiteSpace(CustomServerAddress)
                && !string.IsNullOrWhiteSpace(ServerAddress)
                && InferServerAddressMode(ServerAddress) == global::FaeLightCards.ServerAddressMode.Custom)
            {
                CustomServerAddress = ServerAddress;
                changed = true;
            }

            string effectiveAddress = GetEffectiveServerAddress();
            if (!string.Equals(ServerAddress, effectiveAddress, StringComparison.Ordinal))
            {
                ServerAddress = effectiveAddress;
                changed = true;
            }

            return changed;
        }

        private static ServerAddressMode InferServerAddressMode(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return global::FaeLightCards.ServerAddressMode.Remote;
            }

            string normalized = NormalizeServerAddress(address);
            if (string.Equals(normalized, NormalizeServerAddress(LocalhostServerAddress), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "ws://127.0.0.1:8080/ws", StringComparison.OrdinalIgnoreCase))
            {
                return global::FaeLightCards.ServerAddressMode.Localhost;
            }

            if (string.Equals(normalized, NormalizeServerAddress(RemoteServerAddress), StringComparison.OrdinalIgnoreCase))
            {
                return global::FaeLightCards.ServerAddressMode.Remote;
            }

            return global::FaeLightCards.ServerAddressMode.Custom;
        }

        private static string NormalizeServerAddress(string address)
        {
            return address.Trim().TrimEnd('/');
        }

        public void Save()
        {
            pluginInterface?.SavePluginConfig(this);
        }
    }

    [Serializable]
    public class CustomDeckDesignConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public int FoundCardCount { get; set; }
        public float CardArtScale { get; set; } = 1.0f;
    }

    public enum CardAnimationType
    {
        ThreeDSpin = 0,
        LinearSlide = 1,
        SwoopAndBounce = 2,
        Random = 3
    }

    public enum CardParticleType
    {
        None = 0,
        GoldSparkles = 1,
        FireEmbers = 2,
        NeonDigital = 3,
        CardMatch = 4
    }

    public enum BusRiderRigType
    {
        NoRig = 0,
        PlayerRigged = 1,
        NpcRigged = 2
    }

    public enum ServerAddressMode
    {
        Localhost = 0,
        Remote = 1,
        Custom = 2
    }
}

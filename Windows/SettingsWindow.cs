using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class SettingsWindow : Window
    {
        private const float DefaultSettingsWindowWidth = 400f;
        private const float MinimumSettingsWindowHeight = 260f;
        private const float MaximumSettingsWindowWidth = 2000f;
        private const float SettingsWindowViewportMargin = 80f;
        private const float SettingsWindowHeightSlack = 8f;
        private const float SettingsWindowResizeTolerance = 0.5f;

        private readonly Plugin plugin;
        private readonly Dictionary<string, float> measuredWindowHeightsByState = new();
        private string deckFolderPathInput = string.Empty;
        private string deckStatusMessage = string.Empty;
        private string deckStatusDetails = string.Empty;
        private Vector4 deckStatusColor = new(0.6f, 0.6f, 0.6f, 1.0f);
        private readonly DeckFolderBrowserModel deckFolderBrowser = new();
        private bool shouldOpenDeckFolderBrowser;
        private bool shouldFocusDeckFolderBrowserPath;
        private readonly object connectionTestLock = new();
        private int connectionTestGeneration;
        private ConnectionTestStatus connectionTestStatus = ConnectionTestStatus.NotTested;
        private string connectionTestAddress = string.Empty;
        private long connectionTestElapsedMs;
        private string connectionTestFailureReason = string.Empty;
        private bool developerOptionsAcknowledged;
        private float measuredWindowHeight = MinimumSettingsWindowHeight;
        private bool hasMeasuredWindowHeight;
        private SettingsTab activeSettingsTab = SettingsTab.Gameplay;
        private CustomizationTab activeCustomizationTab = CustomizationTab.WindowPositions;

        private enum SettingsTab
        {
            Gameplay,
            Customizations,
            Network,
            Developer
        }

        private enum CustomizationTab
        {
            WindowPositions,
            Scaling,
            Art,
            Gameplay,
            SoundEffects
        }

        private enum ConnectionTestStatus
        {
            NotTested,
            Testing,
            Success,
            Failed
        }

        private struct SoundOption
        {
            public string Label { get; }
            public string Value { get; }
            public SoundOption(string label, string value)
            {
                Label = label;
                Value = value;
            }
        }

        private static readonly SoundOption[] GameSoundOptions = new SoundOption[]
        {
            new("In-Game Sound: se.1", "game:1"),
            new("In-Game Sound: se.2", "game:2"),
            new("In-Game Sound: se.3", "game:3"),
            new("In-Game Sound: se.4", "game:4"),
            new("In-Game Sound: se.5", "game:5"),
            new("In-Game Sound: se.6", "game:6"),
            new("In-Game Sound: se.7", "game:7"),
            new("In-Game Sound: se.8", "game:8"),
            new("In-Game Sound: se.9", "game:9"),
            new("In-Game Sound: se.10", "game:10"),
            new("In-Game Sound: se.11", "game:11"),
            new("In-Game Sound: se.12", "game:12"),
            new("In-Game Sound: se.13", "game:13"),
            new("In-Game Sound: se.14", "game:14"),
            new("In-Game Sound: se.15", "game:15"),
            new("In-Game Sound: se.16", "game:16")
        };

        private static readonly SoundOption[] SfxSoundOptions = new SoundOption[]
        {
            new("In-Game SFX: ID 9", "sfx:9"),
            new("In-Game SFX: ID 11", "sfx:11"),
            new("In-Game SFX: ID 15", "sfx:15"),
            new("In-Game SFX: ID 23", "sfx:23"),
            new("In-Game SFX: ID 24", "sfx:24"),
            new("In-Game SFX: ID 33", "sfx:33"),
            new("In-Game SFX: ID 37", "sfx:37"),
            new("In-Game SFX: ID 38", "sfx:38"),
            new("In-Game SFX: ID 41", "sfx:41"),
            new("In-Game SFX: ID 53", "sfx:53")
        };

        public SettingsWindow(Plugin plugin) : base("Settings###FaeLightCardsSettingsWindowV2")
        {
            this.plugin = plugin;
            this.IsOpen = false;
            this.Size = new Vector2(DefaultSettingsWindowWidth, MinimumSettingsWindowHeight);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(DefaultSettingsWindowWidth, MinimumSettingsWindowHeight),
                MaximumSize = new Vector2(MaximumSettingsWindowWidth, 2000)
            };
            this.Flags = ImGuiWindowFlags.NoCollapse;
        }

        public override void PreDraw()
        {
            UpdateWindowSizeConstraints();
        }

        public override void Draw()
        {
            ApplyPredictedWindowHeight();

            // Top half: Tabs
            if (ImGui.BeginTabBar("SettingsTabBar"))
            {
                if (ImGui.BeginTabItem("Gameplay"))
                {
                    SetActiveSettingsTab(SettingsTab.Gameplay);
                    ImGui.Spacing();

                    if (ImGui.BeginTable("GameplayTable", 2, ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 220f);
                        ImGui.TableSetupColumn("Control", ImGuiTableColumnFlags.WidthStretch);

                        DrawGameplayActionRow();

                        ImGui.EndTable();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Customizations"))
                {
                    SetActiveSettingsTab(SettingsTab.Customizations);
                    DrawCustomizationsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Network"))
                {
                    SetActiveSettingsTab(SettingsTab.Network);
                    ImGui.Spacing();

                    if (ImGui.BeginTable("NetworkTable", 2, ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 180f);
                        ImGui.TableSetupColumn("Control", ImGuiTableColumnFlags.WidthStretch);

                        DrawConnectionStatusSetting();

                        DrawSetting("Include NPC Players", () =>
                        {
                            bool includeNpcs = plugin.Configuration.IncludeNpcs;
                            if (ImGui.Checkbox("##includeNpcs", ref includeNpcs))
                            {
                                plugin.Configuration.IncludeNpcs = includeNpcs;
                                plugin.Configuration.Save();
                            }
                        });

                        DrawSetting("Server Address", DrawServerAddressSelector);

                        ImGui.EndTable();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Developer"))
                {
                    SetActiveSettingsTab(SettingsTab.Developer);
                    ImGui.Spacing();
                    if (DrawDeveloperAcknowledgement())
                    {
                        ApplyPredictedWindowHeight();
                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();

                        DrawDeveloperTools();
                    }
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            DrawDeckFolderBrowserPopup();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1.0f)); // Subtle gray color
            string footerText = "Last Moved: None";
            if (plugin.UiState.LastMovedElementName != "None")
            {
                var viewportSize = ImGui.GetMainViewport().Size;
                float pctX = viewportSize.X > 0 ? (plugin.UiState.LastMovedElementCoords.X / viewportSize.X) * 100f : 0f;
                float pctY = viewportSize.Y > 0 ? (plugin.UiState.LastMovedElementCoords.Y / viewportSize.Y) * 100f : 0f;
                footerText = $"Last Moved: {plugin.UiState.LastMovedElementName} ({pctX:F1}%, {pctY:F1}% of screen)";
            }
            ImGui.TextUnformatted(footerText);
            ImGui.PopStyleColor();

            UpdateMeasuredWindowHeight();
        }

    }
}

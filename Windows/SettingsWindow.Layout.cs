using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class SettingsWindow
    {
        private void SetActiveSettingsTab(SettingsTab tab)
        {
            activeSettingsTab = tab;
            ApplyPredictedWindowHeight();
        }
        private void SetActiveCustomizationTab(CustomizationTab tab)
        {
            activeCustomizationTab = tab;
            ApplyPredictedWindowHeight();
        }
        private void ApplyPredictedWindowHeight()
        {
            float targetHeight = GetPredictedWindowHeight();
            ApplyWindowHeight(targetHeight);
        }
        private void ApplyWindowHeight(float targetHeight)
        {
            var currentSize = ImGui.GetWindowSize();
            float nextWidth = Math.Clamp(currentSize.X, DefaultSettingsWindowWidth, MaximumSettingsWindowWidth);

            if (Math.Abs(currentSize.Y - targetHeight) > SettingsWindowResizeTolerance
                || Math.Abs(currentSize.X - nextWidth) > SettingsWindowResizeTolerance)
            {
                ImGui.SetWindowSize(new Vector2(nextWidth, targetHeight));
            }
        }
        private void UpdateMeasuredWindowHeight()
        {
            var style = ImGui.GetStyle();

            float maxHeight = GetMaximumSettingsWindowHeight();
            float measuredHeight = ImGui.GetCursorPosY() + style.WindowPadding.Y + SettingsWindowHeightSlack;
            measuredWindowHeight = Math.Clamp(measuredHeight, MinimumSettingsWindowHeight, maxHeight);
            hasMeasuredWindowHeight = true;
            measuredWindowHeightsByState[GetWindowHeightStateKey()] = measuredWindowHeight;

            UpdateWindowSizeConstraints();
        }
        private void UpdateWindowSizeConstraints()
        {
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(DefaultSettingsWindowWidth, MinimumSettingsWindowHeight),
                MaximumSize = new Vector2(MaximumSettingsWindowWidth, GetMaximumSettingsWindowHeight())
            };
        }
        private static float GetMaximumSettingsWindowHeight()
            => Math.Max(MinimumSettingsWindowHeight, ImGui.GetMainViewport().Size.Y - SettingsWindowViewportMargin);

        private float GetPredictedWindowHeight()
        {
            string stateKey = GetWindowHeightStateKey();
            if (measuredWindowHeightsByState.TryGetValue(stateKey, out float stateHeight))
            {
                return Math.Clamp(stateHeight, MinimumSettingsWindowHeight, GetMaximumSettingsWindowHeight());
            }

            if (hasMeasuredWindowHeight)
            {
                return Math.Clamp(
                    Math.Max(measuredWindowHeight, EstimateWindowHeight()),
                    MinimumSettingsWindowHeight,
                    GetMaximumSettingsWindowHeight());
            }

            return EstimateWindowHeight();
        }
        private string GetWindowHeightStateKey()
        {
            string key = activeSettingsTab.ToString();
            if (activeSettingsTab == SettingsTab.Customizations)
            {
                key += $":{activeCustomizationTab}";
                if (activeCustomizationTab == CustomizationTab.Art)
                {
                    key += plugin.CardDeckService.GetSelectedCustomDeck() != null
                        ? ":customDeck"
                        : $":includedDeck:{plugin.Configuration.SelectedDeckDesignId}";
                    key += string.IsNullOrWhiteSpace(deckStatusMessage) ? ":noStatus" : ":status";
                }
            }
            else if (activeSettingsTab == SettingsTab.Network)
            {
                key += $":{plugin.Configuration.ServerAddressMode}:{connectionTestStatus}";
            }
            else if (activeSettingsTab == SettingsTab.Developer)
            {
                key += developerOptionsAcknowledged ? $":expanded:{plugin.GameState.ActivePhase}" : ":collapsed";
            }

            return key;
        }
        private float EstimateWindowHeight()
        {
            var style = ImGui.GetStyle();
            float line = ImGui.GetTextLineHeightWithSpacing();
            float row = ImGui.GetFrameHeightWithSpacing();
            float spacing = style.ItemSpacing.Y;
            float separator = spacing + 1f;

            float height = style.WindowPadding.Y * 2f;
            height += row; // Top-level tab bar.

            height += activeSettingsTab switch
            {
                SettingsTab.Gameplay => spacing + row,
                SettingsTab.Customizations => EstimateCustomizationsHeight(row, line, spacing, separator),
                SettingsTab.Network => spacing + row * 4f,
                SettingsTab.Developer => EstimateDeveloperHeight(row, line, spacing, separator),
                _ => row
            };

            height += spacing + separator + spacing + line; // Footer.
            return Math.Clamp(height + SettingsWindowHeightSlack, MinimumSettingsWindowHeight, GetMaximumSettingsWindowHeight());
        }
        private float EstimateCustomizationsHeight(float row, float line, float spacing, float separator)
        {
            float height = spacing + row; // Nested tab bar.

            height += activeCustomizationTab switch
            {
                CustomizationTab.WindowPositions => spacing + row + spacing + line + separator + row * 3f,
                CustomizationTab.Scaling => spacing + row * 3f,
                CustomizationTab.Art => EstimateArtCustomizationHeight(row, line, spacing),
                CustomizationTab.Gameplay => spacing + row * 3f,
                CustomizationTab.SoundEffects => spacing + row * 5f,
                _ => row
            };

            return height;
        }
        private float EstimateArtCustomizationHeight(float row, float line, float spacing)
        {
            float height = spacing;
            height += row; // Deck Design.
            if (plugin.CardDeckService.GetSelectedCustomDeck() != null)
            {
                height += line * 3f + row; // Deck path and rescan/remove buttons inside the deck selector row.
            }

            height += row * 3f; // Deck Folder, Animation Type, Particle Effect.

            if (!string.IsNullOrWhiteSpace(deckStatusMessage))
            {
                height += row + Math.Max(0f, deckStatusDetails.Split('\n').Length - 1) * line;
            }

            if (plugin.CardDeckService.GetSelectedCustomDeck() != null)
            {
                height += row * 2f; // Deck Art Scale plus optional reset wrap.
            }

            return height + spacing;
        }
        private float EstimateDeveloperHeight(float row, float line, float spacing, float separator)
        {
            float acknowledgementHeight = Math.Max(row, line * 7f);
            if (!developerOptionsAcknowledged)
            {
                return spacing + acknowledgementHeight;
            }

            float height = spacing + acknowledgementHeight;
            height += spacing + separator + spacing;
            height += row * 2f; // Rig controls.
            height += spacing + separator + spacing;
            height += row; // Show Debug Bounds.
            height += spacing;
            height += line; // Debug Game Actions label.
            height += row * 3f; // Phase-specific buttons, conservatively.
            height += spacing + separator + spacing;
            height += line + row; // Debug UI Actions label and queue button.
            return height;
        }
        private static bool BeginSettingsTable(string id, float labelWidth = 180f)
        {
            if (!ImGui.BeginTable(id, 2, ImGuiTableFlags.SizingStretchProp))
            {
                return false;
            }

            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, labelWidth);
            ImGui.TableSetupColumn("Control", ImGuiTableColumnFlags.WidthStretch);
            return true;
        }
        private static void DrawResetPositionButtonRow(string leftLabel, Action leftAction, string rightLabel, Action rightAction)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            if (ImGui.Button(leftLabel, new Vector2(-1f, 0f)))
            {
                leftAction();
            }

            ImGui.TableNextColumn();
            if (ImGui.Button(rightLabel, new Vector2(-1f, 0f)))
            {
                rightAction();
            }
        }
        private static void DrawSetting(string label, Action drawControl)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text(label);
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            drawControl();
        }
    }
}

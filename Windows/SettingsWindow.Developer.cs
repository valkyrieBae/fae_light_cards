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
        private bool DrawDeveloperAcknowledgement()
        {
            if (!ImGui.BeginTable("DeveloperAcknowledgementTable", 2, ImGuiTableFlags.SizingStretchProp))
            {
                return developerOptionsAcknowledged;
            }

            ImGui.TableSetupColumn("Check", ImGuiTableColumnFlags.WidthFixed, 32f);
            ImGui.TableSetupColumn("Warning", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.Checkbox("##developerOptionsAcknowledged", ref developerOptionsAcknowledged);

            ImGui.TableNextColumn();
            ImGui.PushTextWrapPos(0f);
            ImGui.TextWrapped("I understand that playing with these options in the middle of a game will probably mess things up, are mostly used for debugging, and were only left here because Val is lazy. I swear not to complain to her about anything in any situation forevermore.");
            ImGui.PopTextWrapPos();

            ImGui.EndTable();

            return developerOptionsAcknowledged;
        }
        private void DrawDeveloperTools()
        {
            if (ImGui.BeginTable("DeveloperOptionsTable", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 220f);
                ImGui.TableSetupColumn("Control", ImGuiTableColumnFlags.WidthStretch);

                DrawSetting("Rig NPCs Always Give Drinks", DrawRigNpcsAlwaysGiveDrinksControl);
                DrawSetting("Rig Bus Rider", DrawRigBusRiderControl);

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            bool showDebugBounds = plugin.Configuration.ShowDebugBounds;
            if (ImGui.Checkbox("Show Debug Bounds", ref showDebugBounds))
            {
                plugin.Configuration.ShowDebugBounds = showDebugBounds;
                plugin.Configuration.Save();
            }

            ImGui.Spacing();

            ImGui.Text("Debug Game Actions:");
#if FAELIGHTCARDS_DEV_TOOLS
            bool canUseDebugSkips = plugin.GameController is not NetworkController networkController || networkController.DebugToolsAvailable;
#endif
            if (plugin.GameState.ActivePhase == GamePhase.Accumulation)
            {
                if (ImGui.Button("Add Card to Hand"))
                {
                    plugin.GameCoordinator.DrawCardWithAnimation();
                }

#if FAELIGHTCARDS_DEV_TOOLS
                ImGui.Spacing();

                if (canUseDebugSkips)
                {
                    if (ImGui.Button("Skip to Pyramid"))
                    {
                        plugin.DebugSkipToPyramid();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Skip to Last Card"))
                    {
                        plugin.DebugSkipToLastCard();
                    }
                }
#endif
            }
            else if (plugin.GameState.ActivePhase == GamePhase.Pyramid)
            {
                int currentFlip = plugin.GameState.CurrentFlipIndex;
                int activeRow = plugin.GameState.ActiveRow;
                bool canFlip = currentFlip < 15 && RulesEngine.GetRowIndex(currentFlip) == activeRow;
                bool canNextRow = activeRow > 1 && RulesEngine.GetRowIndex(currentFlip) < activeRow;

                if (!canFlip)
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.Button("Flip Next Card"))
                {
                    plugin.GameController.HandleFlipPyramidCard();
                }
                if (!canFlip)
                {
                    ImGui.EndDisabled();
                }

                ImGui.SameLine();

                if (!canNextRow)
                {
                    ImGui.BeginDisabled();
                }
                if (ImGui.Button("Next Row"))
                {
                    plugin.GameController.HandleAdvancePyramidRow();
                }
                if (!canNextRow)
                {
                    ImGui.EndDisabled();
                }

                ImGui.Spacing();

#if FAELIGHTCARDS_DEV_TOOLS
                if (canUseDebugSkips && ImGui.Button("Skip to Last Card"))
                {
                    plugin.DebugSkipToLastCard();
                }
#endif
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Debug UI Actions:");
            if (ImGui.Button("Queue Test Message"))
            {
                string[] testMessages = new[]
                {
                    "Alisaie matched!",
                    "Alphinaud matched!",
                    "G'raha Tia matched!",
                    "Thancred matched!",
                    "Urianger matched!",
                    "Y'shtola matched!",
                    "Estinien matched!",
                    "You matched!",
                    "ALISAIE takes 2 drinks!",
                    "ALPHINAUD takes 3 drinks!",
                    "G'RAHA TIA takes 1 drink!",
                    "THANCRED takes 4 drinks!",
                    "URIANGER takes 2 drinks!",
                    "Y'SHTOLA takes 3 drinks!",
                    "ESTINIEN takes 1 drink!",
                    "YOU take 2 drinks!"
                };
                string randomMsg = testMessages[Random.Shared.Next(testMessages.Length)];
                plugin.GameCoordinator.QueueConveyorMessage(randomMsg, Random.Shared.Next(2) == 0);
            }
        }
        private void DrawRigNpcsAlwaysGiveDrinksControl()
        {
            bool rigNpcs = plugin.Configuration.RigNpcsAlwaysGiveToPlayer;
            if (ImGui.Checkbox("##rigNpcs", ref rigNpcs))
            {
                plugin.Configuration.RigNpcsAlwaysGiveToPlayer = rigNpcs;
                plugin.Configuration.Save();
            }
        }
        private void DrawRigBusRiderControl()
        {
            int rigType = (int)plugin.Configuration.BusRiderRigType;
            string rigLabel = rigType switch
            {
                1 => "Player Rigged",
                2 => "Random NPC Rigged",
                _ => "No Rig (Default)"
            };
            if (ImGui.SliderInt("##rigBusRiderSlider", ref rigType, 0, 2, rigLabel))
            {
                plugin.Configuration.BusRiderRigType = (BusRiderRigType)rigType;
                plugin.Configuration.Save();
            }
        }
    }
}

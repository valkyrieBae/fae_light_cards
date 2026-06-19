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
        private void DrawServerAddressSelector()
        {
            string[] serverLabels = new[] { "Localhost", "3.16.107.89", "Custom" };
            int selectedIndex = plugin.Configuration.ServerAddressMode switch
            {
                ServerAddressMode.Localhost => 0,
                ServerAddressMode.Remote => 1,
                ServerAddressMode.Custom => 2,
                _ => 1
            };

            if (ImGui.Combo("##serverAddressMode", ref selectedIndex, serverLabels, serverLabels.Length))
            {
                var selectedMode = selectedIndex switch
                {
                    0 => ServerAddressMode.Localhost,
                    1 => ServerAddressMode.Remote,
                    2 => ServerAddressMode.Custom,
                    _ => ServerAddressMode.Remote
                };

                plugin.Configuration.SetServerAddressMode(selectedMode);
                plugin.Configuration.Save();
            }

            if (plugin.Configuration.ServerAddressMode == ServerAddressMode.Custom)
            {
                string customServerAddress = plugin.Configuration.CustomServerAddress;
                ImGui.Spacing();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##customServerAddress", ref customServerAddress, 255))
                {
                    plugin.Configuration.SetCustomServerAddress(customServerAddress);
                    plugin.Configuration.Save();
                }
            }
        }
        private void DrawConnectionStatusSetting()
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Connection Status");
            ImGui.TableNextColumn();

            var style = ImGui.GetStyle();
            float buttonWidth = ImGui.CalcTextSize("Test Connection").X + style.FramePadding.X * 2f;

            if (ImGui.BeginTable("ConnectionStatusTestTable", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Test Result", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, buttonWidth);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var snapshot = GetConnectionTestSnapshot();
                ImGui.PushStyleColor(ImGuiCol.Text, snapshot.Color);
                ImGui.TextWrapped(snapshot.Text);
                ImGui.PopStyleColor();

                ImGui.TableNextColumn();
                if (snapshot.IsTesting)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.Button("Test Connection", new Vector2(buttonWidth, 0f)))
                {
                    StartConnectionTest();
                }

                if (snapshot.IsTesting)
                {
                    ImGui.EndDisabled();
                }

                ImGui.EndTable();
            }
        }

        private (string Text, Vector4 Color, bool IsTesting) GetConnectionTestSnapshot()
        {
            string currentAddress = plugin.Configuration.ServerAddress;
            lock (connectionTestLock)
            {
                if ((connectionTestStatus == ConnectionTestStatus.Success || connectionTestStatus == ConnectionTestStatus.Failed)
                    && !string.Equals(connectionTestAddress, currentAddress, StringComparison.Ordinal))
                {
                    connectionTestStatus = ConnectionTestStatus.NotTested;
                    connectionTestAddress = string.Empty;
                    connectionTestElapsedMs = 0;
                    connectionTestFailureReason = string.Empty;
                }

                return connectionTestStatus switch
                {
                    ConnectionTestStatus.Testing => (
                        "Testing...",
                        new Vector4(1.0f, 0.75f, 0.2f, 1.0f),
                        true),
                    ConnectionTestStatus.Success => (
                        $"Success ({connectionTestElapsedMs} ms)",
                        new Vector4(0.3f, 0.85f, 0.45f, 1.0f),
                        false),
                    ConnectionTestStatus.Failed => (
                        $"Failed: {connectionTestFailureReason}",
                        new Vector4(1.0f, 0.35f, 0.35f, 1.0f),
                        false),
                    _ => (
                        "Not tested",
                        new Vector4(0.6f, 0.6f, 0.6f, 1.0f),
                        false)
                };
            }
        }

        private void StartConnectionTest()
        {
            string address = plugin.Configuration.ServerAddress;
            int generation;
            lock (connectionTestLock)
            {
                if (connectionTestStatus == ConnectionTestStatus.Testing)
                {
                    return;
                }

                generation = ++connectionTestGeneration;
                connectionTestStatus = ConnectionTestStatus.Testing;
                connectionTestAddress = address;
                connectionTestElapsedMs = 0;
                connectionTestFailureReason = string.Empty;
            }

            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                NetworkConnectionTestResult result;
                try
                {
                    result = await NetworkConnectionTester.TestAsync(address);
                }
                catch (Exception ex)
                {
                    result = NetworkConnectionTestResult.Failed(ex.Message);
                }

                lock (connectionTestLock)
                {
                    if (generation != connectionTestGeneration)
                    {
                        return;
                    }

                    if (!string.Equals(plugin.Configuration.ServerAddress, address, StringComparison.Ordinal))
                    {
                        connectionTestStatus = ConnectionTestStatus.NotTested;
                        connectionTestAddress = string.Empty;
                        connectionTestElapsedMs = 0;
                        connectionTestFailureReason = string.Empty;
                        return;
                    }

                    connectionTestStatus = result.IsSuccess
                        ? ConnectionTestStatus.Success
                        : ConnectionTestStatus.Failed;
                    connectionTestAddress = address;
                    connectionTestElapsedMs = result.ElapsedMilliseconds;
                    connectionTestFailureReason = FormatConnectionTestFailure(result.ErrorMessage);
                }
            });
        }
        private static string FormatConnectionTestFailure(string? reason)
        {
            string formatted = string.IsNullOrWhiteSpace(reason)
                ? "Unknown error"
                : reason.Replace('\r', ' ').Replace('\n', ' ').Trim();

            const int maxLength = 160;
            if (formatted.Length > maxLength)
            {
                formatted = formatted[..maxLength].TrimEnd() + "...";
            }

            return formatted;
        }
    }
}

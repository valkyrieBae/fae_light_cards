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
        private void DrawGameplayActionRow()
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            string buttonLabel = plugin.AppState.GameUiEnabled ? "Stop Game" : "Start Game";
            if (ImGui.Button(buttonLabel, new Vector2(-1f, 0f)))
            {
                if (plugin.AppState.GameUiEnabled)
                {
                    plugin.HideGameUi();
                }
                else
                {
                    plugin.StartOrShowGameUi();
                }
            }

            ImGui.TableNextColumn();
            if (ImGui.Button("Reset Game", new Vector2(-1f, 0f)))
            {
                plugin.ResetGame();
            }
        }
    }
}

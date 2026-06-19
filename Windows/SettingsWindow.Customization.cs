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
        private List<SoundOption> GetDrawSoundOptions()
        {
            var list = new List<SoundOption>
            {
                new("None", "None"),
                new("Local: click (draw_click.wav)", "draw_click.wav"),
                new("Local: slide (draw_slide.wav)", "draw_slide.wav"),
                new("Local: drop (draw_drop.wav)", "draw_drop.wav")
            };
            list.AddRange(GameSoundOptions);
            list.AddRange(SfxSoundOptions);
            return list;
        }
        private List<SoundOption> GetWinSoundOptions()
        {
            var list = new List<SoundOption>
            {
                new("None", "None"),
                new("Local: chime (win_chime.wav)", "win_chime.wav"),
                new("Local: bell (win_bell.wav)", "win_bell.wav"),
                new("Local: fanfare (win_fanfare.wav)", "win_fanfare.wav")
            };
            list.AddRange(GameSoundOptions);
            list.AddRange(SfxSoundOptions);
            return list;
        }
        private List<SoundOption> GetLoseSoundOptions()
        {
            var list = new List<SoundOption>
            {
                new("None", "None"),
                new("Local: buzzer (lose_buzzer.wav)", "lose_buzzer.wav"),
                new("Local: fail (lose_fail.wav)", "lose_fail.wav"),
                new("Local: glitch (lose_glitch.wav)", "lose_glitch.wav")
            };
            list.AddRange(GameSoundOptions);
            list.AddRange(SfxSoundOptions);
            return list;
        }
        private List<SoundOption> GetClickSoundOptions()
        {
            var list = new List<SoundOption>
            {
                new("None", "None"),
                new("Local: tick (click_tick.wav)", "click_tick.wav"),
                new("Local: standard (click_standard.wav)", "click_standard.wav"),
                new("Local: pop (click_pop.wav)", "click_pop.wav")
            };
            list.AddRange(GameSoundOptions);
            list.AddRange(SfxSoundOptions);
            return list;
        }
        private void DrawCustomizationsTab()
        {
            ImGui.Spacing();

            if (!ImGui.BeginTabBar("CustomizationsSubTabBar"))
            {
                return;
            }

            if (ImGui.BeginTabItem("Window Positions"))
            {
                SetActiveCustomizationTab(CustomizationTab.WindowPositions);
                ImGui.Spacing();
                DrawWindowPositionsCustomizationTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Scaling"))
            {
                SetActiveCustomizationTab(CustomizationTab.Scaling);
                ImGui.Spacing();
                DrawScalingCustomizationTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Art"))
            {
                SetActiveCustomizationTab(CustomizationTab.Art);
                ImGui.Spacing();
                DrawArtCustomizationTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Gameplay"))
            {
                SetActiveCustomizationTab(CustomizationTab.Gameplay);
                ImGui.Spacing();
                DrawGameplayCustomizationTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Sound Effects"))
            {
                SetActiveCustomizationTab(CustomizationTab.SoundEffects);
                ImGui.Spacing();
                DrawSoundEffectsCustomizationTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
        private void DrawWindowPositionsCustomizationTab()
        {
            if (BeginSettingsTable("WindowPositionsCustomizationTable"))
            {
                DrawSetting("Lock Positions", DrawLockPositionsControl);
                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Text("Reset Window Positions");
            ImGui.Separator();

            if (ImGui.BeginTable("WindowPositionResetButtonsTable", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Left", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Right", ImGuiTableColumnFlags.WidthStretch, 1f);

                DrawResetPositionButtonRow(
                    "Reset Deck Position",
                    plugin.DeckWindow.ResetPosition,
                    "Reset Hand & Popups Position",
                    plugin.HandWindow.ResetPosition);
                DrawResetPositionButtonRow(
                    "Reset Prompt & Messages Position",
                    plugin.PromptWindow.ResetPosition,
                    "Reset Pyramid Position",
                    plugin.PyramidWindow.ResetPosition);
                DrawResetPositionButtonRow(
                    "Reset Pyramid Controls Position",
                    plugin.PyramidControlsWindow.ResetPosition,
                    "Reset Tavern & Banner Position",
                    plugin.PlayersWindow.ResetPosition);

                ImGui.EndTable();
            }
        }
        private void DrawScalingCustomizationTab()
        {
            if (!BeginSettingsTable("ScalingCustomizationTable"))
            {
                return;
            }

            DrawSetting("Deck Scale", DrawDeckScaleControl);
            DrawSetting("Hand Scale", DrawHandScaleControl);
            DrawSetting("Pyramid Scale", DrawPyramidScaleControl);

            ImGui.EndTable();
        }
        private void DrawArtCustomizationTab()
        {
            if (!BeginSettingsTable("ArtCustomizationTable"))
            {
                return;
            }

            DrawSetting("Deck Design", DrawDeckDesignSelector);
            DrawSetting("Deck Folder", DrawDeckFolderControls);

            if (!string.IsNullOrWhiteSpace(deckStatusMessage))
            {
                DrawSetting("Deck Status", DrawDeckStatus);
            }

            if (plugin.CardDeckService.GetSelectedCustomDeck() != null)
            {
                DrawSetting("Deck Art Scale", DrawDeckArtScaleControl);
            }

            DrawSetting("Animation Type", DrawAnimationTypeControl);
            DrawSetting("Particle Effect", DrawParticleEffectControl);

            ImGui.EndTable();
        }
        private void DrawGameplayCustomizationTab()
        {
            if (!BeginSettingsTable("GameplayCustomizationTable"))
            {
                return;
            }

            DrawSetting("Tooltip Delay", DrawTooltipDelayControl);
            DrawSetting("Bus Size (cards)", DrawBusSizeControl);
            DrawSetting("NPC Count", DrawNpcCountControl);

            ImGui.EndTable();
        }
        private void DrawSoundEffectsCustomizationTab()
        {
            if (BeginSettingsTable("SoundEffectsCustomizationTable"))
            {
                DrawSetting("Card Draw Sound", DrawCardDrawSoundControl);
                DrawSetting("Win Sound", DrawWinSoundControl);
                DrawSetting("Lose Sound", DrawLoseSoundControl);
                DrawSetting("Button Click Sound", DrawButtonClickSoundControl);

                ImGui.EndTable();
            }

            ImGui.Spacing();
            if (ImGui.Button("Set All Sounds to None"))
            {
                plugin.Configuration.DrawSound = "None";
                plugin.Configuration.WinSound = "None";
                plugin.Configuration.LoseSound = "None";
                plugin.Configuration.ClickSound = "None";
                plugin.Configuration.Save();
            }
        }
        private void DrawLockPositionsControl()
        {
            bool isLocked = plugin.Configuration.IsLocked;
            if (ImGui.Checkbox("##lockPos", ref isLocked))
            {
                plugin.Configuration.IsLocked = isLocked;
                plugin.Configuration.Save();
            }
        }
        private void DrawDeckScaleControl()
        {
            float deckScale = plugin.Configuration.DeckScale;
            if (ImGui.SliderFloat("##deckScaleSlider", ref deckScale, 0.10f, 2.0f, "%.2f"))
            {
                plugin.Configuration.DeckScale = Math.Clamp(deckScale, 0.10f, 2.0f);
                plugin.Configuration.Save();
            }
        }
        private void DrawHandScaleControl()
        {
            float handScale = plugin.Configuration.HandScale;
            if (ImGui.SliderFloat("##handScaleSlider", ref handScale, 0.10f, 3.0f, "%.2f"))
            {
                plugin.Configuration.HandScale = Math.Clamp(handScale, 0.10f, 3.0f);
                plugin.Configuration.Save();
            }
        }
        private void DrawPyramidScaleControl()
        {
            float pyramidScale = plugin.Configuration.PyramidScale;
            if (ImGui.SliderFloat("##pyramidScaleSlider", ref pyramidScale, 0.10f, 3.0f, "%.2f"))
            {
                plugin.Configuration.PyramidScale = Math.Clamp(pyramidScale, 0.10f, 3.0f);
                plugin.Configuration.Save();
            }
        }
        private void DrawTooltipDelayControl()
        {
            float tooltipDelay = plugin.Configuration.TooltipDelay;
            if (ImGui.SliderFloat("##tooltipDelaySlider", ref tooltipDelay, 0.0f, 5.0f, "%.2f"))
            {
                plugin.Configuration.TooltipDelay = Math.Clamp(tooltipDelay, 0.0f, 5.0f);
                plugin.Configuration.Save();
            }
        }
        private void DrawBusSizeControl()
        {
            int busSize = plugin.Configuration.BusSize;
            if (ImGui.SliderInt("##busSizeSlider", ref busSize, 4, 8))
            {
                plugin.Configuration.BusSize = Math.Clamp(busSize, 4, 8);
                plugin.Configuration.Save();
            }
        }
        private void DrawNpcCountControl()
        {
            int npcCount = plugin.Configuration.NpcCount;
            if (ImGui.SliderInt("##npcCountSlider", ref npcCount, 1, GameConstants.ScionNames.Length))
            {
                plugin.Configuration.NpcCount = Math.Clamp(npcCount, 1, GameConstants.ScionNames.Length);
                plugin.Configuration.Save();
            }
        }
        private void DrawAnimationTypeControl()
        {
            int animType = (int)plugin.Configuration.AnimationType;
            string[] animNames = new[] { "3D Spin & Flip", "Simple Slide", "Swoop & Bounce", "Randomized" };
            if (ImGui.Combo("##animTypeCombo", ref animType, animNames, animNames.Length))
            {
                plugin.Configuration.AnimationType = (CardAnimationType)animType;
                plugin.Configuration.Save();
            }
        }
        private void DrawParticleEffectControl()
        {
            int pType = (int)plugin.Configuration.ParticleType;
            string[] pNames = new[] { "None (No Particles)", "Magic Gold Sparkles", "Fiery Red Embers", "Neon Digital Bits", "Match the Card (Red/Black)" };
            if (ImGui.Combo("##particleTypeCombo", ref pType, pNames, pNames.Length))
            {
                plugin.Configuration.ParticleType = (CardParticleType)pType;
                plugin.Configuration.Save();
            }
        }
        private void DrawCardDrawSoundControl()
        {
            DrawSoundSelector("##CardDrawSound", GetDrawSoundOptions(), plugin.Configuration.DrawSound, value => plugin.Configuration.DrawSound = value);
        }
        private void DrawWinSoundControl()
        {
            DrawSoundSelector("##WinSound", GetWinSoundOptions(), plugin.Configuration.WinSound, value => plugin.Configuration.WinSound = value);
        }
        private void DrawLoseSoundControl()
        {
            DrawSoundSelector("##LoseSound", GetLoseSoundOptions(), plugin.Configuration.LoseSound, value => plugin.Configuration.LoseSound = value);
        }
        private void DrawButtonClickSoundControl()
        {
            DrawSoundSelector("##ButtonClickSound", GetClickSoundOptions(), plugin.Configuration.ClickSound, value => plugin.Configuration.ClickSound = value);
        }
        private void DrawSoundSelector(string comboId, List<SoundOption> options, string currentValue, Action<string> setValue)
        {
            string[] labels = options.Select(o => o.Label).ToArray();
            int selectedIndex = options.FindIndex(o => o.Value == currentValue);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            if (ImGui.Combo(comboId, ref selectedIndex, labels, labels.Length))
            {
                string selectedValue = options[selectedIndex].Value;
                setValue(selectedValue);
                plugin.Configuration.Save();
                plugin.EventBus.PublishPlaySound(selectedValue);
            }
        }
    }
}

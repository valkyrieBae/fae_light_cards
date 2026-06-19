using System;
using System.Numerics;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class SettingsWindow
    {
        private void DrawDeckDesignSelector()
        {
            var options = plugin.CardDeckService.GetDeckOptions().ToList();
            string[] labels = options.Select(option => option.Label).ToArray();
            int selectedIndex = options.FindIndex(option => option.Id == plugin.Configuration.SelectedDeckDesignId);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            if (ImGui.Combo("##deckDesignCombo", ref selectedIndex, labels, labels.Length))
            {
                var selectedOption = options[selectedIndex];
                plugin.CardDeckService.SelectDeck(selectedOption.Id);
                var selected = plugin.CardDeckService.GetSelectedCustomDeck();
                SetDeckStatus(selected == null
                    ? $"Using {selectedOption.Label}."
                    : $"Using {selected.Name}.",
                    new Vector4(0.6f, 0.8f, 1.0f, 1.0f));
            }

            var selectedDeck = plugin.CardDeckService.GetSelectedCustomDeck();
            if (selectedDeck == null)
            {
                return;
            }

            ImGui.Spacing();
            ImGui.TextWrapped(selectedDeck.FolderPath);

            if (ImGui.Button("Rescan Selected"))
            {
                var result = plugin.CardDeckService.RescanSelectedDeck();
                if (result != null)
                {
                    SetDeckStatus(result);
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("Remove Selected"))
            {
                string removedName = selectedDeck.Name;
                plugin.CardDeckService.RemoveDeck(selectedDeck.Id);
                SetDeckStatus($"Removed {removedName}.", new Vector4(1.0f, 0.75f, 0.2f, 1.0f));
            }
        }
        private void DrawDeckArtScaleControl()
        {
            var selectedDeck = plugin.CardDeckService.GetSelectedCustomDeck();
            if (selectedDeck == null)
            {
                return;
            }

            float cardArtScale = plugin.CardDeckService.GetCardArtScale(selectedDeck);
            float availableWidth = ImGui.GetContentRegionAvail().X;
            var style = ImGui.GetStyle();
            float resetButtonWidth = ImGui.CalcTextSize("Reset").X + style.FramePadding.X * 2f;
            bool showReset = Math.Abs(cardArtScale - 1.0f) > 0.001f;
            bool drawResetInline = showReset && availableWidth > resetButtonWidth + style.ItemSpacing.X + 120f;
            float sliderWidth = drawResetInline
                ? availableWidth - resetButtonWidth - style.ItemSpacing.X
                : availableWidth;

            ImGui.SetNextItemWidth(sliderWidth);
            if (ImGui.SliderFloat("##deckArtScaleSlider", ref cardArtScale, CardDeckService.MinCardArtScale, CardDeckService.MaxCardArtScale, "%.2fx"))
            {
                plugin.CardDeckService.SetSelectedCustomDeckArtScale(cardArtScale);
            }

            if (showReset)
            {
                if (drawResetInline)
                {
                    ImGui.SameLine();
                }

                if (ImGui.Button("Reset##deckArtScaleReset"))
                {
                    plugin.CardDeckService.SetSelectedCustomDeckArtScale(1.0f);
                }
            }
        }
        private void DrawDeckFolderControls()
        {
            var style = ImGui.GetStyle();
            float browseButtonWidth = ImGui.CalcTextSize("Browse...").X + style.FramePadding.X * 2f;
            float inputWidth = Math.Max(120f, ImGui.GetContentRegionAvail().X - browseButtonWidth - style.ItemSpacing.X);

            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputText("##deckFolderPath", ref deckFolderPathInput, 512);
            ImGui.SameLine();
            if (ImGui.Button("Browse..."))
            {
                OpenDeckFolderBrowser();
            }

            bool canAdd = !string.IsNullOrWhiteSpace(deckFolderPathInput);
            if (!canAdd)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Add Deck"))
            {
                var result = plugin.CardDeckService.AddOrUpdateDeck(deckFolderPathInput, selectDeck: true);
                SetDeckStatus(result);
                if (result.IsUsable)
                {
                    deckFolderPathInput = result.NormalizedFolderPath;
                }
            }

            if (!canAdd)
            {
                ImGui.EndDisabled();
            }
        }
        private void OpenDeckFolderBrowser()
        {
            deckFolderBrowser.Open(new[]
            {
                deckFolderPathInput,
                plugin.CardDeckService.GetSelectedCustomDeck()?.FolderPath ?? string.Empty,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty
            });
            shouldOpenDeckFolderBrowser = true;
            shouldFocusDeckFolderBrowserPath = true;
        }
        private void DrawDeckFolderBrowserPopup()
        {
            const string popupId = "Select Deck Folder###DeckFolderBrowser";

            if (shouldOpenDeckFolderBrowser)
            {
                ImGui.OpenPopup(popupId);
                shouldOpenDeckFolderBrowser = false;
            }

            ImGui.SetNextWindowSize(new Vector2(720f, 520f), ImGuiCond.FirstUseEver);
            bool isOpen = true;
            if (!ImGui.BeginPopupModal(popupId, ref isOpen, ImGuiWindowFlags.NoCollapse))
            {
                return;
            }

            ImGui.TextUnformatted("Folder");
            if (shouldFocusDeckFolderBrowserPath)
            {
                ImGui.SetKeyboardFocusHere();
                shouldFocusDeckFolderBrowserPath = false;
            }

            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##deckFolderBrowserPath", ref deckFolderBrowser.CurrentPath, 512, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                deckFolderBrowser.Navigate(deckFolderBrowser.CurrentPath);
            }

            ImGui.Spacing();
            DrawDeckFolderBrowserShortcuts();

            ImGui.Spacing();
            if (ImGui.BeginChild("DeckFolderBrowserDirectoryList", new Vector2(0f, -55f), true))
            {
                DrawDeckFolderBrowserDirectoryList();
                ImGui.EndChild();
            }

            if (!string.IsNullOrWhiteSpace(deckFolderBrowser.ErrorMessage))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.35f, 0.35f, 1.0f));
                ImGui.TextWrapped(deckFolderBrowser.ErrorMessage);
                ImGui.PopStyleColor();
            }

            bool canUseCurrentFolder = deckFolderBrowser.TryGetCurrentFolder(out string selectedFolderPath);
            if (!canUseCurrentFolder)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Use This Folder"))
            {
                deckFolderPathInput = selectedFolderPath;
                deckFolderBrowser.SetCurrentFolder(selectedFolderPath);
                ImGui.CloseCurrentPopup();
            }

            if (!canUseCurrentFolder)
            {
                ImGui.EndDisabled();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
        private void DrawDeckFolderBrowserShortcuts()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (DeckFolderBrowserModel.IsExistingDirectory(home))
            {
                if (ImGui.Button("Home"))
                {
                    deckFolderBrowser.Navigate(home);
                }
                ImGui.SameLine();
            }

            string? parent = deckFolderBrowser.GetParentDirectory();
            if (!string.IsNullOrWhiteSpace(parent))
            {
                if (ImGui.Button("Parent"))
                {
                    deckFolderBrowser.Navigate(parent);
                }
                ImGui.SameLine();
            }

            foreach (string root in DeckFolderBrowserModel.GetRootDirectories())
            {
                if (ImGui.Button($"{root}##deckRoot{root}"))
                {
                    deckFolderBrowser.Navigate(root);
                }
                ImGui.SameLine();
            }

            ImGui.NewLine();
        }
        private void DrawDeckFolderBrowserDirectoryList()
        {
            var snapshot = deckFolderBrowser.GetCurrentDirectorySnapshot();
            if (!string.IsNullOrWhiteSpace(snapshot.ParentPath) && ImGui.Selectable(".."))
            {
                deckFolderBrowser.Navigate(snapshot.ParentPath);
                return;
            }

            foreach (var directory in snapshot.Directories)
            {
                if (ImGui.Selectable($"{directory.Name}/##{directory.FullPath}"))
                {
                    deckFolderBrowser.Navigate(directory.FullPath);
                    return;
                }
            }
        }
        private void DrawDeckStatus()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, deckStatusColor);
            ImGui.TextWrapped(deckStatusMessage);
            ImGui.PopStyleColor();

            if (!string.IsNullOrWhiteSpace(deckStatusDetails))
            {
                ImGui.TextWrapped(deckStatusDetails);
            }
        }
        private void SetDeckStatus(CardDeckValidationResult result)
        {
            Vector4 color = !result.IsUsable
                ? new Vector4(1.0f, 0.35f, 0.35f, 1.0f)
                : result.FoundCardCount < result.ExpectedCardCount
                    ? new Vector4(1.0f, 0.75f, 0.2f, 1.0f)
                    : new Vector4(0.3f, 0.85f, 0.45f, 1.0f);

            SetDeckStatus(result.Message, color, result.Details);
        }
        private void SetDeckStatus(string message, Vector4 color, string details = "")
        {
            deckStatusMessage = message;
            deckStatusDetails = details;
            deckStatusColor = color;
        }
    }
}

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
            deckFolderBrowserPath = GetInitialDeckBrowserPath();
            deckFolderBrowserError = string.Empty;
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
            if (ImGui.InputText("##deckFolderBrowserPath", ref deckFolderBrowserPath, 512, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                NavigateDeckFolderBrowser(deckFolderBrowserPath);
            }

            ImGui.Spacing();
            DrawDeckFolderBrowserShortcuts();

            ImGui.Spacing();
            if (ImGui.BeginChild("DeckFolderBrowserDirectoryList", new Vector2(0f, -55f), true))
            {
                DrawDeckFolderBrowserDirectoryList();
                ImGui.EndChild();
            }

            if (!string.IsNullOrWhiteSpace(deckFolderBrowserError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.35f, 0.35f, 1.0f));
                ImGui.TextWrapped(deckFolderBrowserError);
                ImGui.PopStyleColor();
            }

            bool canUseCurrentFolder = TryGetExistingDeckFolderPath(deckFolderBrowserPath, out string selectedFolderPath);
            if (!canUseCurrentFolder)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Use This Folder"))
            {
                deckFolderPathInput = selectedFolderPath;
                deckFolderBrowserPath = selectedFolderPath;
                deckFolderBrowserError = string.Empty;
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
            if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
            {
                if (ImGui.Button("Home"))
                {
                    NavigateDeckFolderBrowser(home);
                }
                ImGui.SameLine();
            }

            string? parent = GetParentDirectory(deckFolderBrowserPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                if (ImGui.Button("Parent"))
                {
                    NavigateDeckFolderBrowser(parent);
                }
                ImGui.SameLine();
            }

            foreach (string root in GetRootDirectories())
            {
                if (ImGui.Button($"{root}##deckRoot{root}"))
                {
                    NavigateDeckFolderBrowser(root);
                }
                ImGui.SameLine();
            }

            ImGui.NewLine();
        }
        private void DrawDeckFolderBrowserDirectoryList()
        {
            string currentPath;
            try
            {
                currentPath = ExpandDeckBrowserPath(deckFolderBrowserPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                deckFolderBrowserError = ex.Message;
                return;
            }

            if (!Directory.Exists(currentPath))
            {
                deckFolderBrowserError = "Folder does not exist.";
                return;
            }

            try
            {
                deckFolderBrowserPath = currentPath;
                deckFolderBrowserError = string.Empty;

                string? parent = GetParentDirectory(currentPath);
                if (!string.IsNullOrWhiteSpace(parent) && ImGui.Selectable(".."))
                {
                    NavigateDeckFolderBrowser(parent);
                    return;
                }

                var directories = Directory.EnumerateDirectories(currentPath)
                    .Select(path => new DirectoryInfo(path))
                    .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var directory in directories)
                {
                    string name = string.IsNullOrWhiteSpace(directory.Name) ? directory.FullName : directory.Name;
                    if (ImGui.Selectable($"{name}/##{directory.FullName}"))
                    {
                        NavigateDeckFolderBrowser(directory.FullName);
                        return;
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                deckFolderBrowserError = ex.Message;
            }
        }
        private string GetInitialDeckBrowserPath()
        {
            string[] candidates =
            {
                deckFolderPathInput,
                plugin.CardDeckService.GetSelectedCustomDeck()?.FolderPath ?? string.Empty,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty
            };

            foreach (string candidate in candidates)
            {
                string browsablePath = GetBrowsableDeckFolderPath(candidate);
                if (!string.IsNullOrWhiteSpace(browsablePath))
                {
                    return browsablePath;
                }
            }

            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.GetPathRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory
                : Path.DirectorySeparatorChar.ToString();
        }
        private void NavigateDeckFolderBrowser(string path)
        {
            if (!TryGetExistingDeckFolderPath(path, out string browsablePath))
            {
                deckFolderBrowserError = "Folder does not exist.";
                return;
            }

            deckFolderBrowserPath = browsablePath;
            deckFolderBrowserError = string.Empty;
        }
        private static string GetBrowsableDeckFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                string expandedPath = ExpandDeckBrowserPath(path);
                if (Directory.Exists(expandedPath))
                {
                    return expandedPath;
                }

                if (File.Exists(expandedPath))
                {
                    return Path.GetDirectoryName(expandedPath) ?? string.Empty;
                }

                string? parent = Path.GetDirectoryName(expandedPath);
                while (!string.IsNullOrWhiteSpace(parent))
                {
                    if (Directory.Exists(parent))
                    {
                        return parent;
                    }

                    parent = Path.GetDirectoryName(parent);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return string.Empty;
            }

            return string.Empty;
        }
        private static bool TryGetExistingDeckFolderPath(string path, out string folderPath)
        {
            folderPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string expandedPath = ExpandDeckBrowserPath(path);
                if (!Directory.Exists(expandedPath))
                {
                    return false;
                }

                folderPath = expandedPath;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }
        private static string ExpandDeckBrowserPath(string path)
        {
            string expandedPath = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"').Trim('\''));
            if (expandedPath == "~" || expandedPath.StartsWith("~/", StringComparison.Ordinal) || expandedPath.StartsWith("~\\", StringComparison.Ordinal))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(home))
                {
                    expandedPath = expandedPath == "~"
                        ? home
                        : Path.Combine(home, expandedPath[2..]);
                }
            }

            return Path.GetFullPath(expandedPath);
        }
        private static string? GetParentDirectory(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                var directory = new DirectoryInfo(ExpandDeckBrowserPath(path));
                return directory.Parent?.FullName;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
        private static IReadOnlyList<string> GetRootDirectories()
        {
            try
            {
                return Directory.GetLogicalDrives()
                    .Where(Directory.Exists)
                    .OrderBy(root => root, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                string fallbackRoot = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Path.GetPathRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory
                    : Path.DirectorySeparatorChar.ToString();
                return new[] { fallbackRoot };
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

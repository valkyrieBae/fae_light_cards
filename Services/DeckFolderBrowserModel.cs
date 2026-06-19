using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace FaeLightCards
{
    internal readonly record struct DeckFolderBrowserEntry(string Name, string FullPath);

    internal readonly record struct DeckFolderBrowserDirectorySnapshot(
        string? ParentPath,
        IReadOnlyList<DeckFolderBrowserEntry> Directories);

    internal sealed class DeckFolderBrowserModel
    {
        public string CurrentPath = string.Empty;

        public string ErrorMessage { get; private set; } = string.Empty;

        public void Open(IEnumerable<string> initialPathCandidates)
        {
            CurrentPath = GetInitialDeckBrowserPath(initialPathCandidates);
            ErrorMessage = string.Empty;
        }

        public void SetCurrentFolder(string folderPath)
        {
            CurrentPath = folderPath;
            ErrorMessage = string.Empty;
        }

        public void Navigate(string path)
        {
            if (!TryGetExistingDeckFolderPath(path, out string browsablePath))
            {
                ErrorMessage = "Folder does not exist.";
                return;
            }

            CurrentPath = browsablePath;
            ErrorMessage = string.Empty;
        }

        public bool TryGetCurrentFolder(out string folderPath)
        {
            return TryGetExistingDeckFolderPath(CurrentPath, out folderPath);
        }

        public string? GetParentDirectory()
        {
            return GetParentDirectory(CurrentPath);
        }

        public DeckFolderBrowserDirectorySnapshot GetCurrentDirectorySnapshot()
        {
            string currentPath;
            try
            {
                currentPath = ExpandDeckBrowserPath(CurrentPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                ErrorMessage = ex.Message;
                return new DeckFolderBrowserDirectorySnapshot(null, Array.Empty<DeckFolderBrowserEntry>());
            }

            if (!Directory.Exists(currentPath))
            {
                ErrorMessage = "Folder does not exist.";
                return new DeckFolderBrowserDirectorySnapshot(null, Array.Empty<DeckFolderBrowserEntry>());
            }

            CurrentPath = currentPath;
            ErrorMessage = string.Empty;
            string? parent = GetParentDirectory(currentPath);

            try
            {
                var directories = Directory.EnumerateDirectories(currentPath)
                    .Select(path => new DirectoryInfo(path))
                    .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(directory =>
                    {
                        string name = string.IsNullOrWhiteSpace(directory.Name) ? directory.FullName : directory.Name;
                        return new DeckFolderBrowserEntry(name, directory.FullName);
                    })
                    .ToList();

                return new DeckFolderBrowserDirectorySnapshot(parent, directories);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                ErrorMessage = ex.Message;
                return new DeckFolderBrowserDirectorySnapshot(parent, Array.Empty<DeckFolderBrowserEntry>());
            }
        }

        public static IReadOnlyList<string> GetRootDirectories()
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

        public static bool IsExistingDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                return Directory.Exists(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static string GetInitialDeckBrowserPath(IEnumerable<string> candidates)
        {
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
    }
}

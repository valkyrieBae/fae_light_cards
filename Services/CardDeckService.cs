using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace FaeLightCards
{
    public readonly record struct CardDeckOption(string Id, string Label);

    public class CardDeckValidationResult
    {
        public bool IsUsable { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string NormalizedFolderPath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int FoundCardCount { get; set; }
        public int ExpectedCardCount { get; set; } = CardDeckService.ExpectedFaceCardCount;
    }

    public class CardDeckService
    {
        public const int ExpectedFaceCardCount = 52;
        public const string DecksDirectoryName = "decks";
        public const string CardsDirectoryName = "cards";
        public const string DarkBackFileName = "back_dark.png";
        public const string LightBackFileName = "back_light.png";
        public const float DefaultCardTextureWidth = 243f;
        public const float DefaultCardTextureHeight = 340f;
        public const float PyramidTextureScaleMultiplier = 6.0f;
        public const float MinCardArtScale = 0.25f;
        public const float MaxCardArtScale = 2.0f;

        private const float DefaultCardAspectRatio = DefaultCardTextureWidth / DefaultCardTextureHeight;
        private const float AspectRatioWarningRelativeThreshold = 0.05f;

        private static readonly string[] ExpectedFaceCardFiles = Enum.GetValues<Suit>()
            .SelectMany(suit => Enum.GetValues<Rank>().Select(rank => new Card(suit, rank).GetFileName()))
            .ToArray();

        private static readonly StringComparer PathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private static readonly StringComparer CardFileComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private static readonly IncludedDeck[] IncludedDecks =
        {
            new(Configuration.FaeDeckDesignId, "Fae", "fae"),
            new(Configuration.NormalDeckDesignId, "Normal", "normal")
        };

        private readonly Configuration configuration;
        private readonly IPluginLog log;
        private readonly string includedDecksDirectory;
        private readonly string fallbackCardsDirectory;
        private readonly Dictionary<string, ISharedImmediateTexture> textureCache = new(StringComparer.Ordinal);

        private readonly record struct IncludedDeck(string Id, string Label, string DirectoryName);
        private readonly record struct PngFileInfo(string Path, string Name);
        private readonly record struct PngDimensions(int Width, int Height);

        public CardDeckService(Configuration configuration, string assemblyDirectory, IPluginLog log)
        {
            this.configuration = configuration;
            this.log = log;
            this.includedDecksDirectory = Path.Combine(assemblyDirectory, DecksDirectoryName);
            this.fallbackCardsDirectory = GetIncludedDeckCardsDirectory(Configuration.NormalDeckDesignId);
            EnsureValidSelection();
        }

        public IReadOnlyList<CardDeckOption> GetDeckOptions()
        {
            var options = IncludedDecks
                .Select(deck => new CardDeckOption(deck.Id, deck.Label))
                .ToList();

            foreach (var deck in configuration.CustomDeckDesigns.Where(IsStoredDeckUsable))
            {
                options.Add(new CardDeckOption(deck.Id, $"{deck.Name} ({deck.FoundCardCount}/{ExpectedFaceCardCount})"));
            }

            return options;
        }

        public CustomDeckDesignConfig? GetSelectedCustomDeck()
        {
            if (IsIncludedDeckId(configuration.SelectedDeckDesignId))
            {
                return null;
            }

            return configuration.CustomDeckDesigns.FirstOrDefault(deck => deck.Id == configuration.SelectedDeckDesignId);
        }

        public void SelectDeck(string deckId)
        {
            if (!IsIncludedDeckId(deckId) &&
                !configuration.CustomDeckDesigns.Any(deck => deck.Id == deckId))
            {
                deckId = Configuration.DefaultDeckDesignId;
            }

            if (configuration.SelectedDeckDesignId == deckId)
            {
                return;
            }

            configuration.SelectedDeckDesignId = deckId;
            ClearTextureCache();
            configuration.Save();
        }

        public float GetSelectedCardArtScale()
        {
            var selected = GetSelectedCustomDeck();
            return selected == null ? 1.0f : NormalizeCardArtScale(selected.CardArtScale);
        }

        public float GetCardArtScale(CustomDeckDesignConfig deck)
        {
            return NormalizeCardArtScale(deck.CardArtScale);
        }

        public void SetSelectedCustomDeckArtScale(float scale)
        {
            var selected = GetSelectedCustomDeck();
            if (selected == null)
            {
                return;
            }

            float normalizedScale = NormalizeCardArtScale(scale);
            if (Math.Abs(selected.CardArtScale - normalizedScale) <= 0.001f)
            {
                return;
            }

            selected.CardArtScale = normalizedScale;
            configuration.Save();
        }

        public Vector2 GetDeckCardDisplaySize(float deckScale)
        {
            return GetCardDisplaySize(deckScale, 1.0f);
        }

        public Vector2 GetHandCardDisplaySize(float handScale)
        {
            return GetCardDisplaySize(handScale, UIConstants.CardTextureScaleMultiplier);
        }

        public Vector2 GetPyramidBaseCardDisplaySize()
        {
            return GetCardDisplaySize(1.0f, PyramidTextureScaleMultiplier);
        }

        public CardDeckValidationResult AddOrUpdateDeck(string rawFolderPath, bool selectDeck)
        {
            var result = ValidateDeckFolder(rawFolderPath);
            if (!result.IsUsable)
            {
                return result;
            }

            var existing = configuration.CustomDeckDesigns.FirstOrDefault(deck =>
                PathsEqual(deck.FolderPath, result.NormalizedFolderPath));

            if (existing == null)
            {
                existing = new CustomDeckDesignConfig
                {
                    Id = $"custom:{Guid.NewGuid():N}",
                    CardArtScale = 1.0f
                };
                configuration.CustomDeckDesigns.Add(existing);
            }
            else if (existing.CardArtScale <= 0f || float.IsNaN(existing.CardArtScale) || float.IsInfinity(existing.CardArtScale))
            {
                existing.CardArtScale = 1.0f;
            }

            existing.Name = GetUniqueDeckName(result.DisplayName, existing.Id);
            existing.FolderPath = result.NormalizedFolderPath;
            existing.FoundCardCount = result.FoundCardCount;

            if (selectDeck)
            {
                configuration.SelectedDeckDesignId = existing.Id;
            }

            ClearTextureCache();
            configuration.Save();
            return result;
        }

        public CardDeckValidationResult? RescanSelectedDeck()
        {
            var selected = GetSelectedCustomDeck();
            if (selected == null)
            {
                return null;
            }

            return AddOrUpdateDeck(selected.FolderPath, selectDeck: true);
        }

        public void RemoveDeck(string deckId)
        {
            var deck = configuration.CustomDeckDesigns.FirstOrDefault(existing => existing.Id == deckId);
            if (deck == null)
            {
                return;
            }

            configuration.CustomDeckDesigns.Remove(deck);
            if (configuration.SelectedDeckDesignId == deckId)
            {
                configuration.SelectedDeckDesignId = Configuration.DefaultDeckDesignId;
            }

            ClearTextureCache();
            configuration.Save();
        }

        public CardDeckValidationResult ValidateDeckFolder(string rawFolderPath)
        {
            var result = new CardDeckValidationResult();
            string normalizedPath;

            try
            {
                normalizedPath = NormalizeFolderPath(rawFolderPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                result.Message = ex.Message;
                return result;
            }

            result.NormalizedFolderPath = normalizedPath;
            result.DisplayName = GetDisplayName(normalizedPath);

            if (!Directory.Exists(normalizedPath))
            {
                result.Message = "Deck folder does not exist.";
                result.Details = normalizedPath;
                return result;
            }

            string cardsDirectory = Path.Combine(normalizedPath, CardsDirectoryName);
            if (!Directory.Exists(cardsDirectory))
            {
                result.Message = $"Deck folder must contain a '{CardsDirectoryName}' folder.";
                result.Details = $"Expected: {cardsDirectory}";
                return result;
            }

            try
            {
                var exactExpectedNames = new HashSet<string>(ExpectedFaceCardFiles, CardFileComparer);
                var ignoreCaseExpectedNames = new HashSet<string>(ExpectedFaceCardFiles, StringComparer.OrdinalIgnoreCase);
                var pngFiles = Directory.EnumerateFiles(cardsDirectory)
                    .Where(path => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                    .Select(path => new PngFileInfo(path, Path.GetFileName(path) ?? string.Empty))
                    .Where(file => !string.IsNullOrWhiteSpace(file.Name))
                    .ToList();
                var pngFileNames = pngFiles.Select(file => file.Name).ToList();

                var foundNames = pngFileNames
                    .Where(exactExpectedNames.Contains)
                    .Distinct(CardFileComparer)
                    .ToList();

                var missingNames = ExpectedFaceCardFiles
                    .Except(foundNames, CardFileComparer)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();

                var casingMismatches = pngFileNames
                    .Where(name => !exactExpectedNames.Contains(name) && ignoreCaseExpectedNames.Contains(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var unrecognizedFiles = pngFileNames
                    .Where(name => !ignoreCaseExpectedNames.Contains(name) &&
                                   !string.Equals(name, DarkBackFileName, StringComparison.OrdinalIgnoreCase) &&
                                   !string.Equals(name, LightBackFileName, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                result.FoundCardCount = foundNames.Count;
                result.Message = $"Found {result.FoundCardCount}/{ExpectedFaceCardCount} cards!";

                var details = new List<string>();
                if (result.FoundCardCount == 0)
                {
                    result.Message = "No recognized card replacements found.";
                    result.Details = "Card files must use names like clubs_2.png, hearts_A.png, and spades_K.png.";
                    return result;
                }

                result.IsUsable = true;
                if (missingNames.Count > 0)
                {
                    result.Message += " Missing cards will use the default deck.";
                    details.Add($"Missing: {FormatFileList(missingNames)}");
                }

                if (casingMismatches.Count > 0)
                {
                    details.Add($"Filename casing does not match expected names: {FormatFileList(casingMismatches)}");
                }

                if (unrecognizedFiles.Count > 0)
                {
                    details.Add($"Ignored unrecognized PNGs: {FormatFileList(unrecognizedFiles)}");
                }

                string aspectRatioWarning = BuildAspectRatioWarning(pngFiles
                    .Where(file => exactExpectedNames.Contains(file.Name) ||
                                   string.Equals(file.Name, DarkBackFileName, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(file.Name, LightBackFileName, StringComparison.OrdinalIgnoreCase))
                    .Select(file => file.Path)
                    .ToList());
                if (!string.IsNullOrWhiteSpace(aspectRatioWarning))
                {
                    details.Add(aspectRatioWarning);
                }

                result.Details = string.Join(" ", details);
                return result;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                result.Message = "Could not read the deck folder.";
                result.Details = ex.Message;
                return result;
            }
        }

        private Vector2 GetCardDisplaySize(float layoutScale, float textureScaleMultiplier)
        {
            float safeLayoutScale = Math.Max(0.01f, layoutScale);
            float safeTextureScaleMultiplier = Math.Max(0.01f, textureScaleMultiplier);
            float cardArtScale = GetSelectedCardArtScale();
            float scale = safeLayoutScale * safeTextureScaleMultiplier * cardArtScale;
            return new Vector2(DefaultCardTextureWidth * scale, DefaultCardTextureHeight * scale);
        }

        private static float NormalizeCardArtScale(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                return 1.0f;
            }

            return Math.Clamp(scale, MinCardArtScale, MaxCardArtScale);
        }

        public ISharedImmediateTexture GetCardTexture(Card card)
        {
            return GetTexture(card.GetFileName());
        }

        public ISharedImmediateTexture GetCardBackTexture(bool light = false)
        {
            return GetTexture(light ? LightBackFileName : DarkBackFileName);
        }

        public void ClearTextureCache()
        {
            textureCache.Clear();
        }

        private ISharedImmediateTexture GetTexture(string fileName)
        {
            string selectedDeckId = configuration.SelectedDeckDesignId;
            string cacheKey = $"{selectedDeckId}|{fileName}";
            if (textureCache.TryGetValue(cacheKey, out var texture))
            {
                return texture;
            }

            string fallbackPath = Path.Combine(fallbackCardsDirectory, fileName);
            string texturePath = ResolveSelectedTexturePath(fileName) ?? fallbackPath;

            try
            {
                texture = Plugin.TextureProvider.GetFromFile(texturePath);
            }
            catch (Exception ex) when (texturePath != fallbackPath)
            {
                log.Warning($"Failed to load card texture '{texturePath}': {ex.Message}");
                texture = Plugin.TextureProvider.GetFromFile(fallbackPath);
            }

            textureCache[cacheKey] = texture;
            return texture;
        }

        private string? ResolveSelectedTexturePath(string fileName)
        {
            var includedDeck = GetIncludedDeck(configuration.SelectedDeckDesignId);
            if (includedDeck != null)
            {
                string includedPath = Path.Combine(
                    includedDecksDirectory,
                    includedDeck.Value.DirectoryName,
                    CardsDirectoryName,
                    fileName);
                return File.Exists(includedPath) ? includedPath : null;
            }

            var selected = GetSelectedCustomDeck();
            if (selected == null || string.IsNullOrWhiteSpace(selected.FolderPath))
            {
                return null;
            }

            string customPath = Path.Combine(selected.FolderPath, CardsDirectoryName, fileName);
            return File.Exists(customPath) ? customPath : null;
        }

        private void EnsureValidSelection()
        {
            bool shouldSave = configuration.CustomDeckDesigns.RemoveAll(deck => !IsStoredDeckUsable(deck)) > 0;
            if (string.Equals(configuration.SelectedDeckDesignId, Configuration.LegacyDefaultDeckDesignId, StringComparison.Ordinal))
            {
                configuration.SelectedDeckDesignId = Configuration.DefaultDeckDesignId;
                shouldSave = true;
            }

            if (!IsIncludedDeckId(configuration.SelectedDeckDesignId) &&
                !configuration.CustomDeckDesigns.Any(deck => deck.Id == configuration.SelectedDeckDesignId))
            {
                configuration.SelectedDeckDesignId = Configuration.DefaultDeckDesignId;
                shouldSave = true;
            }

            if (shouldSave)
            {
                configuration.Save();
            }
        }

        private static bool IsStoredDeckUsable(CustomDeckDesignConfig deck)
        {
            return !string.IsNullOrWhiteSpace(deck.Id) &&
                   !string.IsNullOrWhiteSpace(deck.Name) &&
                   !string.IsNullOrWhiteSpace(deck.FolderPath);
        }

        private static bool IsIncludedDeckId(string deckId)
        {
            return IncludedDecks.Any(deck => string.Equals(deck.Id, deckId, StringComparison.Ordinal));
        }

        private static IncludedDeck? GetIncludedDeck(string deckId)
        {
            foreach (var deck in IncludedDecks)
            {
                if (string.Equals(deck.Id, deckId, StringComparison.Ordinal))
                {
                    return deck;
                }
            }

            return null;
        }

        private string GetIncludedDeckCardsDirectory(string deckId)
        {
            var deck = GetIncludedDeck(deckId) ?? GetIncludedDeck(Configuration.DefaultDeckDesignId);
            if (deck == null)
            {
                throw new InvalidOperationException("No included card deck is configured.");
            }

            return Path.Combine(includedDecksDirectory, deck.Value.DirectoryName, CardsDirectoryName);
        }

        private string GetUniqueDeckName(string baseName, string currentDeckId)
        {
            string candidate = string.IsNullOrWhiteSpace(baseName) ? "Custom Deck" : baseName;
            var usedNames = configuration.CustomDeckDesigns
                .Where(deck => deck.Id != currentDeckId)
                .Select(deck => deck.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!usedNames.Contains(candidate))
            {
                return candidate;
            }

            int suffix = 2;
            while (usedNames.Contains($"{candidate} {suffix}"))
            {
                suffix++;
            }

            return $"{candidate} {suffix}";
        }

        private static string NormalizeFolderPath(string rawFolderPath)
        {
            if (string.IsNullOrWhiteSpace(rawFolderPath))
            {
                throw new ArgumentException("Enter a deck folder path.");
            }

            string path = Environment.ExpandEnvironmentVariables(rawFolderPath.Trim().Trim('"').Trim('\''));
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Enter a deck folder path.");
            }

            if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(home))
                {
                    path = path == "~"
                        ? home
                        : Path.Combine(home, path[2..]);
                }
            }

            return Path.GetFullPath(path);
        }

        private static string GetDisplayName(string folderPath)
        {
            string trimmed = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? "Custom Deck" : name;
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                return PathComparer.Equals(
                    Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            catch
            {
                return PathComparer.Equals(left, right);
            }
        }

        private static string BuildAspectRatioWarning(IReadOnlyList<string> pngPaths)
        {
            var unusualFiles = new List<string>();
            foreach (string path in pngPaths)
            {
                if (!TryReadPngDimensions(path, out var dimensions))
                {
                    continue;
                }

                float aspectRatio = dimensions.Width / (float)dimensions.Height;
                float relativeDifference = Math.Abs(aspectRatio - DefaultCardAspectRatio) / DefaultCardAspectRatio;
                if (relativeDifference > AspectRatioWarningRelativeThreshold)
                {
                    unusualFiles.Add($"{Path.GetFileName(path)} ({dimensions.Width}x{dimensions.Height})");
                }
            }

            if (unusualFiles.Count == 0)
            {
                return string.Empty;
            }

            return $"Warning: These PNGs have an unusual card aspect ratio and will be stretched into the default {DefaultCardTextureWidth:0}x{DefaultCardTextureHeight:0} frame: {FormatFileList(unusualFiles)}.";
        }

        private static bool TryReadPngDimensions(string path, out PngDimensions dimensions)
        {
            dimensions = default;

            Span<byte> header = stackalloc byte[24];
            try
            {
                using var stream = File.OpenRead(path);
                if (stream.Read(header) < header.Length)
                {
                    return false;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return false;
            }

            if (header[0] != 0x89 ||
                header[1] != 0x50 ||
                header[2] != 0x4E ||
                header[3] != 0x47 ||
                header[4] != 0x0D ||
                header[5] != 0x0A ||
                header[6] != 0x1A ||
                header[7] != 0x0A)
            {
                return false;
            }

            int width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
            int height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            dimensions = new PngDimensions(width, height);
            return true;
        }

        private static string FormatFileList(IReadOnlyList<string> files)
        {
            const int maxShown = 8;
            var shown = files.Take(maxShown).ToList();
            string message = string.Join(", ", shown);
            if (files.Count > maxShown)
            {
                message += $", and {files.Count - maxShown} more";
            }

            return message;
        }
    }
}

# Fae Light Cards

Fae Light Cards is a Dalamud plugin made by Fae Light for playing social card games together in FFXIV. The plugin adds in-game card windows, prompts, table/player views, card art options, sound effects, local NPC play, and networked rooms for playing with other people.

The plugin is named Fae Light Cards; the game itself is just a tabletop card game you can play with friends.

## Status

This is an unofficial Dalamud plugin targeting Dalamud API level 15. It is intended for casual/social play and is not affiliated with Square Enix, XIVLauncher, or the Dalamud project.

## Installation

### Install Through Dalamud

Once a custom plugin repository URL is published for this project:

1. Open Dalamud's plugin installer in-game with `/xlplugins`.
2. Open the installer settings.
3. Add the published custom repository URL under the custom/experimental plugin repository list.
4. Save the repository list and refresh plugins.
5. Search for `Fae Light Cards` and install it.
6. Open the plugin with `/faecards`.

Use the custom repository URL exactly as published. It should be a raw JSON URL, not the normal GitHub page URL.

### Local Development Install

Build the project and load the resulting plugin through Dalamud's local development plugin flow:

```bash
dotnet build FaeLightCards.csproj
```

For release packaging, build Release from a clean output directory:

```bash
dotnet clean FaeLightCards.csproj -c Release
dotnet build FaeLightCards.csproj -c Release
```

If `bin/Release` contains old plugin files, remove the stale output before building so the generated ZIP only contains the current plugin files.

DalamudPackager writes the install ZIP to:

```text
bin/Release/FaeLightCards/latest.zip
```

Use that ZIP as the downloadable artifact for a custom repository entry or GitHub release.

## How To Play

Open the plugin with:

```text
/faecards
```

Open settings with:

```text
/faecards settings
/faecards config
```

### Starting A Game

1. Choose `Player` or `Dealer`.
2. Choose `Local-Only` or `Networked`.
3. In local-only mode, the plugin fills empty seats with NPC players.
4. In networked mode, the dealer creates a room and shares the room code. Other players choose `Player`, choose `Networked`, then enter the room code.

Network settings are available from the settings window. Use `Test Connection` if a networked room cannot connect.

### Phase 1: Accumulation

Each non-dealer player receives up to four cards through four prompt rounds:

1. Red or Black
2. Higher or Lower
3. Inside or Outside
4. Guess the Suit

Correct guesses give drinks to another player. Wrong guesses make the guesser take drinks. The drink amount increases each round: 1, 2, 3, then 4.

### Phase 2: Pyramid

The dealer builds a fifteen-card pyramid. Cards are flipped from the bottom row upward.

When a flipped pyramid card matches a card in a player's hand by rank, that player can play the matching card onto the pyramid and give drinks to another player. Higher rows are worth more drinks.

The phase ends after the pyramid is complete.

### Ride The Bus

The player with the most cards left rides the bus. If there is a tie, the dealer chooses or the plugin selects from tied players depending on the mode.

The rider must repeatedly guess whether the next card is higher or lower than the current card. Correct guesses build a streak. Wrong guesses reset the streak and the rider drinks. The target streak length is configurable in settings.

## Settings

The settings window includes:

- Gameplay options such as NPC count and bus size.
- Window position and scale controls.
- Card art selection and custom deck support.
- Sound effect selection.
- Network server selection and connection testing.
- Developer tools, gated behind an acknowledgement.

## Custom Decks

Bundled decks live under `decks/`. Custom decks should provide a `cards` folder with all 52 face cards and both back images:

```text
cards/
  clubs_2.png
  ...
  spades_A.png
  back_dark.png
  back_light.png
```

The plugin validates custom deck folders from the settings window.

## Publishing For Dalamud From GitHub

Dalamud custom plugin repositories use a JSON file that is reachable by unauthenticated HTTP `GET`. That JSON file contains an array of plugin store entries. The store entry points Dalamud at a ZIP artifact to install or update.

For GitHub, the repository URL users add should usually look like this:

```text
https://raw.githubusercontent.com/<owner>/<repo>/<branch>/repo.json
```

Do not give users this kind of URL as the Dalamud repository URL:

```text
https://github.com/<owner>/<repo>/blob/<branch>/repo.json
```

That is a GitHub HTML page, not the raw JSON file Dalamud needs.

Recommended release flow:

1. Build a clean Release package.
2. Upload `bin/Release/FaeLightCards/latest.zip` to a GitHub Release, using a clear asset name such as `FaeLightCards.zip`.
3. Add or update `repo.json` in the GitHub repository.
4. Set `DownloadLinkInstall`, `DownloadLinkUpdate`, and optionally `DownloadLinkTesting` to the direct ZIP download URL.
5. Tell users to add the raw `repo.json` URL to Dalamud.

Example `repo.json`:

```json
[
  {
    "Author": "Valkyrie Bae",
    "Name": "Fae Light Cards",
    "Punchline": "A tabletop card game plugin for FFXIV.",
    "Description": "Play a social tabletop card game directly in FFXIV with local NPCs or networked rooms.",
    "InternalName": "FaeLightCards",
    "AssemblyVersion": "1.0.0.11",
    "RepoUrl": "https://github.com/<owner>/<repo>",
    "ApplicableVersion": "any",
    "DalamudApiLevel": 15,
    "IsHide": false,
    "IsTestingExclusive": false,
    "DownloadLinkInstall": "https://github.com/<owner>/<repo>/releases/download/v1.0.0.11/FaeLightCards.zip",
    "DownloadLinkUpdate": "https://github.com/<owner>/<repo>/releases/download/v1.0.0.11/FaeLightCards.zip",
    "DownloadLinkTesting": "https://github.com/<owner>/<repo>/releases/download/v1.0.0.11/FaeLightCards.zip",
    "LastUpdate": 1781764619
  }
]
```

Replace the placeholder owner, repository, version, ZIP URL, and `LastUpdate` value for each release. `LastUpdate` is a Unix timestamp.

For official mainline Dalamud distribution, the current Dalamud docs direct new plugins through a pull request to `DalamudPluginsD17`, initially under the testing track. That path uses a `manifest.toml` pointing at a public repository and commit instead of asking users to add a custom repository URL.

References:

- Dalamud custom repository docs: https://dalamud.dev/plugin-publishing/custom-repositories
- Dalamud submission process: https://dalamud.dev/plugin-publishing/submission
- Dalamud plugin metadata docs: https://dalamud.dev/plugin-development/plugin-metadata

## Build

```bash
dotnet build FaeLightCards.csproj --no-restore
dotnet build FaeLightCards.csproj -c Release --no-restore
```

## Repository Notes

- `bin/`, `obj/`, local reference checkouts, and local publish helpers are ignored.
- Bundled card decks live under `decks/`.
- Bundled sound effects live under `sounds/`.
- The hardcoded default remote server address is configured in `Models/Configuration.cs`.

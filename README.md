# Fae Light Cards

Fae Light Cards is a Dalamud plugin made by Fae Light for playing social card games together in FFXIV. The plugin adds in-game card windows, prompts, table/player views, card art options, sound effects, local NPC play, and networked rooms for playing with other people.

The plugin is named Fae Light Cards; the game itself is just a tabletop card game you can play with friends.

## Status

This is an unofficial Dalamud plugin targeting Dalamud API level 15. It is intended for casual/social play and is not affiliated with Square Enix, XIVLauncher, or the Dalamud project.

## Installation

### Install Through Dalamud

Use this custom plugin repository URL:

```text
https://raw.githubusercontent.com/valkyrieBae/fae_light_cards/main/repo.json
```

1. Open Dalamud's plugin installer in-game with `/xlplugins`.
2. Open the installer settings.
3. Add the published custom repository URL under the custom/experimental plugin repository list.
4. Save the repository list and refresh plugins.
5. Search for `Fae Light Cards` and install it.
6. Open the plugin with `/faecards`.

Use the custom repository URL exactly as shown. It is a raw JSON URL, not the normal GitHub page URL.

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

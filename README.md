# Fae Light Cards

Fae Light Cards is a Dalamud plugin made by Fae Light for playin' card games!
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

Network settings are available from the settings window. Use `Test Connection` if a networked room can't connect.

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
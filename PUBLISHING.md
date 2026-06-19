# Publishing

Maintainer notes for publishing Fae Light Cards through a Dalamud custom plugin repository.

## Custom Repository

Dalamud custom plugin repositories use a JSON file that is reachable by unauthenticated HTTP `GET`. That JSON file contains an array of plugin store entries. Each store entry points Dalamud at a ZIP artifact to install or update.

For this repository, the URL users add in Dalamud is:

```text
https://raw.githubusercontent.com/valkyrieBae/fae_light_cards/main/repo.json
```

Do not use the GitHub `/blob/` page as the Dalamud repository URL. Dalamud needs the raw JSON response, not a GitHub HTML page.

## Build

For local development, build the project and load the resulting plugin through Dalamud's local development plugin flow:

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

Use that ZIP as the downloadable artifact for the custom repository entry or GitHub release.

## Release Flow

1. Build a clean Release package.
2. Upload `bin/Release/FaeLightCards/latest.zip` to GitHub Release tag `v1.0.0.11` as `FaeLightCards.zip`.
3. Update `repo.json` for the release version.
4. Set `DownloadLinkInstall` and `DownloadLinkUpdate` to the direct ZIP download URL.
5. Push `main`, then verify the raw `repo.json` URL and the GitHub Release asset are both reachable.

The current `repo.json` points to:

```text
https://github.com/valkyrieBae/fae_light_cards/releases/download/v1.0.0.11/FaeLightCards.zip
```

Update the version, ZIP URL, changelog, and `LastUpdate` value for each release. `LastUpdate` is a Unix timestamp.

References:

- Dalamud custom repository docs: https://dalamud.dev/plugin-publishing/custom-repositories
- Dalamud submission process: https://dalamud.dev/plugin-publishing/submission
- Dalamud plugin metadata docs: https://dalamud.dev/plugin-development/plugin-metadata

## Repository Notes

- `bin/`, `obj/`, local reference checkouts, and local publish helpers are ignored.
- Bundled card decks live under `decks/`.
- Bundled sound effects live under `sounds/`.
- The hardcoded default remote server address is configured in `Models/Configuration.cs`.

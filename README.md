# Shoko Offline Importer Plugin

A [Shoko Server](https://github.com/ShokoAnime/ShokoServer) plugin responsible for importing releases based on file names.

Report problems in the [issue tracker](https://github.com/ShokoAnime/Shoko.Plugin.OfflineImporter/issues)
or on [Discord](https://discord.gg/shokoanime).

## Features

- **Filename-based Importing** — Imports releases by parsing file names when online metadata sources are unavailable.
- **Custom Parsing Rules** — Configure regex-based rules to match your specific file naming conventions.
- **Automatic Matching Rules** — Set up auto-match rules to automatically approve or deny specific releases. Without any, the importer only links files by hand, and the server lists it as manual only for auto-linking. The first rule whose location, release group and episode range checks all pass decides. Episode ranges are checked against each cross-reference's AniDB episode, or else its episode of another source; a source that numbers episodes by season, such as TMDB, is checked against the number within the season, since a range carries no season.
- **ID Tags** — Reads ID tags such as `[anidb-123]`, `{anidb=a123,456}` or `[tmdbid-123]` from the file name and the folders above it. The importer itself uses the AniDB ones: the first series ID in the file name, else in the folder holding it, as the anime, and every episode ID in the file name, an AniDB movie (`m123`) counting as the episode it is.
- **Matching for Other Plugins** — Other plugins run the same file name matching and auto-match rules through the [abstractions package](#abstractions-package), and administrators through [the API](docs/api.md).

## Installation

### GUI (Recommended)

The plugin is listed in the Shoko Core Repository, which ships with Shoko Server, so there is no repository to add.

1. Open the Shoko Web UI and go to **Settings → Plugin Management → Browse**.
2. Find **Offline Importer** and click **Install** on the desired version.
3. Restart Shoko.

### Manual

1. Download the latest `OfflineImporter-<tag>-any.zip` from the [Releases](https://github.com/ShokoAnime/Shoko.Plugin.OfflineImporter/releases) page.
2. Extract the ZIP into a folder of its own in your Shoko **Plugins** folder.
3. Restart Shoko.

## Configuration

The plugin exposes the following settings in the Shoko UI:

| Setting | Default | Description |
|---|---|---|
| **Match Mode** | `Lax Mode` | How strict the importer should be when matching releases. |
| **Store existing hashes** | `true` | Include hashes stored in the database in the provided release info. |
| **Map release group to AniDB** | `true` | Attempt to map the release group to a known AniDB release group. |
| **Excluded release groups** | none | Groups never put on a release. When a name belongs to several AniDB groups, an excluded one is passed over for the next. |
| **Preferred release groups** | none | For a name several AniDB groups share, the group to use ahead of the others. |
| **Release group sources** | none | The source a group's releases get, in place of the one guessed from the file name. |
| **Enable remote refresh** | `true` | Allow refreshing AniDB data remotely if not found locally. |
| **Enable season searching** | `true` | Use the season number from the file name during search. |
| **Maximum search results to process** | `1` | The maximum number of search results to process per file. |

## API

The plugin reserves the `offline` namespace and serves administrators routes to match paths and look up release
groups; its settings go through the server's own configuration routes. See [the API reference](docs/api.md).

## Abstractions Package

[`Shoko.Plugin.OfflineImporter.Abstractions`](https://www.nuget.org/packages/Shoko.Plugin.OfflineImporter.Abstractions)
holds the contracts other plugins use to run the importer's file name matching, auto-match rules and release group
lookup. It is released together with the plugin, under the same version. See
[the abstractions docs](docs/abstractions.md) for how to use it.

## Building from Source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
```

The plugin is built to `source/Shoko.Plugin.OfflineImporter/bin/Release/net10.0/`, with
`Shoko.Plugin.OfflineImporter.Abstractions.dll` next to it; install both. The repository holds:

- `source/Shoko.Plugin.OfflineImporter/` — the plugin.
- `source/Shoko.Plugin.OfflineImporter.Abstractions/` — the contracts other plugins use, published to NuGet.
- `tests/` — the unit tests.

The plugin and the abstractions share one version, set in `Directory.Build.props`, and are released together.

## License

This project is licensed under the MIT License.

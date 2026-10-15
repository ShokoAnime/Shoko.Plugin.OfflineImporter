# Shoko Offline Importer Plugin

A [Shoko Server](https://github.com/ShokoAnime/ShokoServer) plugin responsible for importing releases based on file names.

Report problems in the [issue tracker](https://github.com/ShokoAnime/Shoko.Plugin.OfflineImporter/issues)
or on [Discord](https://discord.gg/shokoanime).

## Features

- **Filename-based Importing** — Imports releases by parsing file names when online metadata sources are unavailable.
- **Custom Parsing Rules** — Configure regex-based rules to match your specific file naming conventions.
- **Automatic Matching Rules** — Set up auto-match rules to automatically approve or deny specific releases.

## Installation

### GUI (Recommended)

The plugin is listed in the Shoko Core Repository, which ships with Shoko Server, so there is no repository to add.

1. Open the Shoko Web UI and go to **Settings → Plugin Management → Browse**.
2. Find **Offline Importer** and click **Install** on the desired version.
3. Restart Shoko.

### Manual

1. Download the latest `OfflineImporter-<tag>-any.zip` from the [Releases](https://github.com/ShokoAnime/Shoko.Plugin.OfflineImporter/releases) page.
2. Extract the ZIP and place `Shoko.Plugin.OfflineImporter.dll` into your Shoko **Plugins** folder.
3. Restart Shoko.

## Configuration

The plugin exposes the following settings in the Shoko UI:

| Setting | Default | Description |
|---|---|---|
| **Match Mode** | `Lax Mode` | How strict the importer should be when matching releases. |
| **Store existing hashes** | `true` | Include hashes stored in the database in the provided release info. |
| **Map release group to AniDB** | `false` | Attempt to map the release group to a known AniDB release group. |
| **Enable remote refresh** | `false` | Allow refreshing AniDB data remotely if not found locally. |
| **Enable season searching** | `true` | Use the season number from the file name during search. |
| **Maximum search results to process** | `1` | The maximum number of search results to process per file. |

## Building from Source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet restore
dotnet build --configuration Release
```

The compiled assembly will be located at `source/bin/Release/net10.0/Shoko.Plugin.OfflineImporter.dll`.

## License

This project is licensed under the MIT License.

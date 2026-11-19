# Offline Importer API

The HTTP routes of the [Offline Importer](../README.md). Plugins use
[the abstractions package](abstractions.md) instead.

The plugin reserves the `offline` namespace and serves its routes to administrators only.

## Match

Matches paths without importing or saving anything:

`POST /api/plugin/offline/v1/Match`

```json
{ "path": "/anime/Show [tmdbid-1234]/Season 02/Show - S02E03.mkv" }
```

It takes `path`, `paths` (a list), or both, and optionally `managedFolderID` when the paths are relative to that
managed folder. Without one, an absolute path is matched within the managed folder holding it, and a relative
path within the managed folder of the known file it names, or as given. It answers with a list of parsed results,
one per path, in order, as [the abstractions](abstractions.md) describe
them.

## Release groups

The group list is an unofficial copy of AniDB's release groups, kept up to date by volunteers. The importer refreshes
it daily, but it can miss new groups or lag behind AniDB.

These work whatever **Map release group to AniDB** is set to. The lists come a page at a time, as the server's
own lists do: `page` (from 1) and `pageSize` (20 by default, `0` for everything), answered as
`{ "Total": …, "List": [ … ] }`.

- `GET /api/plugin/offline/v1/ReleaseGroup` — the AniDB groups in the offline group list, by name.
  `search=…` searches the names and short names fuzzily, with the server's fuzzy search, best match first;
  `applyExclusions=true` leaves the excluded groups out.
- `GET /api/plugin/offline/v1/ReleaseGroup/ByName?name=…` — the groups whose name or short name is the given one,
  compared exactly: the preferred group first, the excluded ones left out (`applyPreferencesAndExclusions=false`
  lists every group with the name). Not paged.
- `GET /api/plugin/offline/v1/ReleaseGroup/{anidbGroupID}` — one group by its AniDB ID.
- `GET /api/plugin/offline/v1/ReleaseGroup/Shared` — the names several groups share, with their groups.

## Settings

The importer's settings, its parsing and auto-match rules and release group settings among them, are read and
changed through the server's configuration routes under `/api/v3/Configuration`, as any plugin's are. A save
answers with a `400` naming each problem the settings' own validation finds.

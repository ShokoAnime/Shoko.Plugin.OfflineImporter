# Shoko Offline Importer Abstractions

The contracts another [Shoko Server](https://github.com/ShokoAnime/ShokoServer) plugin uses to run the
[Offline Importer](../README.md)'s file name matching: a path in,
the parsed show, season, episodes, release details and the IDs tagged in the file and folder names out. It
carries none of the plugin itself.

The package is released together with the plugin, under the same version, from 2.0.0 on.

The types are split by namespace, under `Shoko.Plugin.OfflineImporter.Abstractions`: the services in `Services`,
what they return and take in `Models`, and the importer's settings with every shape they store in `Configuration`.

## What it does

`IOfflineMatchingService.Match` runs the importer's parsing rules and the cleanup after them, and stops where the
importer would start looking for a release:

- **No side effects.** It reads no files, asks no database or remote service, and never imports anything.
- **The user's rules.** It uses the parsing rules configured in the importer at the time of the call, so changes
  apply at once. Don't copy them.
- **Paths.** Give it a path relative to a managed folder, with the folder, or a path on its own.

The result is the importer's own `ParsedFileResult`: the series name, year and type, the season, the episode
range and type, the episode name, the version, the release group and source, and the rule that matched. When no
rule matched, `Success` is `false` and only `IDs` is filled in.

```csharp
public class MyService(IOfflineMatchingService matcher)
{
    public string? FindTmdbID(IVideoFile file)
    {
        var result = matcher.Match(file.ManagedFolder, file.RelativePath);

        // The series folder, two levels up, before the file name.
        return result.IDs.FirstOrDefault(id => id.Source == MetadataSource.TMDB && id.Depth is 2)?.ID
            ?? result.IDs.FirstOrDefault(id => id.Source == MetadataSource.TMDB && id.Depth is 0)?.ID;
    }
}
```

## Parsing rules

The custom parsing rules the matching runs are the settings' `ParseRules`, as `CustomParseRule`s (in
`Configuration`); read and change them through the settings, below. The rules the plugin ships with are those of
a new `OfflineImporterConfiguration`.

A rule may force IDs of any source on the files it matches, in `ForcedIDs`: each entry a `ForcedMetadataIDs` with a
`Source` (`anidb`, `tmdb` or an alias) and its `IDs` written as a file name's tag without the brackets, such as
`a123,e456` or `tv789`. The importer prefers forced AniDB IDs over those tagged in the path.

## Auto-match rules

`IOfflineAutoMatchService` decides by auto-match rules whether a release may be matched to a video automatically,
as the importer does with its own rules. The rules are `AutoMatchRule`s (in `Shoko.Plugin.OfflineImporter.Abstractions.Configuration`), so another plugin can keep rules of its
own in its settings and ask the same engine to decide.

- `Evaluate(rules, videoFiles, release)` — takes an `IReleaseInfo` or a `ReleaseInfo` and answers with an
  `AutoMatchDecision(IsAllowed, Rule)`: the first rule whose location, release group and episode range checks all
  pass decides, allowing or denying; when none passes, the release is not allowed and `Rule` is `null`.
- Episode ranges are checked against each cross-reference's episode, read through the server's metadata: its
  AniDB episode when it names one, else its first episode of another source. A source that numbers its episodes
  by season, such as TMDB, is checked against the number within the episode's season, since a range carries no
  season.
- The importer's own rules are the settings' `AutoMatchRules`.

## Release groups

`IOfflineReleaseGroupService` looks up AniDB release groups in the importer's offline group list, applying the
importer's excluded groups and preferred group per name, which are the settings' `ExcludedReleaseGroups` and
`PreferredReleaseGroups`. Its lookups work whatever the importer's **Map release group to
AniDB** setting is, which only decides what the importer does with them.

- `GetReleaseGroupByName(name)` — the group the importer would use: the preferred group for the name, else the
  first group with it that is not excluded, or `null`.
- `GetManyReleaseGroupsByName(name)` — every group whose name or short name is `name`, compared exactly as the
  importer does: the preferred group first, then the rest by name and by when AniDB added them, the excluded ones
  left out.
- Both take `applyPreferencesAndExclusions: false` to ignore the settings.
- `GetAllReleaseGroups()` — every group in the list, by name, then by when AniDB added them;
  `applyExclusions: true` leaves the excluded ones out.
- `GetManyReleaseGroupsBySearch(query)` — the groups whose name or short name matches `query`, fuzzily, with the
  server's fuzzy search (`IFuzzySearchService`), ranked as the server ranks its own searches: containing the text
  before only close to it, then where it starts, how close, and how near in length, then the list's order;
  `applyExclusions: true` leaves the excluded ones out.
- `GetReleaseGroupByID(anidbGroupID)` — one group by its ID.
- `GetAllSharedReleaseGroupNames()` — the names several groups share, with their groups, for choosing between them.

The services are implemented by the importer and taken from the service container. They are not meant to be
implemented by other plugins, so members added to them later are minor changes.

## ID tags

`ParsedFileResult.IDs` lists the IDs tagged in the file name and in the folders above it, up to the managed
folder, as `TaggedMetadataID`s with a `Source`, `ID`, `Type`, `Depth`, `PercentageStart`, `PercentageEnd` and
`IsForced`. The IDs the matching parsing rule forces come first, marked `IsForced`, with a `Depth` of `0`. A tag is
the importer's AniDB tag with any source in place of `anidb`:

- **Brackets.** `[…]`, `(…)` or `{…}`.
- **Source.** Any source registered with the server, its value or an alias, as `MetadataSource.TryGet` reads it:
  `anidb`, `tmdb`, `themoviedb` and the sources plugins add. It may carry an `id` suffix, as in
  `[tmdbid-123]` or `[anidb id=123]`. A tag naming a source that is not registered, or one local to the server, is
  left out.
- **Values.** After a `-`, `=` or space, a comma-separated list.

The `Depth` is `0` for the file name, `1` for the folder holding the file, `2` for the one above it, and so on.

A value's prefix says what it names, for every source and ignoring case:

| Prefix | Gives |
|---|---|
| `a` (anime), `s` (series or show), `tv` (TV series) | A `Series`. |
| `m` | A `Movie`. |
| `e` | An `Episode`. |

In a file name, each value is one of:

| Value | Gives |
|---|---|
| `a123`, `s123`, `tv123` | A `Series` 123. |
| `m123` | A `Movie` 123. |
| `e456` | An `Episode` 456. |
| `123-456` (or `=`, `.` or a space between; with any series prefix: `s123-e456`, `tv123-456`, `a123e456`) | A `Series` 123 and an `Episode` 456. |
| `456` | An `Episode` 456 for AniDB, an ID of no known `Type` for the other sources. |

A movie stands alone: `m123-456` is not a value.

An episode or a movie value may end with the part of it the file holds: `'` or `@` (either one), then a size or a
range, each with an optional `%`. The part goes on the episode or movie, never on the series, and each value's part
stands on its own.

- A range is a start and an end: `456@25-75%`, `456'25-75` and `456@0-50` all work.
- A start alone with a trailing `-` runs to the end: `456'50-` and `456@50-%` give 50–100.
- A single number is the size from the start: `456'50` gives 0–50, and `[anidb-5@50,6'50%]` gives both 5 and 6 at
  0–50.
- A part going past 100 is the whole episode or movie.

In a folder name, each value is a number, optionally with one of the prefixes (`[tmdbid-tv123]`, `{tmdb=m456}`,
`[anidb-a17000]`), and takes neither a part nor a second ID. A bare number is an AniDB anime (`Series`), or an ID
of no known `Type` for the other sources, since a folder's `[tmdbid-123]` may be a show or a movie.

A tag counts only if every value in it reads.

## The settings

The importer's settings are the package's `OfflineImporterConfiguration` (in `Configuration`), tied to the plugin,
so another plugin may read and change them directly through the server's configuration service instead of the
services above:

```csharp
public class MyService(ConfigurationProvider<OfflineImporterConfiguration> settings)
{
    public void ExcludeGroup(MetadataGuid group)
    {
        var configuration = settings.Load(copy: true);
        configuration.ExcludedReleaseGroups = [.. configuration.ExcludedReleaseGroups, group];
        settings.Save(configuration);
    }
}
```

Every save is checked, whoever makes it: against the settings' schema, then by the settings' own validation, which
refuses a parsing rule without a name or with one another rule has, a regular expression that does not compile,
forced IDs of a source that is not registered, an auto-match rule without a name, with episode ranges that do not
read, a range ending before it starts, a location without a managed folder or a release group rule naming no
group, an excluded entry that is not a release group, and a preferred group that is also excluded. The problems
are keyed by path, such as `ParseRules[2].Regex`. The importer picks a save up at once: the matching recompiles
the parsing rules when the settings are saved, and everything else reads them on each use.

## Using it from another plugin

Reference the package and declare a plugin dependency on the Offline Importer, then take `IOfflineMatchingService`
from the service container:

```xml
<ItemGroup>
  <PluginDependency Include="72a6ff39-2bff-534c-9216-d03cd38e7346" Version="^2.0.0" />
</ItemGroup>
```

- **Required dependency.** Your plugin does not load without the importer, which brings these contracts with it.
  Don't ship them:

  ```xml
  <PackageReference Include="Shoko.Plugin.OfflineImporter.Abstractions" Version="2.0.0" ExcludeAssets="runtime" />
  ```

- **Optional dependency** (`Optional="true"` on the `PluginDependency`). Ship the contracts DLL: a plain
  reference, without `ExcludeAssets="runtime"`. With the importer loaded, the server hands your plugin the
  importer's copy, so both use the same types; without it, your copy loads and `IOfflineMatchingService` is simply not
  in the container:

  ```xml
  <PackageReference Include="Shoko.Plugin.OfflineImporter.Abstractions" Version="2.0.0" />
  ```

  ```csharp
  var matcher = serviceProvider.GetService<IOfflineMatchingService>();
  if (matcher is null)
      return; // The importer is not installed or not enabled.
  ```

## Over HTTP

The plugin serves the same results to administrators at `POST /api/plugin/offline/v1/Match`, taking
`{ "path": "…" }` or `{ "paths": ["…"] }`, optionally with a `managedFolderID`, and answering with a list of
results in the order of the paths.
It also serves the release group lookups under `/api/plugin/offline/v1/ReleaseGroup`; the settings are read and
changed through the server's own configuration routes. The [API reference](api.md) has the details.

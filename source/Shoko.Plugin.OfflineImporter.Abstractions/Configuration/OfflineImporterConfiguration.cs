using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Abstractions.Services;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// Configure how the offline importer works.
/// </summary>
/// <remarks>
/// Stored as <c>offline-importer.json</c>, the name the server gave it when
/// it was a nested <c>OfflineImporter.Configuration</c>.
/// </remarks>
[Section(DisplaySectionType.Minimal)]
[StorageLocation(FileName = "offline-importer")]
public class OfflineImporterConfiguration : IReleaseInfoProviderConfiguration, INewtonsoftJsonConfiguration, IPluginConfiguration, IConfigurationWithMigrations, IConfigurationWithCustomValidation<OfflineImporterConfiguration>
{
    /// <summary>
    /// Checks what the schema cannot: the parsing rules' names and regular
    /// expressions and the sources they force IDs of, the auto-match rules'
    /// episode ranges, locations and release groups, and the release group
    /// settings. Every save is checked, whoever makes it.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <returns>The problems, keyed by path, such as <c>ParseRules[2].Regex</c>.</returns>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(OfflineImporterConfiguration config, IConfigurationService configurationService, IPluginManager pluginManager)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string key, string message)
        {
            if (!errors.TryGetValue(key, out var list))
                errors[key] = list = [];
            list.Add(message);
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < config.ParseRules.Count; index++)
        {
            var key = $"{nameof(ParseRules)}[{index}]";
            if (config.ParseRules[index] is not { } rule)
            {
                Add(key, "The rule is missing.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(rule.Name))
                Add($"{key}.{nameof(CustomParseRule.Name)}", "The rule has no name.");
            else if (!names.Add(rule.Name))
                Add($"{key}.{nameof(CustomParseRule.Name)}", $"Another rule is already named \"{rule.Name}\".");

            if (!Enum.IsDefined(rule.Type))
                Add($"{key}.{nameof(CustomParseRule.Type)}", $"\"{rule.Type}\" is not a rule type.");

            for (var forcedIndex = 0; forcedIndex < (rule.ForcedIDs?.Count ?? 0); forcedIndex++)
            {
                var forcedKey = $"{key}.{nameof(CustomParseRule.ForcedIDs)}[{forcedIndex}]";
                if (rule.ForcedIDs![forcedIndex] is not { } forced)
                    Add(forcedKey, "The forced IDs are missing.");
                else if (string.IsNullOrWhiteSpace(forced.Source) || !MetadataSource.TryGet(forced.Source.Trim(), out var source) || source.IsLocal)
                    Add($"{forcedKey}.{nameof(ForcedMetadataIDs.Source)}", $"\"{forced.Source}\" is not a registered source.");
            }

            if (string.IsNullOrEmpty(rule.Regex))
            {
                Add($"{key}.{nameof(CustomParseRule.Regex)}", "The rule has no regular expression.");
                continue;
            }

            try
            {
                _ = new Regex(rule.Regex, rule.IsCaseSensitive ? RegexOptions.ECMAScript : RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
            }
            catch (ArgumentException ex)
            {
                Add($"{key}.{nameof(CustomParseRule.Regex)}", ex.Message);
            }
        }

        for (var index = 0; index < config.AutoMatchRules.Count; index++)
        {
            var key = $"{nameof(AutoMatchRules)}[{index}]";
            if (config.AutoMatchRules[index] is not { } rule)
            {
                Add(key, "The rule is missing.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(rule.Name))
                Add($"{key}.{nameof(AutoMatchRule.Name)}", "The rule has no name.");
            if (!Enum.IsDefined(rule.MatchType))
                Add($"{key}.{nameof(AutoMatchRule.MatchType)}", $"\"{rule.MatchType}\" is not a match type.");
            if (!string.IsNullOrWhiteSpace(rule.EpisodeRanges) && AutoMatchEpisodeRangeRule.Parse(rule.EpisodeRanges).Count is 0)
                Add($"{key}.{nameof(AutoMatchRule.EpisodeRanges)}", $"\"{rule.EpisodeRanges}\" has no episode range.");

            var episodeRangeRules = rule.EpisodeRangeRules ?? [];
            for (var rangeIndex = 0; rangeIndex < episodeRangeRules.Count; rangeIndex++)
            {
                if (episodeRangeRules[rangeIndex] is { RuleType: AutoMatchEpisodeRangeRuleType.Range } range && range.End < range.Start)
                    Add($"{key}.{nameof(AutoMatchRule.EpisodeRangeRules)}[{rangeIndex}].{nameof(AutoMatchEpisodeRangeRule.End)}", "The range ends before it starts.");
            }

            for (var locationIndex = 0; locationIndex < rule.LocationRules.Count; locationIndex++)
            {
                if (rule.LocationRules[locationIndex] is not { ManagedFolderID: > 0 })
                    Add($"{key}.{nameof(AutoMatchRule.LocationRules)}[{locationIndex}].{nameof(AutoMatchLocationRule.ManagedFolderID)}", "The location has no managed folder.");
            }

            for (var groupIndex = 0; groupIndex < rule.GroupRules.Count; groupIndex++)
            {
                var groupKey = $"{key}.{nameof(AutoMatchRule.GroupRules)}[{groupIndex}]";
                switch (rule.GroupRules[groupIndex])
                {
                    case null:
                        Add(groupKey, "The group rule is missing.");
                        break;
                    case { RuleType: AutoMatchReleaseGroupRuleType.AniDB, AnidbID: not > 0 }:
                        Add($"{groupKey}.{nameof(AutoMatchGroupRule.AnidbID)}", "The group rule has no AniDB group ID.");
                        break;
                    case { RuleType: AutoMatchReleaseGroupRuleType.Custom } groupRule when string.IsNullOrWhiteSpace(groupRule.GroupID) && string.IsNullOrWhiteSpace(groupRule.GroupName) && string.IsNullOrWhiteSpace(groupRule.GroupSource):
                        Add(groupKey, "The group rule names no group ID, name or source.");
                        break;
                }
            }
        }

        for (var index = 0; index < config.ExcludedReleaseGroups.Length; index++)
        {
            if (config.ExcludedReleaseGroups[index] is not { } guid)
                Add($"{nameof(ExcludedReleaseGroups)}[{index}]", "The group is missing.");
            else if (guid.EntityType.Value != ReleaseGroupGuid.Kind)
                Add($"{nameof(ExcludedReleaseGroups)}[{index}]", $"\"{guid}\" is not a release group.");
        }

        foreach (var (name, groupID) in config.PreferredReleaseGroups)
        {
            var key = $"{nameof(PreferredReleaseGroups)}[{name}]";
            if (string.IsNullOrWhiteSpace(name))
                Add(key, "The name is empty.");
            else if (MetadataGuid.TryParse($"{MetadataSource.AniDB.Value}://{ReleaseGroupGuid.Kind}/{groupID}", out var guid) && Array.IndexOf(config.ExcludedReleaseGroups, guid) >= 0)
                Add(key, $"AniDB release group {groupID} is also excluded.");
        }

        return errors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);
    }

    /// <summary>
    /// Rewrites a stored configuration from before 2.0.0: a parsing rule's
    /// <c>ForcedAnidbAnimeId</c> and <c>ForcedAnidbEpisodeId</c> become one
    /// <c>anidb</c> entry in its <c>ForcedIDs</c>, as <c>a123,e456</c>.
    /// </summary>
    /// <param name="config">The stored configuration.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <returns>The configuration, rewritten if anything needed it.</returns>
    public static string ApplyMigrations(string config, IApplicationPaths applicationPaths)
    {
        JObject root;
        try
        {
            root = JObject.Parse(config);
        }
        catch (JsonReaderException)
        {
            return config;
        }

        if (root["ParseRules"] is not JArray rules)
            return config;

        var changed = false;
        foreach (var rule in rules.OfType<JObject>())
        {
            var anime = rule["ForcedAnidbAnimeId"];
            var episode = rule["ForcedAnidbEpisodeId"];
            if (anime is null && episode is null)
                continue;

            changed = true;
            rule.Remove("ForcedAnidbAnimeId");
            rule.Remove("ForcedAnidbEpisodeId");
            var values = new List<string>();
            if (anime is { Type: JTokenType.Integer } && anime.Value<int>() > 0)
                values.Add($"a{anime.Value<int>()}");
            if (episode is { Type: JTokenType.Integer } && episode.Value<int>() > 0)
                values.Add($"e{episode.Value<int>()}");
            if (rule["ForcedIDs"] is not JArray forced)
                rule["ForcedIDs"] = forced = [];
            if (values.Count > 0)
                forced.Add(new JObject { ["Source"] = "anidb", ["IDs"] = string.Join(',', values) });
        }

        return changed ? root.ToString(Formatting.Indented) : config;
    }

    /// <summary>
    /// Match mode to use during the import.
    /// </summary>
    [DefaultValue(MatchMode.Lax)]
    public MatchMode Mode { get; set; } = MatchMode.Lax;

    /// <summary>
    /// If set to true, hashes stored in the database will be included in
    /// the provided release info.
    /// </summary>
    [Display(Name = "Store existing hashes")]
    [DefaultValue(true)]
    public bool StoreHashes { get; set; } = true;

    /// <summary>
    /// If enabled, the importer will attempt to map the release group to a
    /// known AniDB release group if possible.
    /// </summary>
    [Display(Name = "Map release group to AniDB")]
    [DefaultValue(true)]
    public bool MapAsAnidbGroupIfPossible { get; set; } = true;

    /// <summary>
    /// Release groups never put on a release. When the name in a file name
    /// belongs to more than one AniDB group, an excluded group is passed
    /// over for the next one.
    /// </summary>
    [Display(Name = "Excluded release groups")]
    public MetadataGuid[] ExcludedReleaseGroups { get; set; } = [];

    /// <summary>
    /// The AniDB release group to use for a name that more than one group
    /// has, by name, ahead of the others.
    /// </summary>
    [Display(Name = "Preferred release groups")]
    public Dictionary<string, int> PreferredReleaseGroups { get; set; } = [];

    /// <summary>
    /// The source a release group's releases get, in place of the one
    /// guessed from the file name.
    /// </summary>
    [Display(Name = "Release group sources")]
    public Dictionary<MetadataGuid, ReleaseSource> ReleaseGroupSources { get; set; } = [];

    /// <summary>
    /// If disabled, the importer will not attempt to refresh AniDB data
    /// remotely if not found locally in the database or in the cache.
    /// </summary>
    [Display(Name = "Enable remote refresh")]
    [DefaultValue(true)]
    public bool AllowRemote { get; set; } = true;

    /// <summary>
    /// If disabled, the importer will not attempt to use the season number
    /// to search for the release.
    /// </summary>
    [Display(Name = "Enable season searching")]
    [DefaultValue(true)]
    public bool AllowSeasonSearching { get; set; } = true;

    /// <summary>
    /// The maximum number of search results to process.
    /// </summary>
    [Display(Name = "Maximum search results to process")]
    [DefaultValue(1)]
    [Range(1, 100)]
    public int MaxSearchResultsToProcess { get; set; } = 1;

    /// <summary>
    /// If enabled, the importer will skip the availability check and
    /// check all video file paths regardless of availability.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    public bool SkipAvailabilityCheck { get; set; }

    /// <summary>
    /// The rules to use for automatic matching. If empty, then automatic
    /// matching is disabled.
    /// </summary>
    [List(ListType = DisplayListType.ComplexInline)]
    public List<AutoMatchRule> AutoMatchRules { get; set; } = [];

    /// <summary>
    /// The regex patterns to use for matching.
    /// </summary>
    [Display(Name = "Parsing Rules")]
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [List(ListType = DisplayListType.ComplexInline, UniqueItems = true)]
    public List<CustomParseRule> ParseRules { get; set; } = [
        new()
        {
            Name = "anti-timestamp",
            Regex = @"^\d{4}[._:\- ]\d{2}[._:\- ]\d{2}[._:\- T]\d{2}[._:\- ]\d{2}[._:\- ]\d{2}(?:[._:\- ]\d{1,6})?(?:Z|[+-]\d{2}:?\d{2})?\.(?<extension>[a-zA-Z0-9_\-+]+)$",
            Type = CustomRuleType.Deny,
        },
        new()
        {
            Name = "trash-anime",
            Regex = @"^(?<showName>.+?(?: \((?<year>\d{4})\))) - (?:(?<isSpecial>S00?)|S\d+)E\d+(?:-E?\d+)? - (?<episode>\d+(?:-\d+)?) - (?<episodeName>.+?(?=\[)).*?(?:-(?<releaseGroup>[^\[\] ]+))?\s*\.(?<extension>[a-zA-Z0-9_\-+]+)$",
        },
        new()
        {
            Name = "pipes",
            Regex = @"[\|｜] (?<pre>.*) Season (?<season>\d+) Episode (?<episode>\d+)(?<post>.+)?\.(?<extension>[a-z0-9]{1,10})$",
            Type = CustomRuleType.PrePost,
        },
        new()
        {
            Name = "default",
            Regex = @"^(?<pre>.*?(?<!(?:PV|CM|Preview|Trailer|Commercial|Menu|Jump Festa Special)[\._ ]*(-[\._ ]*)?))[\._ ]*(?:-[\._ ]*)?(?<=\b|_)(?:(?:s(?<season>\d+(?!\d+)))?(?:(?:OVA|OAD)[\._ ]*|Vol(?:\.|ume)[\._ ]*|(?<=\b|_)(?<isOther>o)|e(?:p(?:isode|s)?)?[\._ ]*|(?=(?:\d+(?:(?!-?\d+[pi])-\d+?|\.5)?)(?:v\d{1,2})?(?:[\._ ]*END(?=\b|_))?[\._ ]*\.[a-z0-9]{1,10}$)|(?<=[\._ ]*-[\._ ]*)(?=(?:\d+(?:(?!-\d+[pi])-\d+?|\.5)?)(?:[\._ ]*END(?=\b|_))?[\._ ]*(?:[\._ ]*-[\._ ]*|[\[\({「]))|(?<=[\._ ]*-[\._ ]*)(?=(?:(?!(?:19|2[01])\d{2})\d+(?:(?!-\d+[pi])-\d+?|\.5)?)(?![\._ ]*-[\._ ]*))|(?=(?:(?!\d+[\. _]\(\d{4}\))\d+(?:(?!-\d+[pi])-\d+?|\.5)?)(?:v\d{1,2})?(?:[\._ ]*END(?=\b|_))?[\._ ]*[\[\({「])|(?<=s\d+) )(?<episode>\d+(?:(?!-\d+[pi])-\d+?|\.5)?)(?!(?:\.\d)?(?:\]|\))|-(?!\d+[pi])\d+|(?<!(?:e(?:pisode|ps?)?)?\k<episode>)[\._ ]*(?:OVA|OAD)| (?:nc)?(?:ed|op))|(?<!jump[\. _]festa[\. _])s(?:p(?:ecials?)?)?[ \.]?(?<specialNumber>\d+(?!(?:\.\d)?(?:\]|\))| \d+|[\. _]?-[\. _]?(?:E(?:p(?:isode)?)?[\. _]?)?\d+))(?![\. _-]*(?:nc)?(?:ed|op))|(?<=\b|_)(?<![a-z0-9])(?:s(?<season>\d+(?!\d+))(?:[\. _]*-)?[\. _]*)?(?:(?<isCreditless>nc|[Cc]reditless|NC)[\s_.]*)?(?<isThemeSong>ED|OP|Opening|Ending)(?![a-z]{2,})(?:[\._ ]*(?<episode>\d+(?!\d*p)))?(?<themeSuffix>(?<=(?:OP|ED)(?:[\._ ]*\d+)?)(?:\.\d+|[a-z]))?(?=\b|_))(?:v(?<version>\d{1,2}))?(?=\b|_)(?:[\._ ]*END)?[\._ ]*(?:-[\._ ]*)?(?<post>.+)?\.(?<extension>[a-z0-9]{1,10})$",
            Type = CustomRuleType.PrePost,
        },
        new()
        {
            Name = "brackets-1",
            Regex = @"^\[(?<releaseGroup>[^\]\n]+)\](?:\[[^\]\n]+\]){0,2}\[(?<showName>[^\]\n]+)\]\[(?<year>\d{4})\]\[(?<episode>\d+)\](?:\[[^\]\n]+\]){0,3}\.(?<extension>[a-zA-Z0-9_\-+]+)$",
        },
        new()
        {
            Name = "brackets-2",
            Regex = @"^\[(?<releaseGroup>[^\]\n]+)\](?:\[[^\]\n]+\]){0,2}\[(?<showName>[^\]\n]+)\]\[(?<episode>\d+)\](?:\[[^\]\n]+\]){0,3}\.(?<extension>[a-zA-Z0-9_\-+]+)$",
        },
        new()
        {
            Name = "reversed-1",
            Regex = @"^\[?(?<episode>\d+)\s*-\s*(?<showName>[^[]+)\]\s*(?:\[[^\]]*\])*\.(?<extension>[a-zA-Z0-9_\-+]+)$",
        },
        new()
        {
            Name = "kamba-special",
            Regex = @"^(?<pre>.+)? (?<episode>\d+) (?<post>.+)?\.(?<extension>[a-z0-9]{1,10})$",
            Type = CustomRuleType.PrePost,
        },
        new()
        {
            Name = "fallback",
            Regex = @"^(?<fallback>.+)\.(?<extension>[a-zA-Z0-9_\-+]+)$",
            Type = CustomRuleType.Fallback,
        },
    ];

    /// <summary>
    ///   Live edit action handler.
    /// </summary>
    /// <param name="context">The context for the action.</param>
    /// <returns>The result of the action.</returns>
    [ConfigurationAction(ConfigurationActionType.LiveEdit)]
    public ConfigurationActionResult LiveEditActionHandler(ConfigurationActionContext<OfflineImporterConfiguration> context)
    {
        // Finalize episode ranges once we're done.
        foreach (var rule in AutoMatchRules.ToList())
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                AutoMatchRules.Remove(rule);
                continue;
            }

            if (rule.EpisodeRangeRules is not null)
            {
                rule.EpisodeRanges = rule.EpisodeRangeRules
                    .Select(rule => rule.ToString())
                    .Join(' ');
                rule.EpisodeRangeRules = null;
            }
        }
        return new(context.Configuration);
    }

    /// <summary>
    /// Lists the AniDB release groups sharing a name with another group,
    /// which a name can be mapped to wrongly, and the excluded ones.
    /// </summary>
    /// <param name="releaseGroupService">The offline release group service.</param>
    /// <returns>The release groups, labelled.</returns>
    [OptionsProvider(nameof(ExcludedReleaseGroups))]
    public IReadOnlyList<SelectOption<MetadataGuid>> ListExcludableReleaseGroups(IOfflineReleaseGroupService releaseGroupService)
        => ToReleaseGroupOptions(releaseGroupService.GetAllSharedReleaseGroupNames().SelectMany(shared => shared.Groups), ExcludedReleaseGroups, releaseGroupService);

    /// <summary>
    /// Lists the names that more than one AniDB release group has, and the
    /// ones already given a preferred group.
    /// </summary>
    /// <param name="releaseGroupService">The offline release group service.</param>
    /// <returns>The names, each labelled with its groups.</returns>
    [OptionsProvider(nameof(PreferredReleaseGroups), Target = OptionsTarget.Keys)]
    public IReadOnlyList<SelectOption<string>> ListSharedReleaseGroupNames(IOfflineReleaseGroupService releaseGroupService)
        => releaseGroupService.GetAllSharedReleaseGroupNames()
            .Select(shared => new SelectOption<string>(shared.Name, $"{shared.Name} ({string.Join(", ", shared.Groups.Select(group => $"AniDB {group.ID}"))})"))
            .Concat(PreferredReleaseGroups.Keys.Select(name => new SelectOption<string>(name, name)))
            .DistinctBy(option => option.Value)
            .OrderBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Lists the release groups of the stored releases, and the ones
    /// already given a source.
    /// </summary>
    /// <param name="releaseService">The video release service.</param>
    /// <param name="releaseGroupService">The offline release group service.</param>
    /// <returns>The release groups, labelled.</returns>
    [OptionsProvider(nameof(ReleaseGroupSources), Target = OptionsTarget.Keys)]
    public IReadOnlyList<SelectOption<MetadataGuid>> ListKnownReleaseGroups(IVideoReleaseService releaseService, IOfflineReleaseGroupService releaseGroupService)
        => ToReleaseGroupOptions(releaseService.GetReleaseGroups(), ReleaseGroupSources.Keys, releaseGroupService);

    private static IReadOnlyList<SelectOption<MetadataGuid>> ToReleaseGroupOptions(
        IEnumerable<IReleaseGroup> groups,
        IEnumerable<MetadataGuid> chosen,
        IOfflineReleaseGroupService releaseGroupService
    )
    {
        var labels = new Dictionary<MetadataGuid, string>();
        foreach (var group in groups)
        {
            if (ReleaseGroupGuid.For(group) is { } guid)
                labels.TryAdd(guid, ReleaseGroupGuid.GetLabel(group));
        }

        // The chosen groups are listed too, so they are shown by name.
        foreach (var guid in chosen)
        {
            if (labels.ContainsKey(guid))
                continue;

            labels[guid] = guid.Source == MetadataSource.AniDB &&
                guid.TryGetNumericID<int>(out var groupID) &&
                releaseGroupService.GetReleaseGroupByID(groupID) is { } group
                    ? ReleaseGroupGuid.GetLabel(group)
                    : guid.ToString();
        }

        return labels
            .OrderBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new SelectOption<MetadataGuid>(pair.Key, pair.Value))
            .ToList();
    }
}

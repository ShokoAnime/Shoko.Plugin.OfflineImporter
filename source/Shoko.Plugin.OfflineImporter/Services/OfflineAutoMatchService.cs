using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Release;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Abstractions.Services;
using Shoko.Plugin.OfflineImporter.Configuration;

namespace Shoko.Plugin.OfflineImporter.Services;

/// <summary>
/// Decides by auto-match rules, for the importer itself and for anyone
/// resolving <see cref="IOfflineAutoMatchService"/>.
/// </summary>
/// <param name="logger">The logger.</param>
/// <param name="metadataService">The metadata service, to read the episodes of the cross-references.</param>
public sealed class OfflineAutoMatchService(
    ILogger<OfflineAutoMatchService> logger,
    IMetadataService metadataService
) : IOfflineAutoMatchService
{
    /// <inheritdoc/>
    public AutoMatchDecision Evaluate(IReadOnlyList<AutoMatchRule> rules, IReadOnlyList<IVideoFile> videoFiles, IReleaseInfo release)
    {
        ArgumentNullException.ThrowIfNull(release);
        return Evaluate(rules, videoFiles, release.Group, release.CrossReferences);
    }

    /// <inheritdoc/>
    public AutoMatchDecision Evaluate(IReadOnlyList<AutoMatchRule> rules, IReadOnlyList<IVideoFile> videoFiles, ReleaseInfo release)
    {
        ArgumentNullException.ThrowIfNull(release);
        return Evaluate(rules, videoFiles, release.Group, release.CrossReferences);
    }

    private AutoMatchDecision Evaluate(IReadOnlyList<AutoMatchRule> rules, IReadOnlyList<IVideoFile> videoFiles, IReleaseGroup? releaseGroup, IReadOnlyList<IReleaseVideoCrossReference> crossReferences)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(videoFiles);
        foreach (var rule in rules)
        {
            logger.LogTrace("Checking rule {Rule}", rule);
            if (rule.LocationRules.Count > 0)
            {
                logger.LogDebug("Checking {Count} location rules for {Rule}", rule.LocationRules.Count, rule.Name);

                var ruleMatched = false;
                foreach (var locationRule in rule.LocationRules)
                {
                    foreach (var l2 in videoFiles)
                    {
                        if (l2.ManagedFolderID != locationRule.ManagedFolderID)
                            continue;
                        if (string.IsNullOrEmpty(locationRule.RelativePath))
                        {
                            ruleMatched = true;
                            break;
                        }
                        var rulePrefix = PlatformUtility.NormalizePath(locationRule.RelativePath, stripLeadingSlash: true);
                        if (rulePrefix[0] != '/')
                            rulePrefix = $"/{rulePrefix}";
                        if (rulePrefix[^1] != '/')
                            rulePrefix = $"{rulePrefix}/";
                        if (!l2.RelativePath.StartsWith(rulePrefix, PlatformUtility.StringComparison))
                            continue;
                        ruleMatched = true;
                        break;
                    }
                    if (ruleMatched)
                    {
                        if (locationRule.MatchType is AutoMatchRuleMatchType.Deny)
                            ruleMatched = !ruleMatched;
                        break;
                    }
                }
                if (!ruleMatched)
                {
                    logger.LogDebug("Location rules did not match for {Rule}", rule.Name);
                    continue;
                }
            }
            if (rule.GroupRules.Count > 0)
            {
                if (releaseGroup is null)
                {
                    logger.LogDebug("Release info has no release group.");
                    continue;
                }

                logger.LogDebug("Checking {Count} release group rules for {Rule}", rule.GroupRules.Count, rule.Name);

                var ruleMatched = false;
                foreach (var groupRule in rule.GroupRules)
                {
                    if (groupRule.RuleType is AutoMatchReleaseGroupRuleType.AniDB)
                    {
                        if (groupRule.AnidbID is not > 0)
                            continue;
                        if (releaseGroup is not { Source: "AniDB", ID.Length: > 0 } || !int.TryParse(releaseGroup.ID, out var releaseGroupId))
                            continue;
                        if (releaseGroupId != groupRule.AnidbID)
                            continue;
                        ruleMatched = groupRule.MatchType is AutoMatchRuleMatchType.Allow;
                        break;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(groupRule.GroupSource) &&
                            !string.Equals(groupRule.GroupSource, releaseGroup.Source, groupRule.IsGroupSourceCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrEmpty(groupRule.GroupID) &&
                            !string.Equals(groupRule.GroupID, releaseGroup.ID, groupRule.IsGroupIDCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrEmpty(groupRule.GroupName) &&
                            !string.Equals(groupRule.GroupName, releaseGroup.Name, groupRule.IsGroupNameCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(groupRule.GroupName, releaseGroup.ShortName, groupRule.IsGroupNameCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                            continue;
                        ruleMatched = groupRule.MatchType is AutoMatchRuleMatchType.Allow;
                        break;
                    }
                }
                if (!ruleMatched)
                {
                    logger.LogDebug("Release group rules did not match for {Rule}", rule.Name);
                    continue;
                }
            }
            if (!string.IsNullOrWhiteSpace(rule.EpisodeRanges))
            {
                logger.LogDebug("Checking episode ranges for {Rule}", rule.Name);

                if (!WithinEpisodeRanges(crossReferences, rule.EpisodeRanges))
                {
                    logger.LogDebug("Cross-references are not within any of the defined episode ranges of {Rule}", rule.Name);
                    continue;
                }
            }
            return new() { IsAllowed = rule.MatchType is AutoMatchRuleMatchType.Allow, Rule = rule };
        }

        return new() { IsAllowed = false, Rule = null };
    }

    private bool WithinEpisodeRanges(IReadOnlyList<IReleaseVideoCrossReference> crossReferences, string episodeRanges)
    {
        var ranges = AutoMatchEpisodeRangeRule.Parse(episodeRanges);
        foreach (var xref in crossReferences)
        {
            var isValid = false;
            if (GetEpisode(xref) is not { } episode)
                return false;
            foreach (var range in ranges)
            {
                switch (range.EpisodeType)
                {
                    case AutoMatchEpisodeRangeType.Episode:
                    case AutoMatchEpisodeRangeType.Special:
                    case AutoMatchEpisodeRangeType.AnyCredits:
                    case AutoMatchEpisodeRangeType.Trailer:
                    case AutoMatchEpisodeRangeType.Parody:
                    case AutoMatchEpisodeRangeType.Other:
                        var episodeType = range.EpisodeType is AutoMatchEpisodeRangeType.AnyCredits
                            ? EpisodeType.Credits
                            : Enum.Parse<EpisodeType>(range.EpisodeType.ToString(), true);
                        if (episode.Type != episodeType)
                            break;
                        if (range.RuleType is AutoMatchEpisodeRangeRuleType.All || (episode.EpisodeNumber >= range.Start && episode.EpisodeNumber <= range.End))
                        {
                            isValid = true;
                            break;
                        }
                        break;
                    case AutoMatchEpisodeRangeType.OpeningCredits:
                    case AutoMatchEpisodeRangeType.EndingCredits:
                        if (episode.Type is not EpisodeType.Credits)
                            break;
                        var type = range.EpisodeType is AutoMatchEpisodeRangeType.OpeningCredits ? "OP" : "ED";
                        var parsedInfo = OfflineImporter.ParseCreditType(episode.DefaultTitle.Value);
                        if (parsedInfo.type != type)
                            break;
                        if (range.RuleType is AutoMatchEpisodeRangeRuleType.All || (parsedInfo.number >= range.Start && parsedInfo.number <= range.End))
                        {
                            isValid = true;
                            break;
                        }
                        break;
                }
                if (isValid)
                {
                    if (range.MatchType is AutoMatchRuleMatchType.Deny)
                        isValid = !isValid;
                    break;
                }
            }
            if (!isValid)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Reads a cross-reference's episode: its AniDB episode when it names
    /// one, else its first episode of another source.
    /// </summary>
    private IEpisode? GetEpisode(IReleaseVideoCrossReference xref)
    {
        if (xref.AnidbEpisodeID is > 0 and var anidbEpisodeID)
            return metadataService.GetEntry<IAnidbEpisode>(new(MetadataSource.AniDB, MetadataEntityType.Episode, anidbEpisodeID.ToString(CultureInfo.InvariantCulture)));

        return xref.IDs.FirstOrDefault(id => id.EntityType == MetadataEntityType.Episode && id.Source != MetadataSource.AniDB) is { } episodeID
            ? metadataService.GetEntry<IEpisode>(episodeID)
            : null;
    }
}

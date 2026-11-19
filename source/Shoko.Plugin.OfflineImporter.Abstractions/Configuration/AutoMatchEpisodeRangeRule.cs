using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// A episode range auto-match rule.
/// </summary>
public partial class AutoMatchEpisodeRangeRule
{
    /// <inheritdoc/>
    [Visibility(DisplayVisibility.Hidden), Key]
    [DefaultValue("{ \"Key\": \"Allow\", \"Value\": \"Match: *\" }")]
    public KeyValuePair<string, string> Key
    {
        get => new(
            $"{MatchType}",
            $"Match: {ToString(addPrefix: false)}"
        );
        // no setter
    }

    /// <summary>
    /// Determines if the match should be allowed or denied.
    /// </summary>
    [DefaultValue(AutoMatchRuleMatchType.Allow)]
    public AutoMatchRuleMatchType MatchType { get; set; } = AutoMatchRuleMatchType.Allow;

    /// <summary>
    /// The rule type.
    /// </summary>
    [DefaultValue(AutoMatchEpisodeRangeRuleType.All)]
    public AutoMatchEpisodeRangeRuleType RuleType { get; set; } = AutoMatchEpisodeRangeRuleType.All;

    /// <summary>
    /// The episode type to match.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [DefaultValue(AutoMatchEpisodeRangeType.Episode)]
    public AutoMatchEpisodeRangeType EpisodeType { get; set; } = AutoMatchEpisodeRangeType.Episode;

    /// <summary>
    /// The start of the range.
    /// </summary>
    [Visibility(
        ToggleWhenMemberIsSet = nameof(RuleType),
        ToggleWhenSetTo = AutoMatchEpisodeRangeRuleType.All,
        ToggleVisibilityTo = DisplayVisibility.Hidden
    )]
    [Range(0, int.MaxValue)]
    [DefaultValue(1)]
    public int Start { get; set; } = 1;

    /// <summary>
    /// The end of the range.
    /// </summary>
    [Visibility(
        ToggleWhenMemberIsSet = nameof(RuleType),
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenSetTo = AutoMatchEpisodeRangeRuleType.Range,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    [Range(0, int.MaxValue)]
    [DefaultValue(1)]
    public int End { get; set; } = 1;

    /// <inheritdoc/>
    public string ToString(bool addPrefix = true)
    {
        var type = EpisodeType switch
        {
            AutoMatchEpisodeRangeType.Special => "S",
            AutoMatchEpisodeRangeType.AnyCredits => "C",
            AutoMatchEpisodeRangeType.OpeningCredits => "OP",
            AutoMatchEpisodeRangeType.EndingCredits => "ED",
            AutoMatchEpisodeRangeType.Trailer => "T",
            AutoMatchEpisodeRangeType.Parody => "P",
            AutoMatchEpisodeRangeType.Other => "O",
            AutoMatchEpisodeRangeType.Episode or _ => string.Empty,
        };
        var prefix = MatchType is AutoMatchRuleMatchType.Deny && addPrefix
            ? "!"
            : string.Empty;
        var range = RuleType is AutoMatchEpisodeRangeRuleType.Single
            ? Start.ToString()
            : RuleType is AutoMatchEpisodeRangeRuleType.Range
                ? $"{Start}-{End}"
                : string.IsNullOrEmpty(type) ? "*" : string.Empty;
        return $"{prefix}{type}{range}";
    }

    /// <summary>
    /// Reads episode ranges written as text, as
    /// <see cref="AutoMatchRule.EpisodeRanges"/> holds them.
    /// </summary>
    /// <param name="episodeRanges">The ranges, separated by commas or spaces.</param>
    /// <returns>The ranges that read, in order.</returns>
    public static List<AutoMatchEpisodeRangeRule> Parse(string? episodeRanges)
    {
        if (string.IsNullOrEmpty(episodeRanges) || EpisodeRangeRegex().Matches(episodeRanges) is not { Count: > 0 } result)
            return [];

        var ranges = new List<AutoMatchEpisodeRangeRule>();
        foreach (var range in result)
        {
            var match = EpisodeRangeRegex().Match(range!.ToString()!);
            AutoMatchEpisodeRangeType? type = match.Groups["type"].Value switch
            {
                "E" or "" => AutoMatchEpisodeRangeType.Episode,
                "S" or "SP" => AutoMatchEpisodeRangeType.Special,
                "C" => AutoMatchEpisodeRangeType.AnyCredits,
                "T" or "PV" => AutoMatchEpisodeRangeType.Trailer,
                "P" => AutoMatchEpisodeRangeType.Parody,
                "O" => AutoMatchEpisodeRangeType.Other,
                "OP" => AutoMatchEpisodeRangeType.OpeningCredits,
                "ED" => AutoMatchEpisodeRangeType.EndingCredits,
                _ => null,
            };
            if (!type.HasValue)
                continue;

            var inverseMatch = match.Groups["inverseMatch"].Success;
            var start = match.Groups["rangeStart"].Success ? (int?)int.Parse(match.Groups["rangeStart"].Value) : null;
            var end = start is null ? null : match.Groups["rangeEnd"].Success ? int.Parse(match.Groups["rangeEnd"].Value) : start;
            var ruleType = !start.HasValue
                ? AutoMatchEpisodeRangeRuleType.All
                : !match.Groups["rangeEnd"].Success
                ? AutoMatchEpisodeRangeRuleType.Single
                : AutoMatchEpisodeRangeRuleType.Range;
            if (start.HasValue && end.HasValue && start > end)
                (start, end) = (end, start);
            ranges.Add(new()
            {
                MatchType = inverseMatch
                    ? AutoMatchRuleMatchType.Deny
                    : AutoMatchRuleMatchType.Allow,
                RuleType = ruleType,
                EpisodeType = type.Value,
                Start = start ?? 1,
                End = end ?? 1,
            });
        }
        return ranges;
    }

    [GeneratedRegex(@"(?<![-*A-Z0-9])(?:(?<inverseMatch>!)?)(?:\b|(?=\*|E|SP?|C|OP|ED|P|T|PV|O))(?:(?<type>(E|SP?|C|OP|ED|P|T|PV|O))|(?<type>(E|SP?|C|OP|ED|P|T|PV|O))?(?:(?<isStar>\*)|(?:(?<rangeStart>[1-9][0-9]*)(?:-\k<type>?(?<rangeEnd>[1-9][0-9]*)?)?))(?!-?(?:\*|E|SP?|C|OP|ED|P|T|PV|O|[0-9])))(?!-?(?:\*|E|SP?|C|OP|ED|P|T|PV|O|[0-9]))[,\.]?(?:(?<=\*|E|SP?|C|OP|ED|P|T|PV|O)|\b)", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeRangeRegex();
}

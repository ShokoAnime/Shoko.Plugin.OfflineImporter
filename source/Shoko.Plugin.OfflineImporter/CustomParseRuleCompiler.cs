using System;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Turns custom parsing rules into the rules the parser runs.
/// </summary>
internal static class CustomParseRuleCompiler
{
    /// <summary>
    /// The options a rule's regular expression is compiled with.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The options.</returns>
    internal static RegexOptions GetRegexOptions(this CustomParseRule rule)
        => rule.IsCaseSensitive
            ? RegexOptions.ECMAScript | RegexOptions.Compiled
            : RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled;

    /// <summary>
    /// Converts the custom rule definition to a match rule.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The match rule.</returns>
    internal static FileNameParser.CompiledRule ToMatchRule(this CustomParseRule rule) => new()
    {
        Name = rule.Name,
        UsePath = rule.UsePath,
        Regex = new(rule.Regex, rule.GetRegexOptions()),
        Transform = (originalDetails, match) => Transform(rule, originalDetails, match),
        ForcedTags = rule.ForcedIDs
            .Select(forced => FileNameIds.ReadForced(forced.Source, forced.IDs))
            .OfType<FileNameIds.Tag>()
            .ToList(),
    };

    private static ParsedFileResult? Transform(CustomParseRule rule, ParsedFileResult originalDetails, Match match)
    {
        Func<ParsedFileResult, Match, ParsedFileResult?> builtInTransform = rule.Type switch
        {
            CustomRuleType.Default => FileNameParser.DefaultTransform,
            CustomRuleType.PrePost => FileNameParser.PrePostTransform,
            CustomRuleType.Fallback => FileNameParser.FallbackTransform,
            CustomRuleType.Deny => FileNameParser.DenyTransform,
            _ => FileNameParser.DefaultTransform,
        };
        var result = builtInTransform(originalDetails, match);
        if (result == null) return null;
        if (rule.ForcedEpisodeType.HasValue) result.EpisodeType = rule.ForcedEpisodeType.Value;
        if (rule.ForcedEpisodeNumber.HasValue) result.EpisodeStart = result.EpisodeEnd = rule.ForcedEpisodeNumber.Value;
        return result;
    }
}

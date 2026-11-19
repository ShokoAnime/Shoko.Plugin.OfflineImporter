using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;
using Shoko.Abstractions.Utilities;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// Auto matching rule.
/// </summary>
public class AutoMatchRule
{
    /// <inheritdoc/>
    [Visibility(DisplayVisibility.Hidden), Key]
    [DefaultValue("{ \"Key\": \"Allow\", \"Value\": \"New Auto Match Allow Rule\" }")]
    public KeyValuePair<string, string> Key
    {
        get => new(
            new string?[]
            {
                MatchType.ToString(),
                (EpisodeRangeRules ?? AutoMatchEpisodeRangeRule.Parse(EpisodeRanges)) is { Count: > 0 } episodeRanges
                    ? $"{episodeRanges.Count} Episode Ranges"
                    : null,
                LocationRules.Count is > 0
                    ? $"{LocationRules.Count} Location Rules"
                    : null,
                GroupRules.Count is > 0
                    ? $"{GroupRules.Count} Release Group Rules"
                    : null
            }
                .WhereNotNullOrDefault()
                .Join(", "),
            !string.IsNullOrEmpty(Name)
                ? Name
                : MatchType is AutoMatchRuleMatchType.Allow
                    ? "New Auto Match Allow Rule"
                    : "New Auto Match Deny Rule"
        );
        // no setter
    }

    /// <summary>
    /// Determines if the match should be allowed or denied.
    /// </summary>
    [DefaultValue(AutoMatchRuleMatchType.Allow)]
    public AutoMatchRuleMatchType MatchType { get; set; } = AutoMatchRuleMatchType.Allow;

    /// <summary>
    /// Human and machine friendly name of the rule.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Full)]
    [Display(Name = "Rule Name")]
    [DefaultValue("")]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The episode ranges to match.
    /// Can be multiple ranges separated by commas or spaces.
    /// For a single digit range, just use the number.
    /// If you want to define a range for a different episode type, then prefix the range with the episode type prefix. Episode Prefixes:
    /// "" or "E" → Episode,
    /// "S" or "SP" → Special,
    /// "C" → All Credits,
    /// "OP" → Opening,
    /// "ED" → Ending,
    /// "T" or "PV" → Trailer,
    /// "P" → Parody,
    /// "O" → Other.
    /// Unprefixed ranges counts as normal episodes.
    /// If you want to match all episodes for a range, use the episode type prefix, optionally followed plus a star, e.g. "*", "E", "E*", etc.
    /// Append "!" to inverse the match, e.g. "!3 *" to allow everything but episode 3.
    /// Leave blank to allow everything.
    /// </summary>
    [Visibility(DisplayVisibility.Hidden)]
    [DefaultValue("")]
    public string EpisodeRanges { get; set; } = string.Empty;

    /// <summary>
    /// Sub-rules for auto-matching only episode ranges matching known shapes.
    /// </summary>
    [List(ListType = DisplayListType.ComplexInline)]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<AutoMatchEpisodeRangeRule>? EpisodeRangeRules { get; set; }

    /// <summary>
    /// Sub-rules for auto-matching only location matching known shapes.
    /// </summary>
    [List(ListType = DisplayListType.ComplexInline)]
    public List<AutoMatchLocationRule> LocationRules { get; set; } = [];

    /// <summary>
    /// Sub-rules for auto-matching only release groups matching known shapes.
    /// </summary>
    [Display(Name = "Release Group Rules")]
    [List(ListType = DisplayListType.ComplexInline)]
    public List<AutoMatchGroupRule> GroupRules { get; set; } = [];

    /// <summary>
    ///   Live edit action handler.
    /// </summary>
    /// <param name="context">The context for the action.</param>
    /// <returns>The result of the action.</returns>
    [ConfigurationAction(ConfigurationActionType.LiveEdit)]
    public ConfigurationActionResult LiveEditActionHandler(ConfigurationActionContext context)
    {
        // Finalize and remove invalid episode range rules.
        EpisodeRangeRules ??= AutoMatchEpisodeRangeRule.Parse(EpisodeRanges);
        // Finalize or remove invalid location rules.
        foreach (var rule in LocationRules.ToList())
        {
            if (rule.ManagedFolderSelector is not null)
            {
                if (rule.ManagedFolderSelector.Options.FirstOrDefault(o => o.IsSelected) is { Value: not 0 } selected)
                {
                    rule.ManagedFolderID = selected.Value;
                    rule.ManagedFolderName = selected.Label;
                    rule.ManagedFolderSelector = null;
                    rule.RelativePath = !string.IsNullOrEmpty(rule.RelativePath)
                        ? '/' + PlatformUtility.NormalizePath(rule.RelativePath, stripLeadingSlash: true)
                        : string.Empty;
                }
                else
                {
                    LocationRules.Remove(rule);
                }
            }
            else if (rule.ManagedFolderID is not > 0)
            {
                LocationRules.Remove(rule);
            }
        }
        // Remove invalid group rules.
        foreach (var rule in GroupRules.ToList())
        {
            if (rule.RuleType is AutoMatchReleaseGroupRuleType.AniDB)
            {
                if (rule.AnidbID < 0)
                    GroupRules.Remove(rule);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(rule.GroupID) && string.IsNullOrWhiteSpace(rule.GroupName) && string.IsNullOrWhiteSpace(rule.GroupSource))
                    GroupRules.Remove(rule);
            }
        }
        return new(context.Configuration);
    }
}

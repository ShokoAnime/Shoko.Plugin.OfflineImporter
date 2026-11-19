using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;
using Shoko.Plugin.OfflineImporter.Abstractions.Services;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// A release group auto-match rule.
/// </summary>
public class AutoMatchGroupRule
{
    /// <inheritdoc/>
    [Visibility(DisplayVisibility.Hidden), Key]
    [DefaultValue("{ \"Key\": \"Allow, Source: Custom\", \"Value\": \"New Release Group Allow Rule\" }")]
    public KeyValuePair<string, string> Key
    {
        get => new(
            $"{MatchType}, " +
            RuleType switch
            {
                AutoMatchReleaseGroupRuleType.Custom => "Source: Custom" + (string.IsNullOrEmpty(GroupSource) ? "" : $" ({GroupSource})"),
                AutoMatchReleaseGroupRuleType.AniDB => "Source: AniDB",
                _ => "Source: -",
            },
            RuleType switch
            {
                AutoMatchReleaseGroupRuleType.Custom => !string.IsNullOrEmpty(GroupName ?? GroupID)
                    ? $"Group: {GroupName ?? GroupID}" + (string.IsNullOrEmpty(GroupID) ? "" : $" ({GroupID})")
                    : MatchType is AutoMatchRuleMatchType.Allow
                        ? "New Release Group Allow Rule"
                        : "New Release Group Deny Rule",
                AutoMatchReleaseGroupRuleType.AniDB => AnidbID is > 0 && !string.IsNullOrEmpty(AnidbGroupName)
                    ? $"Group: {AnidbGroupName} ({AnidbID})"
                    : AnidbID is > 0 ? $"Group: <unknown> ({AnidbID})"
                    : MatchType is AutoMatchRuleMatchType.Allow
                        ? "New Release Group Allow Rule"
                        : "New Release Group Deny Rule",
                _ => MatchType is AutoMatchRuleMatchType.Allow
                    ? "New Release Group Allow Rule"
                    : "New Release Group Deny Rule",
            }
        );
        // no setter
    }

    /// <summary>
    /// Determines if the match should be allowed or denied.
    /// </summary>
    [DefaultValue(AutoMatchRuleMatchType.Allow)]
    public AutoMatchRuleMatchType MatchType { get; set; } = AutoMatchRuleMatchType.Allow;

    /// <summary>
    /// Determines how to match the group.
    /// </summary>
    [DefaultValue(AutoMatchReleaseGroupRuleType.Custom)]
    public AutoMatchReleaseGroupRuleType RuleType { get; set; } = AutoMatchReleaseGroupRuleType.Custom;

    /// <summary>
    /// The ID of the AniDB group to match.
    /// </summary>
    [Display(Name = "AniDB Group ID")]
    [Visibility(
        Size = DisplayElementSize.Large,
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(RuleType),
        ToggleWhenSetTo = AutoMatchReleaseGroupRuleType.AniDB,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    public int AnidbID { get; set; }

    /// <summary>
    /// The name of the AniDB group to match, as the offline group list had it
    /// when the rule was last edited. Only shown.
    /// </summary>
    [Visibility(DisplayVisibility.Hidden)]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? AnidbGroupName { get; set; }

    /// <summary>
    /// The ID of the custom group to match.
    /// </summary>
    [Display(Name = "Custom Group ID")]
    [Visibility(
        Size = DisplayElementSize.Full,
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(RuleType),
        ToggleWhenSetTo = AutoMatchReleaseGroupRuleType.Custom,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    public string? GroupID { get; set; }

    /// <summary>
    /// Determines if the group ID is case sensitive.
    /// </summary>
    [Display(Name = "Custom Group ID Case Sensitive")]
    [Visibility(
        Size = DisplayElementSize.Full,
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(RuleType),
        ToggleWhenSetTo = AutoMatchReleaseGroupRuleType.Custom,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    public bool IsGroupIDCaseSensitive { get; set; }

    /// <summary>
    /// The long or short name of the custom group to match.
    /// </summary>
    [Display(Name = "Custom Group Name")]
    [Visibility(
        Size = DisplayElementSize.Full,
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(RuleType),
        ToggleWhenSetTo = AutoMatchReleaseGroupRuleType.Custom,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    public string? GroupName { get; set; }

    /// <summary>
    /// Determines if the group name is case sensitive.
    /// </summary>
    [Display(Name = "Custom Group Name Case Sensitive")]
    [Visibility(
        Size = DisplayElementSize.Full,
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(RuleType),
        ToggleWhenSetTo = AutoMatchReleaseGroupRuleType.Custom,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    public bool IsGroupNameCaseSensitive { get; set; }

    /// <summary>
    /// The source of the custom group to match.
    /// </summary>
    [Display(Name = "Custom Group Source")]
    [Visibility(
        Size = DisplayElementSize.Full,
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(RuleType),
        ToggleWhenSetTo = AutoMatchReleaseGroupRuleType.Custom,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    public string? GroupSource { get; set; }

    /// <summary>
    /// Determines if the group source is case sensitive.
    /// </summary>
    [Display(Name = "Custom Group Source Case Sensitive")]
    [Visibility(
        Size = DisplayElementSize.Full,
        Visibility = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(RuleType),
        ToggleWhenSetTo = AutoMatchReleaseGroupRuleType.Custom,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    public bool IsGroupSourceCaseSensitive { get; set; }

    /// <summary>
    ///   Live edit action handler.
    /// </summary>
    /// <param name="context">The context for the action.</param>
    /// <param name="releaseGroupService">The release group service, to name the AniDB group.</param>
    /// <returns>The result of the action.</returns>
    [ConfigurationAction(ConfigurationActionType.LiveEdit)]
    public ConfigurationActionResult LiveEditActionHandler(ConfigurationActionContext context, IOfflineReleaseGroupService releaseGroupService)
    {
        AnidbGroupName = RuleType is AutoMatchReleaseGroupRuleType.AniDB && AnidbID > 0
            ? releaseGroupService.GetReleaseGroupByID(AnidbID)?.Name
            : null;
        return new(context.Configuration);
    }
}

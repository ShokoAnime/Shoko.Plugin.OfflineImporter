using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Enums;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// A location auto-match rule.
/// </summary>
public class AutoMatchLocationRule
{
    /// <inheritdoc/>
    [Visibility(DisplayVisibility.Hidden), Key]
    [DefaultValue("{ \"Key\": \"Allow\", \"Value\": \"New Location Allow Rule\" }")]
    public KeyValuePair<string, string> Key
    {
        get => new(
            !string.IsNullOrEmpty(RelativePath)
                ? $"{MatchType}, Relative Path: '{RelativePath}'"
                : $"{MatchType}",
            ManagedFolderID > 0
                ? $"Managed Folder: {ManagedFolderName}"
                : ManagedFolderSelector is not null && ManagedFolderSelector.Options.FirstOrDefault(o => o.IsSelected) is { Value: not 0 } selected ?
                    $"Managed Folder: {selected.Label}"
                : MatchType is AutoMatchRuleMatchType.Allow
                    ? "New Location Allow Rule"
                    : "New Location Deny Rule"
        );
        // no setter
    }

    /// <summary>
    /// The managed folder to match against.
    /// </summary>
    [Visibility(DisplayVisibility.Hidden)]
    public int ManagedFolderID { get; set; }

    /// <summary>
    /// Determines if the match should be allowed or denied.
    /// </summary>
    [DefaultValue(AutoMatchRuleMatchType.Allow)]
    public AutoMatchRuleMatchType MatchType { get; set; } = AutoMatchRuleMatchType.Allow;

    /// <summary>
    /// The managed folder to match against.
    /// </summary>
    [Display(Name = "Managed Folder")]
    [Visibility(
        Size = DisplayElementSize.Full,
        Visibility = DisplayVisibility.ReadOnly,
        ToggleVisibilityTo = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(ManagedFolderID),
        ToggleWhenSetTo = 0
    )]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ManagedFolderName { get; set; }

    /// <summary>
    /// The managed folder to match against.
    /// </summary>
    [Display(Name = "Managed Folder")]
    [Visibility(
        Size = DisplayElementSize.Full,
        Visibility = DisplayVisibility.Visible,
        ToggleVisibilityTo = DisplayVisibility.Hidden,
        ToggleWhenMemberIsSet = nameof(ManagedFolderSelector),
        ToggleOperator = UiConditionOperator.NotEquals,
        ToggleWhenSetTo = null
    )]
    public SelectComponent<int>? ManagedFolderSelector { get; set; }

    /// <summary>
    /// Relative path prefix from the start of the managed folder to match against.
    /// </summary>
    [Visibility(
        Size = DisplayElementSize.Full,
        Visibility = DisplayVisibility.ReadOnly,
        ToggleVisibilityTo = DisplayVisibility.Visible,
        ToggleWhenMemberIsSet = nameof(ManagedFolderID),
        ToggleWhenSetTo = 0
    )]
    [DefaultValue("")]
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    ///   Live edit action handler.
    /// </summary>
    /// <param name="context">The context for the action.</param>
    /// <param name="service">The video service.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The result of the action.</returns>
    [ConfigurationAction(ConfigurationActionType.LiveEdit)]
    public ConfigurationActionResult LiveEditActionHandler(ConfigurationActionContext context, IVideoService service, ILogger logger)
    {
        if (ManagedFolderID is 0 && (ManagedFolderSelector is null || ManagedFolderSelector.Options.Count <= 1))
        {
            try
            {
                ManagedFolderSelector = new SelectComponent<int>(
                    service.GetAllManagedFolders()
                        .Select(folder => new SelectOption<int>(folder.ID, $"{folder.Name} ({folder.ID})"))
                        .Prepend(new SelectOption<int>(0, "-"))
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load managed folders.");
                ManagedFolderSelector = new([
                    new SelectOption<int>(0, "-"),
                ]);
            }
        }
        return new(context.Configuration);
    }
}

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;
using Shoko.Plugin.OfflineImporter.Abstractions.Services;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// A custom parsing rule: a regular expression run on a file name, or on its
/// path, and what to do with what it captures.
/// </summary>
/// <remarks>
/// The rules run in order, and the first one that matches decides. Read and
/// replace them through <see cref="IOfflineMatchingService"/>.
/// </remarks>
public class CustomParseRule
{
    /// <inheritdoc/>
    [Visibility(DisplayVisibility.Hidden), Key]
    [DefaultValue("New Parsing Rule")]
    public string Key
    {
        get => !string.IsNullOrWhiteSpace(Name)
            ? Name
            : "New Parsing Rule";
        // no setter
    }

    /// <summary>
    /// Human and machine friendly name of the rule.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Full)]
    [Display(Name = "Rule Name")]
    [DefaultValue("")]
    [Required]
    public string Name { get; set; } = "";

    /// <summary>
    /// Determines the transform to apply post-matching.
    /// </summary>
    [Display(Name = "Type")]
    [Required]
    [DefaultValue(CustomRuleType.Default)]
    public CustomRuleType Type { get; set; } = CustomRuleType.Default;

    /// <summary>
    /// The episode type to forcefully use.
    /// </summary>
    [Display(Name = "Forced Episode Type")]
    [DefaultValue(null)]
    public EpisodeType? ForcedEpisodeType { get; set; } = null;

    /// <summary>
    /// The episode number to forcefully use.
    /// </summary>
    [Display(Name = "Forced Episode Number")]
    [DefaultValue(null)]
    public int? ForcedEpisodeNumber { get; set; } = null;

    /// <summary>
    /// The IDs to force on the files the rule matches, of any source. They
    /// come first in the result's IDs, marked as forced, and the importer
    /// prefers forced AniDB IDs over the ones tagged in the path.
    /// </summary>
    [Display(Name = "Forced IDs")]
    public List<ForcedMetadataIDs> ForcedIDs { get; set; } = [];

    /// <summary>
    /// If enabled, the regex will be case sensitive.
    /// </summary>
    [Display(Name = "Case Sensitive")]
    [Required]
    [DefaultValue(false)]
    public bool IsCaseSensitive { get; set; } = false;

    /// <summary>
    /// Indicates that the regex should be applied to the file path instead of the file name.
    /// </summary>
    [Display(Name = "Use File Path")]
    [Required]
    [DefaultValue(false)]
    public bool UsePath { get; set; } = false;

    /// <summary>
    /// The regex to match against the file name.
    /// </summary>
    [TextArea]
    [Visibility(Size = DisplayElementSize.Large)]
    [Display(Name = "Regular Expression")]
    [Required]
    [DefaultValue("")]
    [StringSyntax("Regex")]
    public string Regex { get; set; } = string.Empty;
}

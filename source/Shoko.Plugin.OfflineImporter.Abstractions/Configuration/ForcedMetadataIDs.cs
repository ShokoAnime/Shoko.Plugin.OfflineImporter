using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// IDs a custom parsing rule forces on the files it matches, written as an ID
/// tag's source and values without the brackets: <c>anidb</c> and
/// <c>a123,e456</c> for <c>[anidb-a123,e456]</c>.
/// </summary>
/// <remarks>
/// The IDs are listed first in the result's IDs, marked as forced, and read as
/// a file name's tag: the same prefixes, the same values and the same parts.
/// </remarks>
public class ForcedMetadataIDs
{
    /// <summary>
    /// The entry as it is listed, its source and IDs, such as
    /// <c>anidb: a123,e456</c>.
    /// </summary>
    [Visibility(DisplayVisibility.Hidden), Key]
    [DefaultValue("anidb: ")]
    public string Key
    {
        get => $"{Source}: {IDs}";
        // no setter
    }

    /// <summary>
    /// The source, as registered with the server: its value, such as
    /// <c>anidb</c> or <c>tmdb</c>, or an alias, such as <c>themoviedb</c>.
    /// </summary>
    [Display(Name = "Source")]
    [Required]
    [DefaultValue("anidb")]
    [RegularExpression(@"^[A-Za-z0-9]+(-[A-Za-z0-9]+)*$")]
    public string Source { get; set; } = "anidb";

    /// <summary>
    /// The IDs, comma-separated, as a file name's tag writes them:
    /// <c>a123</c>, <c>s123</c> or <c>tv123</c> for a series, <c>m123</c> for a
    /// movie, <c>e456</c> for an episode, <c>123-456</c> for both, each episode
    /// or movie optionally with its part, such as <c>e456'50</c>.
    /// </summary>
    [Display(Name = "IDs")]
    [Required]
    [DefaultValue("")]
    [RegularExpression(@"^\s*(?:(?:[Aa]|[Ss]|[Tt][Vv])[0-9]+|[Mm][0-9]+(?:['@][0-9]+(?:-[0-9]*)?%?)?|(?:(?:[Aa]|[Ss]|[Tt][Vv])?[0-9]+(?:[-=. ]|(?=[Ee])))?[Ee]?[0-9]+(?:['@][0-9]+(?:-[0-9]*)?%?)?)(?:\s*,\s*(?:(?:[Aa]|[Ss]|[Tt][Vv])[0-9]+|[Mm][0-9]+(?:['@][0-9]+(?:-[0-9]*)?%?)?|(?:(?:[Aa]|[Ss]|[Tt][Vv])?[0-9]+(?:[-=. ]|(?=[Ee])))?[Ee]?[0-9]+(?:['@][0-9]+(?:-[0-9]*)?%?)?))*\s*$")]
    public string IDs { get; set; } = "";
}

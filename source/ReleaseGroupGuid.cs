using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Names release groups as metadata identifiers, written as
/// <c>&lt;source&gt;://release-group/&lt;id&gt;</c>.
/// </summary>
public static class ReleaseGroupGuid
{
    /// <summary>
    /// The kind every release group identifier uses.
    /// </summary>
    public const string Kind = "release-group";

    /// <summary>
    /// Gets the identifier of a release group.
    /// </summary>
    /// <param name="group">The release group.</param>
    /// <returns>
    /// The identifier, or <c>null</c> if the group's source or ID can't be
    /// written as one.
    /// </returns>
    public static MetadataGuid? For(IReleaseGroup group)
        => MetadataGuid.TryParse($"{group.Source}://{Kind}/{group.ID}", out var guid) ? guid : null;

    /// <summary>
    /// Gets the label a release group is shown with.
    /// </summary>
    /// <param name="group">The release group.</param>
    /// <returns>
    /// Its name, its short name when different, and where it is from.
    /// </returns>
    public static string GetLabel(IReleaseGroup group)
    {
        var name = string.IsNullOrEmpty(group.ShortName) || group.ShortName == group.Name
            ? group.Name
            : $"{group.Name} [{group.ShortName}]";
        return group.Source is "AniDB"
            ? $"{name} (AniDB {group.ID})"
            : $"{name} ({group.Source})";
    }
}

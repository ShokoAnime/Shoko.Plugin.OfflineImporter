using Shoko.Abstractions.Video.Release;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;

namespace Shoko.Plugin.OfflineImporter.Configuration;

/// <summary>
/// What the importer reads off its configuration's release group settings.
/// </summary>
internal static class OfflineImporterConfigurationExtensions
{
    /// <summary>
    /// Gets the AniDB release group preferred for a name, if any.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="name">The name.</param>
    /// <returns>The group's AniDB ID.</returns>
    internal static int? GetPreferredReleaseGroup(this OfflineImporterConfiguration configuration, string name)
        => configuration.PreferredReleaseGroups.TryGetValue(name, out var groupID) ? groupID : null;

    /// <summary>
    /// Checks whether a release group is excluded.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="group">The release group.</param>
    /// <returns><c>true</c> if the group is in the excluded release groups.</returns>
    internal static bool IsExcluded(this OfflineImporterConfiguration configuration, IReleaseGroup group)
        => configuration.ExcludedReleaseGroups.Length > 0 && ReleaseGroupGuid.For(group) is { } guid && System.Array.IndexOf(configuration.ExcludedReleaseGroups, guid) >= 0;
}

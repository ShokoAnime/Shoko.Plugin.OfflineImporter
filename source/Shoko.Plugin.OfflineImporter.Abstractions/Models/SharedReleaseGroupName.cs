using System.Collections.Generic;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Models;

/// <summary>
/// A name or short name that more than one AniDB release group has.
/// </summary>
public sealed class SharedReleaseGroupName
{
    /// <summary>
    /// The name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The groups with the name, in the list's own order.
    /// </summary>
    public required IReadOnlyList<ReleaseGroup> Groups { get; init; }
}

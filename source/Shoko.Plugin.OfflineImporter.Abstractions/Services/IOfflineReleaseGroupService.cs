using System.Collections.Generic;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Video.Release;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Services;

/// <summary>
/// Looks up AniDB release groups in the Offline Importer's offline group
/// list, and reads and replaces the importer's release group settings.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by the Offline Importer and taken from the service container;
/// other plugins are not meant to implement it, so members added to it later
/// are minor changes.
/// </para>
/// <para>
/// The lookups work whether or not the importer maps the release groups of
/// its own releases to AniDB groups; that setting only decides what the
/// importer does. The group list is downloaded and refreshed as the importer
/// does, the first time a lookup needs it.
/// </para>
/// </remarks>
public interface IOfflineReleaseGroupService
{
    /// <summary>
    /// Gets the AniDB release groups whose name or short name is the given
    /// name, compared exactly, as the importer compares them.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="applyPreferencesAndExclusions">
    /// Whether to put the preferred group for the name first and leave the
    /// excluded groups out, as the importer does. When <c>false</c>, every
    /// group with the name is returned in the list's own order.
    /// </param>
    /// <returns>
    /// The groups: the preferred one first, then the rest in the list's own
    /// order (by name, then by when AniDB added them).
    /// </returns>
    IReadOnlyList<ReleaseGroup> GetManyReleaseGroupsByName(string name, bool applyPreferencesAndExclusions = true);

    /// <summary>
    /// Gets the AniDB release group the importer would put on a release
    /// naming the given group: the first of <see cref="GetManyReleaseGroupsByName"/>.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="applyPreferencesAndExclusions">
    /// Whether to take the preferred group for the name and pass over the
    /// excluded groups, as the importer does.
    /// </param>
    /// <returns>
    /// The preferred group, else the first group with the name that is not
    /// excluded, or <c>null</c> when there is none.
    /// </returns>
    ReleaseGroup? GetReleaseGroupByName(string name, bool applyPreferencesAndExclusions = true);

    /// <summary>
    /// Gets every AniDB release group in the offline group list.
    /// </summary>
    /// <param name="applyExclusions">
    /// Whether to leave the excluded groups out. Off by default, so the whole
    /// list is returned.
    /// </param>
    /// <returns>The groups, by name, then by when AniDB added them.</returns>
    IReadOnlyList<ReleaseGroup> GetAllReleaseGroups(bool applyExclusions = false);

    /// <summary>
    /// Searches the AniDB release groups by name and short name, fuzzily,
    /// with the server's fuzzy search.
    /// </summary>
    /// <param name="query">The text to search for.</param>
    /// <param name="applyExclusions">Whether to leave the excluded groups out.</param>
    /// <returns>
    /// The groups that match, best first, as the server ranks its own
    /// searches: those containing the text before those only close to it,
    /// then by where the text starts, how close, and how near in length, then
    /// in the list's own order.
    /// </returns>
    IReadOnlyList<ReleaseGroup> GetManyReleaseGroupsBySearch(string query, bool applyExclusions = false);

    /// <summary>
    /// Gets an AniDB release group by its ID.
    /// </summary>
    /// <param name="anidbGroupID">The AniDB release group ID.</param>
    /// <returns>The group, or <c>null</c> when the list does not have it.</returns>
    ReleaseGroup? GetReleaseGroupByID(int anidbGroupID);

    /// <summary>
    /// Lists the names or short names that more than one AniDB release group
    /// has, for choosing a preferred group for each.
    /// </summary>
    /// <returns>The names, in order, each with its groups.</returns>
    IReadOnlyList<SharedReleaseGroupName> GetAllSharedReleaseGroupNames();

}

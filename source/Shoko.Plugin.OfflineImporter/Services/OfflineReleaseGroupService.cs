using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Release;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Abstractions.Services;
using Shoko.Plugin.OfflineImporter.Configuration;

namespace Shoko.Plugin.OfflineImporter.Services;

/// <summary>
/// Looks up release groups in the offline group list with the importer's
/// release group settings, for the importer itself and for anyone resolving
/// <see cref="IOfflineReleaseGroupService"/>.
/// </summary>
/// <param name="configurationProvider">The configuration provider.</param>
/// <param name="applicationPaths">The application paths, where the group list is kept.</param>
/// <param name="fuzzySearch">The server's fuzzy search.</param>
public sealed class OfflineReleaseGroupService(
    ConfigurationProvider<OfflineImporterConfiguration> configurationProvider,
    IApplicationPaths applicationPaths,
    IFuzzySearchService fuzzySearch
) : IOfflineReleaseGroupService
{
    /// <inheritdoc/>
    public IReadOnlyList<ReleaseGroup> GetManyReleaseGroupsByName(string name, bool applyPreferencesAndExclusions = true)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!applyPreferencesAndExclusions)
            return OfflineReleaseGroupSearch.FindByName(name, applicationPaths);

        var configuration = configurationProvider.Load();
        return OfflineReleaseGroupSearch.FindByName(name, applicationPaths, configuration.IsExcluded, configuration.GetPreferredReleaseGroup(name));
    }

    /// <inheritdoc/>
    public ReleaseGroup? GetReleaseGroupByName(string name, bool applyPreferencesAndExclusions = true)
        => GetManyReleaseGroupsByName(name, applyPreferencesAndExclusions).FirstOrDefault();

    /// <inheritdoc/>
    public IReadOnlyList<ReleaseGroup> GetAllReleaseGroups(bool applyExclusions = false)
        => OfflineReleaseGroupSearch.GetAll(applicationPaths, applyExclusions ? configurationProvider.Load().IsExcluded : null);

    /// <inheritdoc/>
    public IReadOnlyList<ReleaseGroup> GetManyReleaseGroupsBySearch(string query, bool applyExclusions = false)
    {
        ArgumentNullException.ThrowIfNull(query);
        return GetAllReleaseGroups(applyExclusions)
            .Select((group, index) => (group, index, score: fuzzySearch.FuzzyScoreAnyName(query, new HashSet<string> { group.Name, group.ShortName })))
            .Where(match => match.score is not null)
            // Ranked as the server ranks its own searches: matches containing
            // the text first, then by where it starts, how close, and how
            // near in length; the list's order breaks ties.
            .OrderBy(match => match.score!.Value.isNotExact)
            .ThenBy(match => match.score!.Value.index)
            .ThenBy(match => match.score!.Value.distance)
            .ThenBy(match => match.score!.Value.lengthDiff)
            .ThenBy(match => match.index)
            .Select(match => match.group)
            .ToList();
    }

    /// <inheritdoc/>
    public ReleaseGroup? GetReleaseGroupByID(int anidbGroupID)
        => OfflineReleaseGroupSearch.LookupByID(anidbGroupID, applicationPaths);

    /// <inheritdoc/>
    public IReadOnlyList<SharedReleaseGroupName> GetAllSharedReleaseGroupNames()
        => OfflineReleaseGroupSearch.GetSharedNames(applicationPaths)
            .Select(shared => new SharedReleaseGroupName { Name = shared.Name, Groups = shared.Groups })
            .ToList();

}

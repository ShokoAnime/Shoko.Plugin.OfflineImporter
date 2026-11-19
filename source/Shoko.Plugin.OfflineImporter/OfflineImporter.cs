
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Hashing;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Services;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Imports releases based on file paths.
///
/// By default operates only using the local database, local cache, and
/// local title search. Online refresh can be enabled if desired, but it's
/// designed to function without it.
///
/// You have three different operation modes; lax, strict (with extra info),
/// and strict (without extra info). By default it operates in lax mode,
/// which guesses based on all clues if finds in the file name. Then we have
/// strict mode, where it will only look for anidb bracket tags in the file
/// and folder names to match against, optionally with extra info from the
/// lax mode enabled without using the lax search for the cross-reference
/// info.
/// </summary>
public partial class OfflineImporter : IReleaseInfoProvider<OfflineImporterConfiguration>
{
    private readonly ILogger<OfflineImporter> _logger;

    private readonly IApplicationPaths _applicationPaths;

    private readonly IAnidbService _anidbService;

    private readonly IMetadataService _metadataService;

    private readonly IVideoService _videoService;

    private readonly ConfigurationProvider<OfflineImporterConfiguration> _configurationProvider;

    private readonly OfflineMatchingService _matcher;

    private readonly OfflineReleaseGroupService _releaseGroups;

    private readonly OfflineAutoMatchService _autoMatch;

    /// <inheritdoc/>
    public string Name => "Offline Importer";

    /// <inheritdoc/>
    public string? CannotAutoMatchReason => _configurationProvider.Load().AutoMatchRules.Count > 0
        ? null
        : "No auto-match rules are set up in the Offline Importer's configuration.";

    private const string MatchPrefix = "match://";

    private const string SearchPrefix = "search://";

    private const string IdPrefix = "offline://";

    /// <inheritdoc/>
    public string Description => """
        Imports releases based on file paths.

        Works from the local database, local cache, and local title search,
        and refreshes missing anime from AniDB unless online refresh is turned
        off. Without auto-match rules it only links files by hand.

        You have three different operation modes; lax, strict (with extra info),
        and strict (without extra info). By default it operates in lax mode,
        which guesses based on all clues if finds in the file name. Then we have
        strict mode, where it will only look for anidb bracket tags in the file
        and folder names to match against, optionally with extra info from the
        lax mode enabled without using the lax search for the cross-reference
        info.
    """;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineImporter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="anidbService">The anidb service.</param>
    /// <param name="metadataService">The metadata service.</param>
    /// <param name="videoService">The video service.</param>
    /// <param name="configurationProvider">The configuration provider.</param>
    /// <param name="matcher">The matching service, holding the parsing rules.</param>
    /// <param name="releaseGroups">The release group service.</param>
    /// <param name="autoMatch">The auto-match service, deciding by the auto-match rules.</param>
    public OfflineImporter(
        ILogger<OfflineImporter> logger,
        IApplicationPaths applicationPaths,
        IAnidbService anidbService,
        IMetadataService metadataService,
        IVideoService videoService,
        ConfigurationProvider<OfflineImporterConfiguration> configurationProvider,
        OfflineMatchingService matcher,
        OfflineReleaseGroupService releaseGroups,
        OfflineAutoMatchService autoMatch
    )
    {
        _logger = logger;
        _applicationPaths = applicationPaths;
        _anidbService = anidbService;
        _metadataService = metadataService;
        _videoService = videoService;
        _configurationProvider = configurationProvider;
        _matcher = matcher;
        _releaseGroups = releaseGroups;
        _autoMatch = autoMatch;
    }

    /// <inheritdoc/>
    public async Task<ReleaseInfo?> GetReleaseInfoForVideo(ReleaseInfoContext context, CancellationToken cancellationToken)
    {
        var (video, isAutomatic) = context;
        var config = _configurationProvider.Load();
        if (isAutomatic && config.AutoMatchRules.Count is 0)
        {
            _logger.LogDebug("No rules configured for auto matching.");
            return null;
        }

        _logger.LogDebug("Getting release info for {Video}", video.LocalID);
        var videoFiles = video.Files;
        foreach (var location in videoFiles)
        {
            var isAvailable = location.IsAvailable;
            if (location.Path is not { Length: > 0 } filePath)
            {
                _logger.LogDebug("Location is not available: {Path} (ManagedFolder={ManagedFolderID})", location.RelativePath, location.ManagedFolderID);
                continue;
            }

            if (!_configurationProvider.Load().SkipAvailabilityCheck && !isAvailable)
            {
                _logger.LogDebug("Location is not available: {Path} (ManagedFolder={ManagedFolderID})", location.RelativePath, location.ManagedFolderID);
                continue;
            }

            var releaseInfo = await GetReleaseInfoByFilePath(filePath, location.RelativePath, search: true, local: isAvailable, cancellationToken).ConfigureAwait(false);
            if (releaseInfo is not { CrossReferences.Count: > 0 })
                continue;

            if (isAutomatic)
            {
                _logger.LogInformation("Checking rules for automatic match for {Path}", filePath);
                var decision = _autoMatch.Evaluate(config.AutoMatchRules, videoFiles, releaseInfo);
                var rule = decision.Rule;
                if (!decision.IsAllowed || rule is null)
                {
                    if (rule is not null)
                        _logger.LogDebug("Release info {ReleaseInfo} is was explicitly disallowed by rule {Rule}", releaseInfo, rule.Name);
                    else
                        _logger.LogDebug("Release info {ReleaseInfo} is was not allowed by any of the {Count} rules", releaseInfo, config.AutoMatchRules.Count);
                    return null;
                }

                _logger.LogDebug("Release info {ReleaseInfo} was allowed by rule {Rule}", releaseInfo, rule.Name);
            }

            releaseInfo.FileSize = video.Size;
            releaseInfo.Hashes = video.Hashes
                .Select(x => new HashDigest() { Type = x.Type, Value = x.Value, Metadata = x.Metadata })
                .ToList();

            if (video.MediaInfo is { } mediaInfo)
            {
                if (mediaInfo.Encoded is { } encodedAt && encodedAt > DateTime.MinValue && encodedAt != DateTime.UnixEpoch)
                    releaseInfo.ReleasedAt = DateOnly.FromDateTime(encodedAt);

                releaseInfo.IsChaptered = mediaInfo.Chapters.Count > 0;
            }

            return releaseInfo;
        }

        return null;
    }

    private async Task<ReleaseInfo?> GetReleaseInfoByFilePath(string filePath, string relativePath, bool search, bool local, CancellationToken cancellationToken)
    {
        var config = _configurationProvider.Load();
        var animeId = (int?)null;
        var releaseInfo = (ReleaseInfo?)null;
        var parts = relativePath[1..].Split('/');
        var match = _matcher.Parse(filePath, parts, out var idTags);
        var (taggedAnimeId, episodeList) = GetTaggedAnidbIDs(match, idTags);
        animeId = taggedAnimeId;
        if (episodeList is not null)
            releaseInfo = (await GetReleaseInfoById(IdPrefix + episodeList, cancellationToken))!;
        else if (search && config.Mode is MatchMode.Lax && match is { Success: true })
            releaseInfo = await GetReleaseInfoForMatch(match, animeId, cancellationToken);

        if (
            releaseInfo is not null &&
            releaseInfo.CrossReferences.Any(xref => xref.AnidbAnimeID is null or <= 0) &&
            animeId is > 0 &&
            _anidbService.SearchAnimeByID(animeId.Value) is not null
        )
        {
            foreach (var xref in releaseInfo.CrossReferences)
            {
                if (xref.AnidbAnimeID is null or <= 0)
                    xref.AnidbAnimeID = animeId;
            }
        }

        if (match is { Success: true })
        {
            releaseInfo ??= new();
            var group = (ReleaseGroup?)null;
            if (!string.IsNullOrEmpty(match.ReleaseGroup))
            {
                if (config.MapAsAnidbGroupIfPossible)
                    group = _releaseGroups.GetReleaseGroupByName(match.ReleaseGroup);
                group ??= new() { ID = match.ReleaseGroup, Name = match.ReleaseGroup, ShortName = match.ReleaseGroup, Source = "Offline" };
                if (config.IsExcluded(group))
                    group = null;
            }

            releaseInfo.Group = group;
            if (match.ReleaseSource is not null)
                releaseInfo.Source = match.ReleaseSource.Value;
            if (group is not null && ReleaseGroupGuid.For(group) is { } groupGuid && config.ReleaseGroupSources.TryGetValue(groupGuid, out var groupSource))
                releaseInfo.Source = groupSource;
            releaseInfo.OriginalFilename = Path.GetFileName(match.FilePath);
            releaseInfo.Version = match.ReleaseVersion ?? 1;
            releaseInfo.Metadata = JsonConvert.SerializeObject(match, new JsonSerializerSettings() { Converters = [new StringEnumConverter()] });
            releaseInfo.IsCreditless = match.IsCreditless;
            releaseInfo.IsCensored = match.IsCensored;
            // Assume the creation date has been properly set in the file-system.
            if (local && releaseInfo.ReleasedAt is null)
                releaseInfo.ReleasedAt = DateOnly.FromDateTime(File.GetCreationTimeUtc(match.FilePath));
        }

        return releaseInfo;
    }

    /// <summary>
    /// Reads the AniDB IDs tagged in a path, as the import uses them.
    /// </summary>
    /// <param name="match">The match, with its <see cref="ParsedFileResult.IDs"/>.</param>
    /// <param name="tags">The tags the IDs were read from, with their values as written.</param>
    /// <returns>
    /// The anime, the first AniDB series ID a custom rule forced, else in the
    /// file name, else in the folder holding it; and the episodes a custom
    /// rule forced, else those in the file name's AniDB tags, as a release
    /// ID's list, with their parts as written and a movie as the episode it is
    /// on AniDB, or <c>null</c> when there are none.
    /// </returns>
    internal static (int? AnimeID, string? EpisodeList) GetTaggedAnidbIDs(ParsedFileResult match, IReadOnlyList<FileNameIds.Tag> tags)
    {
        var animeId = match.IDs
            .Where(id => id.Depth is 0 or 1 && id.Source == MetadataSource.AniDB && id.Type == MetadataEntityType.Series)
            .OrderBy(id => id.IsForced ? -1 : id.Depth)
            .Select(id => int.TryParse(id.ID, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : (int?)null)
            .FirstOrDefault(value => value is not null);
        var anidbTags = tags.Where(tag => tag.Depth is 0 && tag.Source == MetadataSource.AniDB).ToList();
        var forced = anidbTags.Where(tag => tag.IsForced).SelectMany(tag => tag.Values).Where(value => !value.IsSeriesOnly).ToList();
        var episodes = (forced.Count > 0 ? forced : anidbTags.Where(tag => !tag.IsForced).SelectMany(tag => tag.Values).Where(value => !value.IsSeriesOnly))
            .Select(value => value.ImporterText)
            .ToList();
        return (animeId, episodes.Count > 0 ? string.Join(',', episodes) : null);
    }

    private async Task<ReleaseInfo?> GetReleaseInfoForMatch(ParsedFileResult match, int? animeId, CancellationToken cancellationToken)
    {
        ReleaseInfo? releaseInfo;
        if (animeId is > 0)
        {
            _logger.LogDebug("Found anime ID in folder name to use: {AnimeID}.", animeId);
            if (_anidbService.SearchAnimeByID(animeId.Value) is not { } searchResult)
            {
                _logger.LogDebug("No search result found for anime ID {AnimeID}.", animeId);
                return null;
            }

            releaseInfo = await GetReleaseInfoForMatchAndAnime(match, searchResult, cancellationToken).ConfigureAwait(false);
            if (releaseInfo is not null)
            {
                _logger.LogDebug("Found anime id match for {ShowName} in search results.", match.SeriesName);
                return releaseInfo;
            }

            _logger.LogDebug("No match found for anime ID {AnimeID}.", animeId);
            return null;
        }

        var searchResults = _anidbService.SearchAnime(match.SeriesName!, fuzzy: true);
        if (searchResults.Count == 0)
        {
            _logger.LogDebug("No search results found for {ShowName}.", match.SeriesName);
            return null;
        }

        var followSeasonNumber = _configurationProvider.Load().AllowSeasonSearching &&
            match is { SeriesYear: null, SeasonNumber: > 1 and < 20, EpisodeType: EpisodeType.Episode, EpisodeStart: > 0, EpisodeEnd: > 0 };
        var limit = _configurationProvider.Load().MaxSearchResultsToProcess;
        _logger.LogDebug("Found {Count} search results for {ShowName}. (Year={Year},Type={AnimeType},Limit={Limit})", searchResults.Count, match.SeriesName, match.SeriesYear, match.SeriesType, limit);
        foreach (var searchResult in searchResults.Take(limit))
        {
            releaseInfo = await GetReleaseInfoForMatchAndAnime(match, searchResult, cancellationToken, year: match.SeriesYear, animeType: match.SeriesType, followSeasonNumber: followSeasonNumber).ConfigureAwait(false);
            if (releaseInfo is not null)
            {
                _logger.LogDebug("Found match for {ShowName} in search results. (Anime={AnimeID})", match.SeriesName, searchResult.AnidbID);
                return releaseInfo;
            }
        }

        _logger.LogDebug("No match found for {ShowName} in search results.", match.SeriesName);
        return null;
    }

    private async Task<ReleaseInfo?> GetReleaseInfoForMatchAndAnime(ParsedFileResult match, IAnidbAnimeSearchResult searchResult, CancellationToken cancellationToken, int depth = 0, int? year = null, AnimeType? animeType = null, bool followSeasonNumber = false, DateTime? previousAirDate = null)
    {
        var anime = searchResult.AnidbAnime;
        if (
            anime is null ||
            (followSeasonNumber && anime.RelatedSeries.Count == 0) ||
            !anime.Episodes.Any(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd && x.EpisodeNumber >= match.EpisodeStart)
        )
        {
            var method = _configurationProvider.Load().AllowRemote
                ? AnidbRefreshMethod.Cache | AnidbRefreshMethod.Remote | AnidbRefreshMethod.SkipSupplementaryUpdate
                : AnidbRefreshMethod.Cache | AnidbRefreshMethod.SkipSupplementaryUpdate;
            _logger.LogDebug("Refreshing AniDB Anime {AnimeName} (Anime={AnimeID},Method={Method})", searchResult.DefaultTitle.Value, searchResult.AnidbID, method.ToString());
            try
            {
                anime = await _anidbService.RefreshAnimeByID(searchResult.AnidbID, method, cancellationToken).ConfigureAwait(false);
            }
            catch (AnidbHttpBannedException ex)
            {
                _logger.LogWarning(ex, "Got banned while refreshing {AnimeName} (Anime={AnimeID})", searchResult.DefaultTitle.Value, searchResult.AnidbID);
                return null;
            }

            if (anime is null)
                return null;
        }

        // Switch season if we're in the wrong season.
        if (followSeasonNumber)
        {
            // We need an air date to do this, and if it doesn't have one then
            // it's most likely haven't aired yet.
            if (anime.AirDate is not { } currentAirDate)
            {
                _logger.LogDebug("Season number is set but anime {AnimeName} does not have an air date. (Anime={AnimeID})", searchResult.DefaultTitle.Value, searchResult.AnidbID);
                return null;
            }

            // When looking for prequels/sequels, check the air date to prevent
            // following pre-sequels in either direction.
            if (previousAirDate.HasValue && (depth is 0 ? currentAirDate > previousAirDate.Value : currentAirDate < previousAirDate.Value))
            {
                _logger.LogDebug("Anime aired after previous air date. (Anime={AnimeID})", searchResult.AnidbID);
                return null;
            }

            // If the entry anime have any prequels, then first go backwards to
            // find the earliest prequel before starting the forward search,
            // ignoring any pre-sequels.
            var relations = anime.RelatedSeries;
            if (depth is 0 && relations.Any(x => x is { RelationType: RelationType.Prequel }))
            {
                _logger.LogDebug("Attempting prequel(s) for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.AnidbID);
                var prequels = GetRelatedAnimeIDs(anime, RelationType.Prequel);
                var shouldContinue = true;
                foreach (var prequelID in prequels)
                {
                    var prequelSearch = _anidbService.SearchAnimeByID(prequelID);
                    if (prequelSearch is null)
                    {
                        _logger.LogDebug("Unknown prequel for {AnimeName}. (Anime={AnimeID},PrequelAnime={PrequelAnimeID})", anime.DefaultTitle.Value, anime.AnidbID, prequelID);
                        continue;
                    }

                    _logger.LogDebug("Attempting prequel {PrequelAnimeName} for {AnimeName}. (Anime={AnimeID},PrequelAnime={PrequelAnimeID})", prequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.AnidbID, prequelSearch.AnidbID);
                    var finalResult = await GetReleaseInfoForMatchAndAnime(match, prequelSearch, cancellationToken, depth, year, animeType, followSeasonNumber, currentAirDate.ToDateTime()).ConfigureAwait(false);
                    if (finalResult is not null)
                    {
                        _logger.LogDebug("Found prequel {PrequelAnimeName} for {AnimeName}. (Anime={AnimeID},PrequelAnime={PrequelAnimeID})", prequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.AnidbID, prequelSearch.AnidbID);
                        return finalResult;
                    }

                    if (shouldContinue && prequelSearch.AnidbAnime is { } prequelAnime && prequelAnime.AirDate is { } prequelAirDate && prequelAirDate < currentAirDate)
                        shouldContinue = false;
                }

                if (!shouldContinue)
                {
                    _logger.LogDebug("No prequel found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.AnidbID);
                    return null;
                }
            }

            // Do the year/type check on the entry anime.
            if (depth is 0)
            {
                if (year.HasValue && (currentAirDate.Year != year.Value))
                {
                    _logger.LogDebug("Year mismatch between {ShowName} and {AnimeName}. (Anime={AnimeID},FoundYear={FoundYear},ExpectedYear={ExpectedYear})", match.SeriesName, anime.DefaultTitle.Value, anime.AnidbID, currentAirDate.Year, year);
                    return null;
                }

                if (animeType is not null && anime.Type != animeType)
                {
                    _logger.LogDebug("Type mismatch between {ShowName} and {AnimeName}. (Anime={AnimeID},FoundType={FoundType},ExpectedType={ExpectedType})", match.SeriesName, anime.DefaultTitle.Value, anime.AnidbID, anime.Type, animeType);
                    return null;
                }
            }

            // If the anime is a movie series or tv special, or if it's an OVA
            // with less then the cut-off number, then ignore it and look for
            // any the next sequel.
            const int OvaCutOff = 8;
            if (anime.Type is AnimeType.Movie or AnimeType.TVSpecial || (anime.Type is AnimeType.OVA && anime.EpisodeCounts[EpisodeType.Episode] <= OvaCutOff))
            {
                var sequels = GetRelatedAnimeIDs(anime, RelationType.Sequel);
                foreach (var sequelID in sequels)
                {
                    var sequelSearch = _anidbService.SearchAnimeByID(sequelID);
                    if (sequelSearch is null)
                    {
                        _logger.LogDebug("Unknown sequel for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", anime.DefaultTitle.Value, anime.AnidbID, sequelID);
                        continue;
                    }

                    _logger.LogDebug("Attempting sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.AnidbID, sequelSearch.AnidbID);
                    var finalResult = await GetReleaseInfoForMatchAndAnime(match, sequelSearch, cancellationToken, depth, year, animeType, followSeasonNumber, previousAirDate).ConfigureAwait(false);
                    if (finalResult is not null)
                    {
                        _logger.LogDebug("Found sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.AnidbID, sequelSearch.AnidbID);
                        return finalResult;
                    }
                }

                _logger.LogDebug("No sequel found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.AnidbID);
                return null;
            }

            // Unless we found the desired season number then continue until we
            // exhaust all sequels.
            if (depth + 1 != match.SeasonNumber)
            {
                var sequels = GetRelatedAnimeIDs(anime, RelationType.Sequel);
                foreach (var sequelID in sequels)
                {
                    var sequelSearch = _anidbService.SearchAnimeByID(sequelID);
                    if (sequelSearch is null)
                    {
                        _logger.LogDebug("Unknown sequel for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", anime.DefaultTitle.Value, anime.AnidbID, sequelID);
                        continue;
                    }

                    _logger.LogDebug("Attempting sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.AnidbID, sequelSearch.AnidbID);
                    var finalResult = await GetReleaseInfoForMatchAndAnime(match, sequelSearch, cancellationToken, depth + 1, year, animeType, followSeasonNumber, currentAirDate.ToDateTime()).ConfigureAwait(false);
                    if (finalResult is not null)
                    {
                        _logger.LogDebug("Found sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.AnidbID, sequelSearch.AnidbID);
                        return finalResult;
                    }
                }

                _logger.LogDebug("No sequel found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.AnidbID);
                return null;
            }
        }

        // Prevents following movies when searching for sequels, since we can't
        // know beforehand if the anidb anime is a movie before we potentially
        // fetch it.
        if (depth is > 0 && anime is { Type: AnimeType.Movie or AnimeType.Unknown })
        {
            _logger.LogDebug("Skipping unknown or movie {AnimeName} (Anime={AnimeID})", anime.DefaultTitle.Value, anime.AnidbID);
            return null;
        }

        var allEpisodes = anime.Episodes.ToList();
        List<IAnidbEpisode> episodes;
        // Extra handling for TV Specials with multiple parts as additional episodes.
        if (match.EpisodeType is EpisodeType.Episode && anime.Type is AnimeType.TVSpecial && allEpisodes.Count > 0 && allEpisodes[0].DefaultTitle.Value is "TV Special" && allEpisodes.Count(ep => ep.Type is EpisodeType.Episode) > 1)
        {
            episodes = allEpisodes
                .Where(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd + 1 && x.EpisodeNumber >= match.EpisodeStart + 1)
                .ToList();
            if (episodes.Count > 0)
            {
                if (year.HasValue && (!anime.AirDate.HasValue || anime.AirDate.Value.Year != year.Value))
                {
                    _logger.LogDebug("Year mismatch between {ShowName} and {AnimeName} (Anime={AnimeID},FoundYear={FoundYear},ExpectedYear={ExpectedYear})", match.SeriesName, anime.DefaultTitle.Value, anime.AnidbID, anime.AirDate?.Year, year);
                    return null;
                }

                if (animeType is not null && anime.Type != animeType)
                {
                    _logger.LogDebug("Type mismatch between {ShowName} and {AnimeName} (Anime={AnimeID},FoundType={FoundType},ExpectedType={ExpectedType})", match.SeriesName, anime.DefaultTitle.Value, anime.AnidbID, anime.Type, animeType);
                    return null;
                }

                var parts = allEpisodes.Count(ep => ep.Type is EpisodeType.Episode) - 1;
                var range = (int)Math.Floor(100d / parts);
                var start = (int)(range * (match.EpisodeStart - 1));
                var rangeSize = match.EpisodeEnd - match.EpisodeStart + 1;
                var end = start + (int)(range * rangeSize);
                if (start + range > 95)
                    end = 100;
                return new ReleaseInfo()
                {
                    ID = IdPrefix + episodes.Select(x => $"{anime.AnidbID}-{x.AnidbID}").Join(','),
                    CrossReferences = [
                        new ReleaseVideoCrossReference() { AnidbAnimeID = anime.AnidbID, AnidbEpisodeID = allEpisodes[0].AnidbID, PercentageStart = start, PercentageEnd = end },
                        .. episodes.Select(x => new ReleaseVideoCrossReference() { AnidbAnimeID = anime.AnidbID, AnidbEpisodeID = x.AnidbID }),
                    ],
                };
            }
        }
        // Extra handling for credits if we managed to parse the credit type
        else if (match.EpisodeType is EpisodeType.Credits && !string.IsNullOrWhiteSpace(match.EpisodeText))
        {
            var (type, num, suf) = ParseCreditType(match.EpisodeText);
            var allCredits = allEpisodes
                .Where(x => x.Type == EpisodeType.Credits)
                .Select(x => (x, y: ParseCreditType(x.DefaultTitle.Value)))
                .Where(t => t.y.type == type)
                .ToList();
            var altMode = allCredits.GroupBy(x => x.y.number).All(g => g.Count() == 1);
            episodes = allCredits
                .Select(t => (
                    t.x,
                    t.y,
                    z: altMode
                        ? new HashSet<string>([.. ParseCreditTitle(t.x.DefaultTitle.Value), .. ParseCreditTitle($"{type}{IntegerToSuffix(t.y.number)}")], StringComparer.InvariantCultureIgnoreCase)
                        : ParseCreditTitle(t.x.DefaultTitle.Value)
                ))
                .Where(t => t.z.Contains(match.EpisodeText))
                .Select(x => x.x)
                .ToList();
        }
        // HAMA-style specials offset handling.
        else if (
            match is { EpisodeStart: > 200, EpisodeEnd: > 200 } &&
            anime.EpisodeCounts is { } episodeCounts && (
                (match.EpisodeType is EpisodeType.Episode && episodeCounts.Episodes is > 0 and <= 190) ||
                (match.EpisodeType is EpisodeType.Special && episodeCounts.Specials is > 0 and <= 190)
            )
        )
        {
            var (episodeType, offset) = true switch
            {
                // The 100-200 range is not supported.
                _ when match.EpisodeStart is >= 201 and < 300 && episodeCounts.Trailers > 0 => (EpisodeType.Trailer, 200), // Trailers
                _ when match.EpisodeStart is >= 301 and < 400 && episodeCounts.Credits > 0 => (EpisodeType.Credits, 300), // OPs/EDs/etc.
                _ when match.EpisodeStart is >= 401 and < 500 && episodeCounts.Others > 0 => (EpisodeType.Other, 400), // Others
                _ => (match.EpisodeType, 0),
            };
            var episodeStart = match.EpisodeStart - offset;
            var episodeEnd = match.EpisodeEnd - offset;
            if (episodeType is EpisodeType.Special && episodeEnd - episodeStart == 0 && !string.IsNullOrEmpty(match.EpisodeName))
                episodes = SearchForSpecialByName(match.EpisodeName, episodeStart, allEpisodes);
            else
                episodes = allEpisodes
                    .Where(x => x.Type == episodeType && x.EpisodeNumber <= episodeEnd && x.EpisodeNumber >= episodeStart)
                    .ToList();
        }
        else if (match.EpisodeType is EpisodeType.Special && match.EpisodeEnd - match.EpisodeStart == 0 && !string.IsNullOrEmpty(match.EpisodeText))
        {
            episodes = SearchForSpecialByEpisodeText(match.EpisodeText, match.EpisodeStart, allEpisodes);
            if (episodes.Count is 0 && match.EpisodeType is EpisodeType.Special && match.EpisodeEnd - match.EpisodeStart == 0 && !string.IsNullOrEmpty(match.EpisodeName))
                episodes = SearchForSpecialByName(match.EpisodeName, match.EpisodeStart, allEpisodes);
        }
        else if (match.EpisodeType is EpisodeType.Special && match.EpisodeEnd - match.EpisodeStart == 0 && !string.IsNullOrEmpty(match.EpisodeName))
        {
            episodes = SearchForSpecialByName(match.EpisodeName, match.EpisodeStart, allEpisodes);
        }
        else
        {
            episodes = allEpisodes
                .Where(x => x.Type == match.EpisodeType && x.EpisodeNumber <= match.EpisodeEnd && x.EpisodeNumber >= match.EpisodeStart)
                .ToList();
        }
        if (episodes.Count == 0)
        {
            var highestEpisodeNumber = allEpisodes.Count > 0 ? allEpisodes.Max(x => x.EpisodeNumber) : 0;
            if (match.EpisodeStart > highestEpisodeNumber)
            {
                _logger.LogDebug(
                    "Episode range ({EpisodeStart}-{EpisodeEnd}) is above last episode number ({LastEpisodeNumber}), trying to find sequel for {AnimeName}. (Anime={AnimeID})",
                    match.EpisodeStart,
                    match.EpisodeEnd,
                    highestEpisodeNumber,
                    anime.DefaultTitle.Value,
                    anime.AnidbID
                );
                var sequels = GetRelatedAnimeIDs(anime, RelationType.Sequel);
                if (sequels.Count == 0)
                {
                    _logger.LogDebug("No sequels found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.AnidbID);
                    return null;
                }

                foreach (var sequelID in sequels)
                {
                    var sequelSearch = _anidbService.SearchAnimeByID(sequelID);
                    if (sequelSearch is null)
                    {
                        _logger.LogDebug("Unknown sequel for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", anime.DefaultTitle.Value, anime.AnidbID, sequelID);
                        continue;
                    }

                    var sequelMatch = new ParsedFileResult()
                    {
                        Success = true,
                        FilePath = match.FilePath,
                        FileExtension = match.FileExtension,
                        EpisodeName = match.EpisodeName,
                        EpisodeEnd = match.EpisodeEnd - highestEpisodeNumber,
                        EpisodeStart = match.EpisodeStart - highestEpisodeNumber,
                        EpisodeType = match.EpisodeType,
                        ReleaseGroup = match.ReleaseGroup,
                        SeasonNumber = match.SeasonNumber,
                        SeriesName = match.SeriesName,
                        ReleaseVersion = match.ReleaseVersion,
                        RuleName = match.RuleName,
                    };
                    _logger.LogDebug("Attempting sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.AnidbID, sequelSearch.AnidbID);
                    var sequelResult = await GetReleaseInfoForMatchAndAnime(sequelMatch, sequelSearch, cancellationToken, depth + 1, year, animeType, followSeasonNumber, previousAirDate).ConfigureAwait(false);
                    if (sequelResult is not null)
                    {
                        _logger.LogDebug("Found sequel {SequelAnimeName} for {AnimeName}. (Anime={AnimeID},SequelAnime={SequelAnimeID})", sequelSearch.DefaultTitle.Value, anime.DefaultTitle.Value, anime.AnidbID, sequelSearch.AnidbID);
                        return sequelResult;
                    }
                }

                _logger.LogDebug("No matched sequels found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.AnidbID);
                return null;
            }

            _logger.LogDebug("No episodes found for {AnimeName}. (Anime={AnimeID})", anime.DefaultTitle.Value, anime.AnidbID);
            return null;
        }

        if (!followSeasonNumber)
        {
            if (year.HasValue && (!anime.AirDate.HasValue || anime.AirDate.Value.Year != year.Value))
            {
                _logger.LogDebug("Year mismatch between {ShowName} and {AnimeName}. (Anime={AnimeID},FoundYear={FoundYear},ExpectedYear={ExpectedYear})", match.SeriesName, anime.DefaultTitle.Value, anime.AnidbID, anime.AirDate?.Year, year);
                return null;
            }

            if (animeType is not null && anime.Type != animeType)
            {
                _logger.LogDebug("Type mismatch between {ShowName} and {AnimeName}. (Anime={AnimeID},FoundType={FoundType},ExpectedType={ExpectedType})", match.SeriesName, anime.DefaultTitle.Value, anime.AnidbID, anime.Type, animeType);
                return null;
            }
        }

        foreach (var episode in episodes)
        {
            _logger.LogDebug("Found episode {EpisodeType} {EpisodeNumber} for {ShowName}. (Anime={AnimeID},Episode={EpisodeID})", episode.Type.ToString(), episode.EpisodeNumber, anime.DefaultTitle.Value, anime.AnidbID, episode.AnidbID);
        }
        var releaseInfo = new ReleaseInfo()
        {
            ID = IdPrefix + episodes.Select(x => $"{anime.AnidbID}-{x.AnidbID}").Join(','),
            CrossReferences = episodes.Select(x => new ReleaseVideoCrossReference() { AnidbAnimeID = anime.AnidbID, AnidbEpisodeID = x.AnidbID }).ToList(),
        };
        return releaseInfo;
    }

    /// <inheritdoc/>
    public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
    {
        // Search/match by file name/path.
        if (releaseId.StartsWith(MatchPrefix) || releaseId.StartsWith(SearchPrefix))
        {
            var search = releaseId.StartsWith(SearchPrefix);
            var filePath = search
                ? releaseId[SearchPrefix.Length..]
                : releaseId[MatchPrefix.Length..];
            var relativePath = Path.GetFileName(filePath);
            var folderName = "/" + Path.GetDirectoryName(filePath);
            if (folderName is { Length: > 0 })
                relativePath = folderName + "/" + relativePath;
            _logger.LogDebug("Getting release info for {FilePath} (RelativePath={RelativePath},Search={Search})", filePath, relativePath, search);

            var local = false;
            if (Path.IsPathFullyQualified(filePath) &&
                _videoService.GetVideoFileByAbsolutePath(filePath) is { Path: { Length: > 0 } videoPath0, IsAvailable: var isAvailable0 })
            {
                local = isAvailable0;
                filePath = videoPath0;
            }
            else if (_videoService.GetVideoFileByRelativePath(filePath) is { Path: { Length: > 0 } videoPath1, IsAvailable: var isAvailable1 })
            {
                local = isAvailable1;
                filePath = videoPath1;
            }

            return GetReleaseInfoByFilePath(filePath, relativePath, search, local, cancellationToken);
        }

        if (!releaseId.StartsWith(IdPrefix))
        {
            _logger.LogWarning("Invalid release id: {ReleaseId}", releaseId);
            return Task.FromResult<ReleaseInfo?>(null);
        }

        releaseId = releaseId[IdPrefix.Length..];
        if (!IdSegments.TryParse(releaseId, out var segments))
        {
            _logger.LogWarning("Malformed release id: {ReleaseId}", releaseId);
            return Task.FromResult<ReleaseInfo?>(null);
        }

        var crossReferences = segments
            .Select(segment => new ReleaseVideoCrossReference
            {
                AnidbAnimeID = segment.SeriesID,
                AnidbEpisodeID = segment.EpisodeID,
                PercentageStart = segment.PercentageStart,
                PercentageEnd = segment.PercentageEnd,
            })
            .ToList();

        return Task.FromResult<ReleaseInfo?>(new() { ID = IdPrefix + releaseId, CrossReferences = crossReferences, });
    }

    private static string IntegerToSuffix(int number) => number switch
    {
        // from a to z
        _ when number is >= 1 and <= 26 => ((char)('a' + number - 1)).ToString(),
        _ => string.Empty,
    };

    private static List<int> GetRelatedAnimeIDs(IAnidbAnime anime, RelationType relationType)
        => anime.RelatedSeries
            .Where(x => x.RelationType == relationType && x.RelatedID.Source == MetadataSource.AniDB)
            .Select(x => x.RelatedID.TryGetNumericID<int>(out var id) ? id : 0)
            .Where(id => id > 0)
            .Order()
            .ToList();

    internal static (string type, int number, string suffix) ParseCreditType(string title)
    {
        var match = CreditTitleRegex().Match(title);
        if (!match.Success)
            return (string.Empty, 0, string.Empty);

        var type = match.Groups["type"].Value.ToLowerInvariant() switch
        {
            "ed" or "ending" or "credits" => "ED",
            "op" or "opening" or "intro" => "OP",
            _ => null,
        };
        if (type is null)
            return (string.Empty, 0, string.Empty);

        var number = match.Groups["number"].Success ? int.Parse(match.Groups["number"].Value) : 1;
        var suffix = match.Groups["suffix"].Value;
        if (match.Groups["version"].Success)
            suffix = match.Groups["version"].Value switch
            {
                "1" => "a",
                "2" => "b",
                "3" => "c",
                _ => suffix,
            };

        string? episodeRange = null;
        foreach (var group in match.Groups.Values.Where(g => g.Name.StartsWith("episodeRange")))
        {
            if (group.Success)
            {
                episodeRange = group.Value;
                break;
            }
        }

        var realSuffix = !string.IsNullOrEmpty(suffix) ? suffix : !string.IsNullOrEmpty(episodeRange) ? $" ({episodeRange})" : string.Empty;
        return (type, number, realSuffix);
    }

    private static HashSet<string> ParseCreditTitle(string title)
    {
        var matches = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var match = CreditTitleRegex().Match(title);
        if (!match.Success)
            return matches;

        var types = match.Groups["type"].Value.ToLowerInvariant() switch
        {
            "ed" or "ending" or "credits" => new string[] { "ED", "ED ", "Ending ", "Credits " },
            "op" or "opening" or "intro" => ["OP", "OP ", "Opening ", "Intro "],
            _ => null,
        };
        if (types is null)
            return matches;

        var isSpecial = match.Groups["isSpecial1"].Success || match.Groups["isSpecial2"].Success;
        var isExtra = match.Groups["isExtra"].Success;
        var isFull = match.Groups["isFull1"].Success || match.Groups["isFull2"].Success;
        var isFinalEpisode = match.Groups["isFinalEpisode"].Success;
        var isDVD = match.Groups["isDVD"].Success;
        var number = match.Groups["number"].Success ? int.Parse(match.Groups["number"].Value).ToString() : "1";
        var suffix = match.Groups["suffix"].Value;
        if (match.Groups["version"].Success)
            suffix = match.Groups["version"].Value switch
            {
                "1" => "a",
                "2" => "b",
                "3" => "c",
                _ => suffix,
            };

        string? episodeRange = null;
        foreach (var group in match.Groups.Values.Where(g => g.Name.StartsWith("episodeRange")))
        {
            if (group.Success)
            {
                episodeRange = group.Value;
                break;
            }
        }

        var realSuffix = !string.IsNullOrEmpty(suffix) ? suffix : !string.IsNullOrEmpty(episodeRange) ? $" ({episodeRange})" : string.Empty;
        foreach (var type in types)
        {
            var modifiedType = type;
            if (realSuffix.Length > 0 && realSuffix[0] == ' ' && type[^1] == ' ')
                modifiedType = type[0..^1];
            var phrases = new List<string>() {
                $"{modifiedType}{number}{realSuffix}",
            };
            if (number is "1")
            {
                phrases.Add($"{modifiedType}{realSuffix}");
            }
            if (isExtra)
            {
                phrases.Add($"{modifiedType.TrimEnd()} EX{number}{realSuffix}");
                phrases.Add($"{modifiedType.TrimEnd()} EX {number}{realSuffix}");
            }
            if (number is "1" && isExtra)
            {
                phrases.Add($"{modifiedType.TrimEnd()} EX{realSuffix}");
                phrases.Add($"{modifiedType.TrimEnd()} EX {realSuffix}");
            }
            foreach (var phrase in phrases)
            {
                matches.Add(phrase.TrimEnd());
                if (isFull)
                {
                    matches.Add(("Full " + phrase).TrimEnd());
                    matches.Add(phrase.TrimEnd() + " (LongVer.)");
                }
                if (isFinalEpisode)
                {
                    if (type is "ED" or "OP")
                    {
                        matches.Add(("FE " + phrase).TrimEnd());
                        matches.Add(("FE" + phrase).TrimEnd());
                    }
                    else
                    {
                        matches.Add(("Final Episode " + phrase).TrimEnd());
                    }
                }
            }
        }
        return matches;
    }

    private static List<IAnidbEpisode> SearchForSpecialByEpisodeText(string episodeText, float episodeNumber, IReadOnlyList<IAnidbEpisode> allEpisodes)
    {
        var exactName = $"Episode {episodeText}";
        var exactNameShort = $"E{episodeText}";
        var startsWith = $"Episode {episodeText} ";
        var startsWithShort = $"E{episodeText} ";
        var specials = allEpisodes.Where(x => x.Type is EpisodeType.Special).ToList();
        var match =
            specials.FirstOrDefault(x =>
                string.Equals(x.DefaultTitle.Value, exactName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.DefaultTitle.Value, exactNameShort, StringComparison.OrdinalIgnoreCase) ||
                x.DefaultTitle.Value.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase) ||
                x.DefaultTitle.Value.StartsWith(startsWithShort, StringComparison.OrdinalIgnoreCase) ||
                x.Titles.Any(y =>
                    string.Equals(y.Value, exactName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(y.Value, exactNameShort, StringComparison.OrdinalIgnoreCase) ||
                    y.Value.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase) ||
                    y.Value.StartsWith(startsWithShort, StringComparison.OrdinalIgnoreCase)
                )
            ) ??
            specials.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);
        return match is not null ? [match] : [];
    }

    private static List<IAnidbEpisode> SearchForSpecialByName(string episodeName, float episodeNumber, IReadOnlyList<IAnidbEpisode> allEpisodes)
    {
        episodeName = episodeName.Replace('`', '\'');

        var specials = allEpisodes.Where(x => x.Type is EpisodeType.Special).ToList();
        var match =
            specials.FirstOrDefault(x =>
                string.Equals(x.DefaultTitle.Value.Replace('`', '\''), episodeName, StringComparison.OrdinalIgnoreCase) ||
                x.Titles.Any(y => string.Equals(y.Value.Replace('`', '\''), episodeName, StringComparison.OrdinalIgnoreCase))
            ) ??
            specials.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);
        return match is not null ? [match] : [];
    }

    [GeneratedRegex(@"^(?:(?<isFinalEpisode>Final Episode) |Episode (?<episodeRange1>\d+(?:\-\d+)?) |(?<isFull1>Full) |(?<isDVD>DVD) |(?<isSpecial1>Special|Bonus|Special Broadcast|TV)? |(?<isCreditless>creditless) |(?<isInternational>international) |(?<isOriginal>Original )?(?<language>japanese|english|american|us|german|french(?: uncensored)?|italian|spanish|arabic) |(?<isEdgeCase>BD-BOX II|Doukyuusei: Natsu no Owari ni|Ami-chan's First Love|Narration -) )?(?<type>ED|OP|Opening|Ending|Intro|Credits)(?<isExtra> EX)?(?: *(?<number>\d+))?(?<suffix> ?[a-z]|\.\d+)?(?:(?: \((?<episodeRange2>\d+(?:\-\d+)?)\)| Episode (?<episodeRange3>\d+(?:\-\d+)?))?| \((?:(?<isSpecial2>Specials?)|Episode (?<episodeRange4>S?\d+(?:\-\d+)?(?: ?& ?S?\d+(?:\-\d+)?)?)|(?<isFull2>LongVer.)|(?<details1>[^\)]+))\)| \[(?<details2>[^\]]+)\]| (?<isFull3>full)| - Version (?<version>\d+)| - .+| .+)?$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreditTitleRegex();
}

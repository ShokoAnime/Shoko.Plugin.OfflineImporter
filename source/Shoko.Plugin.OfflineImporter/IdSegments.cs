using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Reads the episode lists the importer takes in its release IDs:
/// comma-separated
/// <c>[[a]&lt;series&gt;-][e]&lt;episode&gt;[('|@)&lt;percent&gt;[-&lt;percent&gt;][%]]</c>
/// segments, AniDB IDs only. The ID tags' wider grammar, with its other
/// prefixes and its parts read on their own, is <see cref="FileNameIds"/>';
/// this one stays as it is, sizes counted on from the previous part, so
/// stored release IDs keep their meaning.
/// </summary>
internal static partial class IdSegments
{
    /// <summary>
    /// One segment of a list.
    /// </summary>
    /// <param name="SeriesID">The series ID, if the segment gives one.</param>
    /// <param name="EpisodeID">The episode ID.</param>
    /// <param name="HasEpisodeMarker">Whether the episode ID is marked with an <c>e</c>.</param>
    /// <param name="PercentageStart">Where the file's part of the episode starts.</param>
    /// <param name="PercentageEnd">Where the file's part of the episode ends.</param>
    /// <param name="HasPercentage">Whether the segment gave a percentage itself.</param>
    internal sealed record Segment(int? SeriesID, int EpisodeID, bool HasEpisodeMarker, int PercentageStart, int PercentageEnd, bool HasPercentage);

    /// <summary>
    /// Reads a comma-separated list of segments, counting each segment's
    /// part of its episode on from where the previous one ended.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="segments">The segments, if every one of them is valid.</param>
    /// <returns><c>true</c> if every segment is valid.</returns>
    internal static bool TryParse(string list, [NotNullWhen(true)] out IReadOnlyList<Segment>? segments)
    {
        var segmentRegex = SegmentRegex();
        var matches = list
            .Split(',')
            .Select(part => segmentRegex.Match(part))
            .ToList();
        if (matches.Count is 0 || matches.Any(part => part is null || !part.Success))
        {
            segments = null;
            return false;
        }

        var previousPercentage = 0;
        var result = new List<Segment>();
        foreach (var match in matches)
        {
            var seriesId = match.Groups["seriesId"].Success ? int.Parse(match.Groups["seriesId"].Value) : (int?)null;
            var episodeId = int.Parse(match.Groups["episodeId"].Value);
            var hasPercentage = match.Groups["percentRangeStartOrWholeRange"].Success;
            var (percentageStart, percentageEnd) = NextPart(
                hasPercentage ? int.Parse(match.Groups["percentRangeStartOrWholeRange"].Value) : null,
                match.Groups["percentRangeEnd"].Success ? int.Parse(match.Groups["percentRangeEnd"].Value) : null,
                ref previousPercentage
            );
            result.Add(new(seriesId, episodeId, match.Groups["episodeMarker"].Success, percentageStart, percentageEnd, hasPercentage));
        }

        segments = result;
        return true;
    }

    /// <summary>
    /// Works out a segment's part of its episode or movie, counting on from
    /// where the previous segment's part ended.
    /// </summary>
    /// <param name="start">
    /// The first number after <c>'</c> or <c>@</c>: where the part starts
    /// when an end follows, else the size of the part. <c>null</c> for the
    /// whole of it.
    /// </param>
    /// <param name="end">The number after the <c>-</c>, where the part ends.</param>
    /// <param name="previous">Where the previous part ended, <c>0</c> after a whole one.</param>
    /// <returns>Where the part starts and ends, from 0 to 100.</returns>
    private static (int Start, int End) NextPart(int? start, int? end, ref int previous)
    {
        var percentageStart = start ?? 100;
        var percentageEnd = start is not null && end is { } given ? given : -1;
        if (percentageEnd is -1)
        {
            percentageEnd = percentageStart + previous;
            percentageStart = previous;
        }
        if (percentageEnd > 100)
        {
            percentageEnd = 100;
            percentageStart = 0;
        }
        previous = percentageEnd;
        if (previous >= 100)
            previous = 0;
        return (percentageStart, percentageEnd);
    }

    [GeneratedRegex(@"(?<=^|,)\s*(?:a?(?<seriesId>\d+)(?:[=\- \.]|(?=e)))?(?<episodeMarker>e)?(?<episodeId>\d+)(?:['@](?<percentRangeStartOrWholeRange>\d+)(?:\-(?<percentRangeEnd>\d+))?%?)?\s*(?=$|,),?", RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SegmentRegex();
}

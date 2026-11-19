using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Metadata;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Reads the ID tags in file and folder names, for any registered source.
/// </summary>
/// <remarks>
/// <para>
/// This is the importer's AniDB tag grammar with the source word taken from
/// the tag: a tag is <c>[</c>, <c>(</c> or <c>{</c>, a source, optionally
/// followed by <c>id</c> or <c>ids</c> (attached or after a <c>-</c>,
/// <c>.</c> or space), a <c>-</c>, <c>=</c> or space, a comma-separated
/// list of values, and the closing bracket. The source is resolved with
/// <see cref="MetadataSource.TryGet"/>, first as written and then without
/// an attached <c>id</c>/<c>ids</c>; a tag whose source is not registered,
/// or is local to the server, is left out.
/// </para>
/// <para>
/// A value's prefix says what it names, ignoring case: <c>a</c>, <c>s</c>
/// or <c>tv</c> a series, <c>m</c> a movie and <c>e</c> an episode. In a
/// file name each value is one of:
/// <list type="bullet">
/// <item><c>(a|s|tv)&lt;series&gt;</c>;</item>
/// <item><c>m&lt;movie&gt;[part]</c>;</item>
/// <item>a segment <c>[[a|s|tv]&lt;series&gt;(-|=|.|space|)][e]&lt;episode&gt;[part]</c>,
/// where the separator may be left out only before an <c>e</c>;</item>
/// </list>
/// and a part is <c>('|@)&lt;percent&gt;[-[&lt;percent&gt;]][%]</c>: a
/// range, a start running to the end (<c>'50-</c>, 50–100), or a size from
/// the start (<c>'50</c>, 0–50). In a folder name each value is a number,
/// optionally with one of the prefixes,
/// and takes no part. A bare number is an episode in a file name and a
/// series in a folder name for AniDB, and of no known type for the other
/// sources. A tag counts only if every value in it is valid. Folder tags
/// take no leading space after the bracket and only <c>id</c> as their
/// suffix, as the importer's AniDB folder tags did.
/// </para>
/// <para>
/// The importer reads its AniDB IDs from the same tags: the anime from the
/// series IDs, and the episodes from the file name's values, written as its
/// release IDs write them (<see cref="TagValue.ImporterText"/>), each part
/// as a range, since a release ID counts a size on from the previous part.
/// </para>
/// </remarks>
internal static partial class FileNameIds
{
    /// <summary>
    /// A tag, with its values as written.
    /// </summary>
    /// <param name="Source">The source.</param>
    /// <param name="Depth">How many folders up the tag was found.</param>
    /// <param name="Values">The values.</param>
    /// <param name="IsForced">Whether a custom parsing rule forced the tag.</param>
    internal sealed record Tag(MetadataSource Source, int Depth, IReadOnlyList<TagValue> Values, bool IsForced = false);

    /// <summary>
    /// A value of a tag.
    /// </summary>
    /// <param name="Text">The value as written, trimmed.</param>
    /// <param name="IsSeriesOnly">Whether the value names a series alone, as <c>a123</c> or <c>tv123</c> in a file name.</param>
    /// <param name="IsMovie">Whether the value names a movie, as <c>m123</c> in a file name.</param>
    internal sealed record TagValue(string Text, bool IsSeriesOnly, bool IsMovie = false)
    {
        /// <summary>
        /// The value as the importer's release IDs write it: a movie as the
        /// episode it is on AniDB (<c>m123'50</c> as <c>e123'0-50</c>), a
        /// series prefix as <c>a</c> (<c>tv123-456</c> as <c>a123-456</c>),
        /// and a size or an open range as the range it stands for, since a
        /// release ID counts a size on from the previous part.
        /// </summary>
        internal string ImporterText => ImporterPartRegex().Replace(
            IsMovie ? "e" + Text[1..] : ImporterSeriesPrefixRegex().Replace(Text, "a"),
            match =>
            {
                if (match.Groups["partEnd"].Success)
                    return match.Value;

                var (start, end) = GetPart(match);
                return $"{match.Groups["delimiter"].Value}{start}-{end}{match.Groups["percent"].Value}";
            }
        );
    }

    /// <summary>
    /// Reads the tags in the names of a path.
    /// </summary>
    /// <param name="segments">The names in the path, the file name last.</param>
    /// <returns>The tags, the file name's first, then each folder's going up, each name's in order.</returns>
    internal static List<Tag> ReadTags(IReadOnlyList<string> segments)
    {
        var tags = new List<Tag>();
        for (var depth = 0; depth < segments.Count; depth++)
            ReadName(segments[segments.Count - 1 - depth], depth, tags);
        return tags;
    }

    /// <summary>
    /// Lists the IDs the tags name.
    /// </summary>
    /// <param name="tags">The tags.</param>
    /// <returns>The IDs, in the order of the tags and their values.</returns>
    internal static List<TaggedMetadataID> ToIDs(IEnumerable<Tag> tags)
    {
        var ids = new List<TaggedMetadataID>();
        foreach (var tag in tags)
        {
            var isAnidb = tag.Source == MetadataSource.AniDB;
            if (tag.Depth > 0)
            {
                // A bare number in a folder names the anime for AniDB, and
                // anything for the other sources.
                foreach (var value in tag.Values)
                {
                    var match = FolderValueRegex().Match(value.Text);
                    var type = GetType(match.Groups["prefix"].Value) ?? (isAnidb ? MetadataEntityType.Series : null);
                    ids.Add(new() { Source = tag.Source, ID = match.Groups["id"].Value, Type = type, Depth = tag.Depth, IsForced = tag.IsForced });
                }

                continue;
            }

            foreach (var value in tag.Values)
            {
                if (SeriesValueRegex().Match(value.Text) is { Success: true } seriesMatch)
                {
                    ids.Add(new() { Source = tag.Source, ID = seriesMatch.Groups["id"].Value, Type = MetadataEntityType.Series, Depth = tag.Depth, IsForced = tag.IsForced });
                    continue;
                }

                if (MovieValueRegex().Match(value.Text) is { Success: true } movieMatch)
                {
                    var (movieStart, movieEnd) = ReadPart(movieMatch);
                    ids.Add(new() { Source = tag.Source, ID = movieMatch.Groups["id"].Value, Type = MetadataEntityType.Movie, Depth = tag.Depth, IsForced = tag.IsForced, PercentageStart = movieStart, PercentageEnd = movieEnd });
                    continue;
                }

                var segment = SegmentValueRegex().Match(value.Text);
                var hasSeries = segment.Groups["series"].Success;
                if (hasSeries)
                    ids.Add(new() { Source = tag.Source, ID = segment.Groups["series"].Value, Type = MetadataEntityType.Series, Depth = tag.Depth, IsForced = tag.IsForced });

                // A bare number in a file name is an episode for AniDB, and
                // anything for the other sources.
                var isEpisode = isAnidb || hasSeries || segment.Groups["marker"].Success;
                var (start, end) = ReadPart(segment);
                ids.Add(new() { Source = tag.Source, ID = segment.Groups["id"].Value, Type = isEpisode ? MetadataEntityType.Episode : null, Depth = tag.Depth, IsForced = tag.IsForced, PercentageStart = start, PercentageEnd = end });
            }
        }

        return ids;
    }

    private static (int? Start, int? End) ReadPart(Match match)
        => match.Groups["partStart"].Success ? GetPart(match) : (null, null);

    /// <summary>
    /// Works out a part from its <c>partStart</c>, <c>partRange</c> and
    /// <c>partEnd</c> groups: a size runs from 0, an open range to 100, and
    /// a part going past 100 is the whole of it, as the release IDs read it.
    /// </summary>
    private static (int Start, int End) GetPart(Match match)
    {
        var first = int.Parse(match.Groups["partStart"].Value);
        var (start, end) = match.Groups["partRange"].Success
            ? (first, match.Groups["partEnd"].Success ? int.Parse(match.Groups["partEnd"].Value) : 100)
            : (0, first);
        return end > 100 ? (0, 100) : (start, end);
    }

    private static MetadataEntityType? GetType(string prefix) => prefix.ToLowerInvariant() switch
    {
        "a" or "s" or "tv" => MetadataEntityType.Series,
        "m" => MetadataEntityType.Movie,
        "e" => MetadataEntityType.Episode,
        _ => null,
    };

    private static void ReadName(string name, int depth, List<Tag> tags)
    {
        if (string.IsNullOrEmpty(name))
            return;

        var isFileName = depth is 0;
        foreach (Match match in TagRegex().Matches(name))
        {
            var word = match.Groups["word"].Value;
            var separatedSuffix = match.Groups["suffix"].Value;
            if (ResolveSource(word) is not var (source, attachedSuffix))
                continue;

            if (!isFileName)
            {
                // `anidb(?:[\- \.]?id)?`, right after the bracket.
                if (match.Groups["lead"].Length > 0)
                    continue;
                if (attachedSuffix.Length > 0 ? attachedSuffix is not "id" || separatedSuffix.Length > 0 : separatedSuffix.Length > 0 && !FolderSuffixRegex().IsMatch(separatedSuffix))
                    continue;
            }
            else if (attachedSuffix.Length > 0 && separatedSuffix.Length > 0)
            {
                continue;
            }

            if (ReadValues(match.Groups["values"].Value, isFileName) is { } values)
                tags.Add(new(source, depth, values));
        }
    }

    /// <summary>
    /// Reads IDs a custom parsing rule forces, written as a file name's tag
    /// without the brackets.
    /// </summary>
    /// <param name="source">The source, its value or an alias.</param>
    /// <param name="ids">The comma-separated values.</param>
    /// <returns>
    /// The tag, at depth <c>0</c> and marked as forced, or <c>null</c> if the
    /// source is not registered, is local to the server, or a value does not
    /// read.
    /// </returns>
    internal static Tag? ReadForced(string? source, string? ids)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(ids))
            return null;
        if (!MetadataSource.TryGet(source.Trim(), out var metadataSource) || metadataSource.IsLocal)
            return null;
        if (ReadValues(ids, isFileName: true) is not { Count: > 0 } values)
            return null;

        return new(metadataSource, 0, values, IsForced: true);
    }

    private static (MetadataSource Source, string AttachedSuffix)? ResolveSource(string word)
    {
        if (MetadataSource.TryGet(word, out var source))
            return source.IsLocal ? null : (source, string.Empty);

        foreach (var suffix in (ReadOnlySpan<string>)["ids", "id"])
        {
            if (word.Length > suffix.Length && word.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && MetadataSource.TryGet(word[..^suffix.Length], out source))
                return source.IsLocal ? null : (source, suffix);
        }

        return null;
    }

    private static List<TagValue>? ReadValues(string text, bool isFileName)
    {
        var parts = text.Split(',');
        // One trailing comma right before the bracket is allowed.
        if (parts.Length > 1 && parts[^1].Length is 0)
            parts = parts[..^1];

        var values = new List<TagValue>(parts.Length);
        foreach (var part in parts)
        {
            var value = part.Trim();
            if (!isFileName)
            {
                if (!FolderValueRegex().IsMatch(value))
                    return null;
                values.Add(new(value, false));
            }
            else if (SeriesValueRegex().IsMatch(value))
            {
                values.Add(new(value, true));
            }
            else if (MovieValueRegex().IsMatch(value))
            {
                values.Add(new(value, false, true));
            }
            else if (SegmentValueRegex().IsMatch(value))
            {
                values.Add(new(value, false));
            }
            else
            {
                return null;
            }
        }

        return values;
    }

    [GeneratedRegex(@"\[(?<lead>\s*)(?<word>[a-z][a-z0-9_]*)(?<suffix>[\- \.]?\s*ids?)?[\-= ](?<values>[^\[\]\(\)\{\}]*)\]|\((?<lead>\s*)(?<word>[a-z][a-z0-9_]*)(?<suffix>[\- \.]?\s*ids?)?[\-= ](?<values>[^\[\]\(\)\{\}]*)\)|\{(?<lead>\s*)(?<word>[a-z][a-z0-9_]*)(?<suffix>[\- \.]?\s*ids?)?[\-= ](?<values>[^\[\]\(\)\{\}]*)\}", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"^[\- \.]?id$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex FolderSuffixRegex();

    [GeneratedRegex(@"^(?<prefix>a|s|tv|m|e)?(?<id>\d+)$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex FolderValueRegex();

    [GeneratedRegex(@"^(?:a|s|tv)(?<id>\d+)$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex SeriesValueRegex();

    [GeneratedRegex(@"^m(?<id>\d+)(?:['@](?<partStart>\d+)(?<partRange>\-(?<partEnd>\d+)?)?%?)?$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex MovieValueRegex();

    [GeneratedRegex(@"^(?:(?:a|s|tv)?(?<series>\d+)(?:[=\- \.]|(?=e)))?(?<marker>e)?(?<id>\d+)(?:['@](?<partStart>\d+)(?<partRange>\-(?<partEnd>\d+)?)?%?)?$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex SegmentValueRegex();

    [GeneratedRegex(@"^(?:s|tv)(?=\d)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex ImporterSeriesPrefixRegex();

    [GeneratedRegex(@"(?<delimiter>['@])(?<partStart>\d+)(?<partRange>\-(?<partEnd>\d+)?)?(?<percent>%?)$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex ImporterPartRegex();
}

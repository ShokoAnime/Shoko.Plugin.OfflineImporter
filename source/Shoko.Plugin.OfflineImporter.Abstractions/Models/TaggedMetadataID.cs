using Shoko.Abstractions.Metadata;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Models;

/// <summary>
/// An ID found in a file or folder name, written as a tag such as
/// <c>[anidb-123]</c>, <c>{anidb=a123,456}</c>, <c>[tmdb-m456]</c> or
/// <c>[tmdbid-tv456]</c>.
/// </summary>
public sealed record TaggedMetadataID
{
    /// <summary>
    /// The source the tag names, as registered with the server. Tags naming a
    /// source that is not registered are left out.
    /// </summary>
    public required MetadataSource Source { get; init; }

    /// <summary>
    /// The ID, as the source writes it.
    /// </summary>
    public required string ID { get; init; }

    /// <summary>
    /// What the ID names, when the tag says:
    /// <see cref="MetadataEntityType.Series"/> for <c>a123</c>, <c>s123</c>
    /// and <c>tv123</c> and the first half of <c>123-456</c>,
    /// <see cref="MetadataEntityType.Movie"/> for <c>m123</c>, and
    /// <see cref="MetadataEntityType.Episode"/> for <c>e456</c> and the
    /// second half of <c>123-456</c>; the prefixes ignore case. A bare number
    /// is an episode in an AniDB tag in a file name and an anime in an AniDB
    /// tag in a folder name, and is left <c>null</c> for every other source.
    /// </summary>
    public required MetadataEntityType? Type { get; init; }

    /// <summary>
    /// Where the tag was found: <c>0</c> for the file name, <c>1</c> for the
    /// folder holding the file, <c>2</c> for the one above it, and so on, up to
    /// the managed folder.
    /// </summary>
    public required int Depth { get; init; }

    /// <summary>
    /// Where the file's part of the episode or movie starts, from 0 to 100,
    /// when a file name's tag gives one: <c>'</c> or <c>@</c>, then a size from
    /// the start (<c>456@50</c>, <c>m123'50</c>: 0–50), a start running to the
    /// end (<c>456'50-</c>: 50–100) or a range (<c>456'25-75%</c>), the
    /// <c>%</c> optional.
    /// </summary>
    public int? PercentageStart { get; init; }

    /// <summary>
    /// Where the file's part of the episode or movie ends, from 0 to 100.
    /// </summary>
    public int? PercentageEnd { get; init; }

    /// <summary>
    /// Whether a custom parsing rule forced the ID rather than a tag in the
    /// path. Forced IDs come first, with a <see cref="Depth"/> of <c>0</c>.
    /// </summary>
    public bool IsForced { get; init; }
}

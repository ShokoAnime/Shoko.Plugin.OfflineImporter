using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Models;

/// <summary>
/// Contains information about a parsed file path.
/// </summary>
public sealed class ParsedFileResult
{
    /// <summary>
    /// The rule name used to match this file.
    /// </summary>
    public required string RuleName { get; set; }

    /// <summary>
    /// Whether the file was matched.
    /// </summary>
    [MemberNotNullWhen(true, nameof(FilePath), nameof(FileExtension))]
    public required bool Success { get; set; }

    /// <summary>
    /// The file path. It may be empty if <see cref="Success"/> is <c>false</c>.
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// The file extension.
    /// </summary>
    public required string? FileExtension { get; set; }

    /// <summary>
    /// The series name.
    /// </summary>
    public string? SeriesName { get; set; }

    /// <summary>
    /// The series type.
    /// </summary>
    public AnimeType? SeriesType { get; set; }

    /// <summary>
    /// The series's release year, if found.
    /// </summary>
    public int? SeriesYear { get; set; }

    /// <summary>
    /// The season number, if found in the filename or folder name.
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// The episode name.
    /// </summary>
    public string? EpisodeName { get; set; }

    /// <summary>
    /// The episode type.
    /// </summary>
    public EpisodeType EpisodeType { get; set; }

    /// <summary>
    /// The episode start number.
    /// </summary>
    public float EpisodeStart { get; set; }

    /// <summary>
    /// The episode end number.
    /// </summary>
    public float EpisodeEnd { get; set; }

    /// <summary>
    /// The episode text, different from the episode name as it describes
    /// the episode range in textual form.
    /// </summary>
    public string? EpisodeText { get; set; }

    /// <summary>
    /// Indicates if the theme video is creditless.
    /// </summary>
    public bool? IsCreditless { get; set; }

    /// <summary>
    /// Indicates if the episode is censored or uncensored, as described in the
    /// file name.
    /// </summary>
    public bool? IsCensored { get; set; }

    /// <summary>
    /// The release group as described in the file name.
    /// </summary>
    public string? ReleaseGroup { get; set; }

    /// <summary>
    /// The release source as described in the file name.
    /// </summary>
    public ReleaseSource? ReleaseSource { get; set; }

    /// <summary>
    /// The release revision/version as described in the file name.
    /// </summary>
    public int? ReleaseVersion { get; set; }

    /// <summary>
    /// The IDs found in the file name and the folders above it, such as
    /// <c>[anidb-123]</c>, <c>{tmdb=456}</c> or <c>[tmdbid-456]</c>.
    /// </summary>
    public IReadOnlyList<TaggedMetadataID> IDs { get; set; } = [];

    /// <summary>
    /// An empty result.
    /// </summary>
    public static ParsedFileResult Empty => new()
    {
        RuleName = "none",
        Success = false,
        FilePath = string.Empty,
        FileExtension = null,
    };
}

using System.Collections.Generic;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Release;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Services;

/// <summary>
/// Decides by auto-match rules whether a release may be matched to a video
/// automatically, and reads and replaces the Offline Importer's own rules.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by the Offline Importer and taken from the service container;
/// other plugins are not meant to implement it, so members added to it later
/// are minor changes.
/// </para>
/// <para>
/// The rules are <see cref="AutoMatchRule"/>s, so another plugin can keep
/// rules of its own in its settings and have them decided the same way.
/// </para>
/// <para>
/// Episode ranges are checked against each cross-reference's episode: its
/// AniDB episode when it names one, else its first episode of any other
/// source, read through the server's metadata. A source that numbers its
/// episodes by season, such as TMDB, is checked against the number within
/// the episode's season, since the ranges carry no season.
/// </para>
/// </remarks>
public interface IOfflineAutoMatchService
{
    /// <summary>
    /// Decides whether a release may be matched automatically. The first
    /// rule whose location, release group and episode range checks all pass
    /// decides, allowing or denying the release; when none passes, the
    /// release is not allowed.
    /// </summary>
    /// <param name="rules">The rules, in order.</param>
    /// <param name="videoFiles">The files of the video the release is for, for the location checks.</param>
    /// <param name="release">The release, for the release group and episode range checks.</param>
    /// <returns>The decision.</returns>
    AutoMatchDecision Evaluate(IReadOnlyList<AutoMatchRule> rules, IReadOnlyList<IVideoFile> videoFiles, IReleaseInfo release);

    /// <inheritdoc cref="Evaluate(IReadOnlyList{AutoMatchRule}, IReadOnlyList{IVideoFile}, IReleaseInfo)"/>
    AutoMatchDecision Evaluate(IReadOnlyList<AutoMatchRule> rules, IReadOnlyList<IVideoFile> videoFiles, ReleaseInfo release);

}

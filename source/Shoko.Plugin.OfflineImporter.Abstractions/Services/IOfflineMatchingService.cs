using System;
using System.Collections.Generic;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Video;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Services;

/// <summary>
/// Runs the Offline Importer's file name matching: its parsing rules and
/// the cleanup after them, from a path to a <see cref="ParsedFileResult"/>,
/// and reads and replaces the custom parsing rules it runs.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by the Offline Importer and taken from the service container;
/// other plugins are not meant to implement it, so members added to it later
/// are minor changes.
/// </para>
/// <para>
/// Matching is synchronous and reads no files, asks no database or remote
/// service and never imports anything: it stops where the importer would
/// start looking for a release. It uses the parsing rules the user has
/// configured at the time of the call, so changes to them apply at once and
/// should not be copied.
/// </para>
/// </remarks>
public interface IOfflineMatchingService
{
    /// <summary>
    /// Matches a path that is not tied to a managed folder.
    /// </summary>
    /// <param name="path">
    /// The path, absolute or as given. ID tags are read from the file name
    /// and from every folder in the path.
    /// </param>
    /// <returns>
    /// The result. When no rule matched, <see cref="ParsedFileResult.Success"/>
    /// is <c>false</c>, and only <see cref="ParsedFileResult.IDs"/> is filled in.
    /// </returns>
    ParsedFileResult Match(string path);

    /// <summary>
    /// Matches a path relative to a managed folder.
    /// </summary>
    /// <param name="managedFolder">The managed folder the path is in.</param>
    /// <param name="relativePath">The path relative to the managed folder.</param>
    /// <returns>
    /// The result. Rules working on the full path see the managed folder's
    /// path joined with the relative path, while ID tags are only read from
    /// the file name and the folders inside the managed folder. When no rule
    /// matched, <see cref="ParsedFileResult.Success"/> is <c>false</c>, and
    /// only <see cref="ParsedFileResult.IDs"/> is filled in.
    /// </returns>
    ParsedFileResult Match(IManagedFolder managedFolder, string relativePath);

}

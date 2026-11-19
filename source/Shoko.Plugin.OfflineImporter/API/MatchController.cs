using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Services;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Abstractions.Services;
using Shoko.Plugin.OfflineImporter.Models;

namespace Shoko.Plugin.OfflineImporter.API;

/// <summary>
/// Runs the importer's parsing rules on paths, without importing anything.
/// </summary>
/// <param name="matcher">The matching service.</param>
/// <param name="videoService">The video service, to find managed folders.</param>
[ApiController]
[Authorize("admin")]
[Route("api/plugin/offline/v1/[controller]")]
public class MatchController(IOfflineMatchingService matcher, IVideoService videoService) : ControllerBase
{
    /// <summary>
    /// Matches one or more paths, as the importer would before it searches
    /// for a release. Reads nothing from disk and saves nothing.
    /// </summary>
    /// <param name="body">The paths.</param>
    /// <returns>The parsed results, in the order of the paths.</returns>
    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public ActionResult<IReadOnlyList<ParsedFileResult>> Match([FromBody] MatchRequest body)
    {
        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(body.Path))
            paths.Add(body.Path);
        if (body.Paths is { Count: > 0 })
            paths.AddRange(body.Paths.Where(path => !string.IsNullOrWhiteSpace(path)));
        if (paths.Count is 0)
        {
            ModelState.AddModelError(nameof(body.Path), "Give a path or a list of paths.");
            return ValidationProblem(ModelState);
        }

        if (body.ManagedFolderID is { } folderID)
        {
            if (videoService.GetManagedFolderByID(folderID) is not { } folder)
                return NotFound($"No managed folder with ID {folderID}.");

            return Ok(paths.Select(path => matcher.Match(folder, path)).ToList());
        }

        return Ok(paths.Select(MatchPath).ToList());
    }

    private ParsedFileResult MatchPath(string path)
    {
        if (Path.IsPathFullyQualified(path))
        {
            if (videoService.GetVideoFileByAbsolutePath(path) is { ManagedFolder: { } knownFolder, RelativePath: { } knownPath })
                return matcher.Match(knownFolder, knownPath);

            // The deepest managed folder holding the path, for a file not
            // known yet.
            if (FindManagedFolder(path) is ({ } folder, { } relativePath))
                return matcher.Match(folder, relativePath);

            return matcher.Match(path);
        }

        if (videoService.GetVideoFileByRelativePath(path) is { ManagedFolder: { } fileFolder, RelativePath: { } filePath })
            return matcher.Match(fileFolder, filePath);

        return matcher.Match(path);
    }

    private (IManagedFolder? Folder, string? RelativePath) FindManagedFolder(string path)
        => videoService.GetAllManagedFolders()
            .Select(folder => (Folder: (IManagedFolder?)folder, RelativePath: (string?)Path.GetRelativePath(folder.Path, path)))
            .Where(pair => !Path.IsPathRooted(pair.RelativePath) && pair.RelativePath is not "." && !pair.RelativePath!.StartsWith(".."))
            .OrderBy(pair => pair.RelativePath!.Length)
            .FirstOrDefault();
}

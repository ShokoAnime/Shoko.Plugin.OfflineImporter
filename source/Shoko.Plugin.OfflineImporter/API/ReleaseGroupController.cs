using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Video.Release;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Abstractions.Services;
using Shoko.Plugin.OfflineImporter.Models;

namespace Shoko.Plugin.OfflineImporter.API;

/// <summary>
/// Looks up AniDB release groups in the offline group list, and reads and
/// replaces the release group settings.
/// </summary>
/// <param name="releaseGroupService">The release group service.</param>
[ApiController]
[Authorize("admin")]
[Route("api/plugin/offline/v1/[controller]")]
public class ReleaseGroupController(IOfflineReleaseGroupService releaseGroupService) : ControllerBase
{
    /// <summary>
    /// Gets a page of the AniDB release groups in the offline group list, all
    /// of them or those a search matches.
    /// </summary>
    /// <param name="search">
    /// Optional. Text to search for in the names and short names, fuzzily;
    /// the matches come best first. Without it, the groups come by name.
    /// </param>
    /// <param name="applyExclusions">Whether to leave the excluded groups out.</param>
    /// <param name="pageSize">The page size. Set to <c>0</c> to disable pagination.</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The page, with the number of groups in all.</returns>
    [HttpGet]
    public ActionResult<ListResult<ReleaseGroup>> GetReleaseGroups(
        [FromQuery] string? search = null,
        [FromQuery] bool applyExclusions = false,
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
        => ListResult<ReleaseGroup>.FromPage(
            string.IsNullOrWhiteSpace(search)
                ? releaseGroupService.GetAllReleaseGroups(applyExclusions)
                : releaseGroupService.GetManyReleaseGroupsBySearch(search, applyExclusions),
            page,
            pageSize
        );

    /// <summary>
    /// Gets the AniDB release groups whose name or short name is the given
    /// name, compared exactly.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="applyPreferencesAndExclusions">Whether to put the preferred group first and leave the excluded ones out.</param>
    /// <returns>The groups, the one the importer would take first.</returns>
    [HttpGet("ByName")]
    public ActionResult<IReadOnlyList<ReleaseGroup>> GetManyReleaseGroupsByName([FromQuery, Required] string name, [FromQuery] bool applyPreferencesAndExclusions = true)
        => Ok(releaseGroupService.GetManyReleaseGroupsByName(name, applyPreferencesAndExclusions));

    /// <summary>
    /// Gets an AniDB release group by its ID.
    /// </summary>
    /// <param name="anidbGroupID">The AniDB release group ID.</param>
    /// <returns>The group.</returns>
    [HttpGet("{anidbGroupID:int}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public ActionResult<ReleaseGroup> GetReleaseGroupByID([FromRoute] int anidbGroupID)
        => releaseGroupService.GetReleaseGroupByID(anidbGroupID) is { } group ? Ok(group) : NotFound();

    /// <summary>
    /// Gets a page of the names that more than one AniDB release group has.
    /// </summary>
    /// <param name="pageSize">The page size. Set to <c>0</c> to disable pagination.</param>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The page of names, each with its groups, with the number of names in all.</returns>
    [HttpGet("Shared")]
    public ActionResult<ListResult<SharedReleaseGroupName>> GetAllSharedReleaseGroupNames(
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
        => ListResult<SharedReleaseGroupName>.FromPage(releaseGroupService.GetAllSharedReleaseGroupNames(), page, pageSize);

}

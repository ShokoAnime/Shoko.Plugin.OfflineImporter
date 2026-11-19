using System.Collections.Generic;

namespace Shoko.Plugin.OfflineImporter.Models;

/// <summary>
/// The paths to match.
/// </summary>
public class MatchRequest
{
    /// <summary>
    /// One path to match.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// More paths to match, after <see cref="Path"/>.
    /// </summary>
    public List<string>? Paths { get; set; }

    /// <summary>
    /// The managed folder the paths are relative to. Without it, an
    /// absolute path is matched within the managed folder holding it, and a
    /// relative path within the managed folder of the file it names, or as
    /// given when no known file has it.
    /// </summary>
    public int? ManagedFolderID { get; set; }
}

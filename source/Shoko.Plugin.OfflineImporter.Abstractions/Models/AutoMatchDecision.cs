using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Models;

/// <summary>
/// What the auto-match rules decided for a release.
/// </summary>
public sealed record AutoMatchDecision
{
    /// <summary>
    /// Whether the release may be matched automatically.
    /// </summary>
    public required bool IsAllowed { get; init; }

    /// <summary>
    /// The rule that decided, or <c>null</c> when none applied, in which case
    /// the release is not allowed.
    /// </summary>
    public required AutoMatchRule? Rule { get; init; }
}

using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// The type of group to validate a rule against a potential match.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutoMatchReleaseGroupRuleType : byte
{
    /// <summary>
    /// Matches against a custom group.
    /// </summary>
    [Display(Name = "Custom Group")]
    [EnumMember(Value = "custom")]
    [JsonStringEnumMemberName("custom")]
    Custom = 0,

    /// <summary>
    /// Matches against an AniDB group.
    /// </summary>
    [Display(Name = "AniDB Group")]
    [EnumMember(Value = "anidb")]
    [JsonStringEnumMemberName("anidb")]
    AniDB = 1,
}

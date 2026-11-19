using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// Auto matching episode range rule type.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutoMatchEpisodeRangeRuleType : byte
{
    /// <summary>
    /// Select all episodes for the given episode type.
    /// </summary>
    [Display(Name = "All Episodes")]
    [EnumMember(Value = "all")]
    [JsonStringEnumMemberName("all")]
    All = 1,

    /// <summary>
    /// Select a single episode for the given episode type.
    /// </summary>
    [Display(Name = "Single Episode")]
    [EnumMember(Value = "single")]
    [JsonStringEnumMemberName("single")]
    Single = 2,

    /// <summary>
    /// Select a range of episodes for the given episode type.
    /// </summary>
    [Display(Name = "Range of Episodes")]
    [EnumMember(Value = "range")]
    [JsonStringEnumMemberName("range")]
    Range = 3,
}

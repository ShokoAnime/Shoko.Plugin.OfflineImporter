using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// Auto matching episode range type.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutoMatchEpisodeRangeType : byte
{
    /// <summary>
    /// Normal episodes.
    /// </summary>
    [Display(Name = "Normal Episodes")]
    [EnumMember(Value = "episode")]
    [JsonStringEnumMemberName("episode")]
    Episode = 1,

    /// <summary>
    /// Specials.
    /// </summary>
    [Display(Name = "Specials")]
    [EnumMember(Value = "special")]
    [JsonStringEnumMemberName("special")]
    Special = 2,

    /// <summary>
    /// Any credits, including opening credits, ending credits, any anything else.
    /// </summary>
    [Display(Name = "Any Credits")]
    [EnumMember(Value = "credits")]
    [JsonStringEnumMemberName("credits")]
    AnyCredits = 3,

    /// <summary>
    /// Opening credits.
    /// </summary>
    [Display(Name = "Opening Credits")]
    [EnumMember(Value = "openings")]
    [JsonStringEnumMemberName("openings")]
    OpeningCredits = 4,

    /// <summary>
    /// Ending credits.
    /// </summary>
    [Display(Name = "Ending Credits")]
    [EnumMember(Value = "endings")]
    [JsonStringEnumMemberName("endings")]
    EndingCredits = 5,

    /// <summary>
    /// Trailers / previews.
    /// </summary>
    [Display(Name = "Trailers / Preview Videos (PVs)")]
    [EnumMember(Value = "trailer")]
    [JsonStringEnumMemberName("trailer")]
    Trailer = 6,

    /// <summary>
    /// Parodies. Legacy type.
    /// </summary>
    [Display(Name = "Parodies (Legacy)")]
    [EnumMember(Value = "parody")]
    [JsonStringEnumMemberName("parody")]
    Parody = 7,

    /// <summary>
    /// Other type episodes. Edge cases.
    /// </summary>
    [Display(Name = "Other Type Episodes / Edge Cases")]
    [EnumMember(Value = "other")]
    [JsonStringEnumMemberName("other")]
    Other = 8,
}

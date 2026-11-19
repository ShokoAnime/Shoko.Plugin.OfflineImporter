using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// Match mode to use during the import.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MatchMode : byte
{
    /// <summary>
    /// Lax mode.
    /// </summary>
    [Display(Name = "Lax Mode")]
    [EnumMember(Value = "lax")]
    [JsonStringEnumMemberName("lax")]
    Lax = 0,

    /// <summary>
    /// Strict mode, with extra info extracted from the file name.
    /// </summary>
    [Display(Name = "Strict Mode (Include Extra Info)")]
    [EnumMember(Value = "strict")]
    [JsonStringEnumMemberName("strict")]
    StrictAndInfo = 1,

    /// <summary>
    /// Strict mode, with no extra info.
    /// </summary>
    [Display(Name = "Strict Mode (No Extra Info)")]
    [EnumMember(Value = "strict-fast")]
    [JsonStringEnumMemberName("strict-fast")]
    StrictAndFast = 2,
}

using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// The type of a custom rule.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomRuleType : byte
{
    /// <summary>
    /// Default rule type.
    /// </summary>
    [Display(Name = "Default Match")]
    [EnumMember(Value = "default")]
    [JsonStringEnumMemberName("default")]
    [Key]
    Default = 0,

    /// <summary>
    /// Pre-post filtering rule. Has extra logic to handle "post"/"pre"
    /// groups in the match.
    /// </summary>
    /// <remarks>
    /// Will be applied on top of the default rule.
    /// </remarks>
    [Display(Name = "Pre/Post Match")]
    [EnumMember(Value = "pre-post")]
    [JsonStringEnumMemberName("pre-post")]
    PrePost = 1,

    /// <summary>
    /// Fallback rule. Has extra logic to handle missing episode
    /// numbers in the match.
    /// </summary>
    /// <remarks>
    /// Will be applied on top of the default rule.
    /// </remarks>
    [Display(Name = "Fallback Match (No Episode Number)")]
    [EnumMember(Value = "fallback")]
    [JsonStringEnumMemberName("fallback")]
    Fallback = 2,

    /// <summary>
    /// Will deny all matches to this rule.
    /// </summary>
    [Display(Name = "Deny Match")]
    [EnumMember(Value = "deny")]
    [JsonStringEnumMemberName("deny")]
    Deny = 3,
}

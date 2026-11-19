using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Plugin.OfflineImporter.Abstractions.Configuration;

/// <summary>
/// Auto matching behavior.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutoMatchRuleMatchType : byte
{
    /// <summary>
    /// Will allow all matches to this rule.
    /// </summary>
    [Display(Name = "Allow Matches")]
    [EnumMember(Value = "allow")]
    [JsonStringEnumMemberName("allow")]
    Allow = 0,

    /// <summary>
    /// Will deny all matches to this rule.
    /// </summary>
    [Display(Name = "Deny Matches")]
    [EnumMember(Value = "deny")]
    [JsonStringEnumMemberName("deny")]
    Deny = 1,
}

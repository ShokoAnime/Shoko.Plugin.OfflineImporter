using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Configuration;
using Xunit;

namespace Shoko.Plugin.OfflineImporter.Tests;

public class ConfigurationTests
{
    // As the server reads and writes plugin configurations.
    private static readonly JsonSerializerSettings _settings = new()
    {
        Formatting = Formatting.Indented,
        DefaultValueHandling = DefaultValueHandling.Include,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        Converters = [new StringEnumConverter()],
    };

    [Theory]
    [InlineData("offline-importer.json")]
    [InlineData("offline-importer-auto-match.json")]
    public void StoredConfiguration_RoundTrips(string fixture)
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        var stored = JObject.Parse(OfflineImporterConfiguration.ApplyMigrations(text, null!));
        stored.Remove("$schema");

        var configuration = JsonConvert.DeserializeObject<OfflineImporterConfiguration>(stored.ToString(), _settings)!;
        var written = JObject.Parse(JsonConvert.SerializeObject(configuration, _settings));

        // Newer than the stored file, so written with its default.
        Assert.Equal(new JObject(), written["PreferredReleaseGroups"]);
        written.Remove("PreferredReleaseGroups");

        Assert.NotEmpty(configuration.ParseRules);
        Assert.True(JToken.DeepEquals(stored, written), written.ToString());
    }

    [Fact]
    public void Migration_TurnsForcedAnidbIDsIntoForcedIDs()
    {
        var stored = new JObject
        {
            ["ParseRules"] = new JArray
            {
                new JObject { ["Name"] = "both", ["ForcedAnidbAnimeId"] = 123, ["ForcedAnidbEpisodeId"] = 456 },
                new JObject { ["Name"] = "anime", ["ForcedAnidbAnimeId"] = 123, ["ForcedAnidbEpisodeId"] = null },
                new JObject { ["Name"] = "none", ["ForcedAnidbAnimeId"] = null, ["ForcedAnidbEpisodeId"] = null },
            },
        };

        var migrated = JObject.Parse(OfflineImporterConfiguration.ApplyMigrations(stored.ToString(), null!));
        var configuration = JsonConvert.DeserializeObject<OfflineImporterConfiguration>(migrated.ToString(), _settings)!;

        Assert.DoesNotContain("ForcedAnidb", migrated.ToString());
        Assert.Equal(["a123,e456"], configuration.ParseRules[0].ForcedIDs.Select(forced => $"{forced.IDs}"));
        Assert.Equal(["anidb"], configuration.ParseRules[0].ForcedIDs.Select(forced => forced.Source));
        Assert.Equal(["a123"], configuration.ParseRules[1].ForcedIDs.Select(forced => forced.IDs));
        Assert.Empty(configuration.ParseRules[2].ForcedIDs);
    }

    [Fact]
    public void Migration_LeavesACurrentConfigurationAlone()
    {
        var current = JsonConvert.SerializeObject(new OfflineImporterConfiguration(), _settings);
        Assert.Same(current, OfflineImporterConfiguration.ApplyMigrations(current, null!));
    }

    [Theory]
    [InlineData("\"Parody\"")]
    [InlineData("\"parody\"")]
    public void StoredParody_StillReads(string json)
        => Assert.Equal(AutoMatchEpisodeRangeType.Parody, JsonConvert.DeserializeObject<AutoMatchEpisodeRangeType>(json));

    [Fact]
    public void StoredConfiguration_KeepsItsFileName()
        => Assert.Equal("offline-importer", typeof(OfflineImporterConfiguration).GetCustomAttribute<StorageLocationAttribute>()?.FileName);
}

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Services;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Configuration;
using Shoko.Plugin.OfflineImporter.Services;
using Xunit;

namespace Shoko.Plugin.OfflineImporter.Tests;

public class ImporterTests
{
    private static OfflineImporter CreateImporter(OfflineImporterConfiguration? configuration = null)
    {
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(configuration ?? new OfflineImporterConfiguration { MapAsAnidbGroupIfPossible = false });
        var provider = new ConfigurationProvider<OfflineImporterConfiguration>(configurationService.Object);
        var applicationPaths = Mock.Of<IApplicationPaths>();
        return new(
            NullLogger<OfflineImporter>.Instance,
            applicationPaths,
            Mock.Of<IAnidbService>(service => service.SearchAnimeByID(It.IsAny<int>()) == Mock.Of<IAnidbAnimeSearchResult>()),
            Mock.Of<IMetadataService>(),
            Mock.Of<IVideoService>(),
            provider,
            new(provider),
            new(provider, applicationPaths, new FakeFuzzySearchService()),
            new(NullLogger<OfflineAutoMatchService>.Instance, Mock.Of<IMetadataService>())
        );
    }

    private static async Task<string> Import(string path, OfflineImporterConfiguration? configuration = null)
    {
        var release = await CreateImporter(configuration).GetReleaseInfoById($"match://{path}", TestContext.Current.CancellationToken);
        Assert.NotNull(release);
        var references = release.CrossReferences.Select(reference => $"{string.Join('+', reference.IDs)}@{reference.PercentageStart}-{reference.PercentageEnd}");
        return $"{release.ID} {string.Join(' ', references)}";
    }

    [Theory]
    [InlineData("5", "5")]
    [InlineData("5'50", "5'0-50")]
    [InlineData("5'50-", "5'50-100")]
    [InlineData("5@25-75%", "5@25-75%")]
    public async Task AnidbMovieTag_ImportsAsTheEpisode(string value, string releaseValue)
    {
        var episode = await Import($"Show/Show - 01 [anidb-e{value}].mkv");

        Assert.StartsWith($"offline://e{releaseValue} anidb://episode/5@", episode);
        Assert.Equal(episode, await Import($"Show/Show - 01 [anidb-m{value}].mkv"));
    }

    [Fact]
    public async Task ForcedAnidbIDs_TakeThePlaceOfTheTaggedOnes()
    {
        var configuration = new OfflineImporterConfiguration { MapAsAnidbGroupIfPossible = false };
        foreach (var rule in configuration.ParseRules)
            rule.ForcedIDs = [new() { Source = "anidb", IDs = "a1,e2" }];

        var release = await Import("Show [anidb-7]/Show - 01 [anidb-e9].mkv", configuration);

        Assert.StartsWith("offline://e2 ", release);
        Assert.Contains("anidb://series/1", release);
        Assert.DoesNotContain("episode/9", release);
    }

    [Fact]
    public async Task StoredReleaseID_KeepsCountingSizesOnFromThePreviousPart()
    {
        var release = await CreateImporter().GetReleaseInfoById("offline://e5'50,e6'50", TestContext.Current.CancellationToken);

        Assert.NotNull(release);
        Assert.Equal([(0, 50), (50, 100)], release.CrossReferences.Select(reference => (reference.PercentageStart, reference.PercentageEnd)));
    }

    [Theory]
    [InlineData("Show/Show - 01 [anidb-s1,5].mkv", "offline://5 ", "anidb://series/1")]
    [InlineData("Show/Show - 01 [anidb-TV1-5].mkv", "offline://a1-5 ", "anidb://series/1")]
    [InlineData("Show [anidb-a17000]/Show - 01 [anidb-5].mkv", "offline://5 ", "anidb://series/17000")]
    [InlineData("Show [anidb-tv17000]/Show - 01 [anidb-5].mkv", "offline://5 ", "anidb://series/17000")]
    public async Task AnidbSeriesPrefixes_NameTheAnime(string path, string releaseID, string anime)
    {
        var release = await Import(path);

        Assert.StartsWith(releaseID, release);
        Assert.Contains(anime, release);
        Assert.Contains("anidb://episode/5", release);
    }
}

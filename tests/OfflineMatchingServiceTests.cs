using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Configuration;
using Shoko.Plugin.OfflineImporter.Services;
using Xunit;

namespace Shoko.Plugin.OfflineImporter.Tests;

public class OfflineMatchingServiceTests
{
    private static OfflineMatchingService CreateMatcher()
    {
        var service = new Mock<IConfigurationService>();
        service.Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(new OfflineImporterConfiguration());
        return new(new ConfigurationProvider<OfflineImporterConfiguration>(service.Object));
    }

    private static IManagedFolder CreateFolder(string path = "/mnt/anime")
    {
        var folder = new Mock<IManagedFolder>();
        folder.SetupGet(f => f.ID).Returns(7);
        folder.SetupGet(f => f.Path).Returns(path);
        return folder.Object;
    }

    private static IReadOnlyList<TaggedMetadataID> IDs(string relativePath)
        => CreateMatcher().Match(CreateFolder(), relativePath).IDs;

    [Fact]
    public void Match_ReadsTheFileName()
    {
        var result = CreateMatcher().Match("/mnt/anime/Show/[Group] Show Name - 05v2 [BD 1080p].mkv");

        Assert.True(result.Success);
        Assert.Equal("default", result.RuleName);
        Assert.Equal("Show Name", result.SeriesName);
        Assert.Equal("Group", result.ReleaseGroup);
        Assert.Equal(2, result.ReleaseVersion);
        Assert.Equal(EpisodeType.Episode, result.EpisodeType);
        Assert.Equal(5f, result.EpisodeStart);
        Assert.Equal(5f, result.EpisodeEnd);
        Assert.Empty(result.IDs);
    }

    [Fact]
    public void Match_ReadsIDsWhenNoRuleMatches()
    {
        var result = CreateMatcher().Match(CreateFolder(), "Show [anidb-1]/2021-05-01 12-00-00.mkv");

        Assert.False(result.Success);
        Assert.Equal([new TaggedMetadataID { Source = MetadataSource.AniDB, ID = "1", Type = MetadataEntityType.Series, Depth = 1 }], result.IDs);
    }

    [Fact]
    public void AnidbFileTag_KeepsItsMeaning()
        => Assert.Equal(
            [
                new() { Source = MetadataSource.AniDB, ID = "123", Type = MetadataEntityType.Series, Depth = 0 },
                new() { Source = MetadataSource.AniDB, ID = "456", Type = MetadataEntityType.Episode, Depth = 0 },
                new() { Source = MetadataSource.AniDB, ID = "123", Type = MetadataEntityType.Series, Depth = 0 },
                new() { Source = MetadataSource.AniDB, ID = "457", Type = MetadataEntityType.Episode, Depth = 0, PercentageStart = 0, PercentageEnd = 50 },
            ],
            IDs("Show/Show - 01 [anidb-a123, 456, 123-457@0-50%].mkv")
        );

    [Fact]
    public void AnidbFileTag_ReadsEachPartOnItsOwn()
        => Assert.Equal(
            [
                new() { Source = MetadataSource.AniDB, ID = "5", Type = MetadataEntityType.Episode, Depth = 0, PercentageStart = 0, PercentageEnd = 50 },
                new() { Source = MetadataSource.AniDB, ID = "6", Type = MetadataEntityType.Episode, Depth = 0, PercentageStart = 0, PercentageEnd = 50 },
                new() { Source = MetadataSource.AniDB, ID = "7", Type = MetadataEntityType.Episode, Depth = 0, PercentageStart = 50, PercentageEnd = 100 },
                new() { Source = MetadataSource.AniDB, ID = "8", Type = MetadataEntityType.Episode, Depth = 0, PercentageStart = 25, PercentageEnd = 75 },
                new() { Source = MetadataSource.AniDB, ID = "9", Type = MetadataEntityType.Episode, Depth = 0, PercentageStart = 0, PercentageEnd = 100 },
            ],
            IDs("Show/Show - 01 {anidb=e5'50%,e6'50%,e7@50-%,e8'25-75,e9'150}.mkv")
        );

    [Theory]
    [InlineData("Show [anidb-123]/Show - 01.mkv")]
    [InlineData("Show (anidb id=123)/Show - 01.mkv")]
    [InlineData("Show {anidbid 123}/Show - 01.mkv")]
    public void AnidbFolderTag_NamesTheAnime(string path)
        => Assert.Equal([new TaggedMetadataID { Source = MetadataSource.AniDB, ID = "123", Type = MetadataEntityType.Series, Depth = 1 }], IDs(path));

    [Theory]
    [InlineData("Show [ anidb-123]/Show - 01.mkv")]
    [InlineData("Show [anidb ids-123]/Show - 01.mkv")]
    [InlineData("Show [anidb-123,x]/Show - 01.mkv")]
    [InlineData("Show [anidb-a1-e2]/Show - 01.mkv")]
    [InlineData("Show [anidb-m1@50]/Show - 01.mkv")]
    public void AnidbFolderTag_KeepsTheFolderForm(string path)
        => Assert.Empty(IDs(path));

    [Fact]
    public void TmdbFolderTag_HasNoType()
        => Assert.Equal(
            [new TaggedMetadataID { Source = MetadataSource.TMDB, ID = "1234", Type = null, Depth = 2 }],
            IDs("Show [tmdb-1234]/Season 02/Show - S02E03.mkv")
        );

    [Theory]
    [InlineData("Show [tmdbid-1234]/Show - 01.mkv")]
    [InlineData("Show {tmdbid=1234}/Show - 01.mkv")]
    [InlineData("Show [themoviedb-1234]/Show - 01.mkv")]
    [InlineData("Show [TMDB 1234]/Show - 01.mkv")]
    public void TmdbFolderTag_TakesAliasesAndTheIDSuffix(string path)
        => Assert.Equal([new TaggedMetadataID { Source = MetadataSource.TMDB, ID = "1234", Type = null, Depth = 1 }], IDs(path));

    [Fact]
    public void TmdbFileTag_TakesTheSegmentGrammar()
        => Assert.Equal(
            [
                new() { Source = MetadataSource.TMDB, ID = "5", Type = null, Depth = 0 },
                new() { Source = MetadataSource.TMDB, ID = "6", Type = MetadataEntityType.Episode, Depth = 0 },
                new() { Source = MetadataSource.TMDB, ID = "1", Type = MetadataEntityType.Series, Depth = 0 },
                new() { Source = MetadataSource.TMDB, ID = "2", Type = MetadataEntityType.Episode, Depth = 0, PercentageStart = 0, PercentageEnd = 50 },
            ],
            IDs("Show/Show - 01 [tmdb-5,e6,1-2@50%].mkv")
        );

    [Theory]
    [InlineData("Show [foo-12]/Show [bar=3] - 01.mkv")]
    [InlineData("Show [user-12]/Show - 01 [shoko-3].mkv")]
    public void UnknownAndLocalSources_AreSkipped(string path)
        => Assert.Empty(IDs(path));

    [Fact]
    public void ManagedFolder_IsNotReadForIDs()
        => Assert.Empty(CreateMatcher().Match(CreateFolder("/mnt/anime [tmdb-1]"), "Show - 01.mkv").IDs);

    [Fact]
    public void ImporterTags_KeepTheValuesAsWritten()
    {
        CreateMatcher().Parse("/mnt/anime/Show/Show - 01 [anidb-a1,5,a2,6'50%].mkv", ["Show", "Show - 01 [anidb-a1,5,a2,6'50%].mkv"], out var tags);

        var tag = Assert.Single(tags);
        Assert.Equal("a2", tag.Values.Last(value => value.IsSeriesOnly).Text);
        Assert.Equal("6'50%", tag.Values.Last(value => !value.IsSeriesOnly).Text);
    }
}

using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Configuration;
using Shoko.Plugin.OfflineImporter.Services;
using Xunit;

namespace Shoko.Plugin.OfflineImporter.Tests;

public class TypedPrefixTests
{
    private static OfflineMatchingService CreateService()
    {
        var service = new Mock<IConfigurationService>();
        service.Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(new OfflineImporterConfiguration());
        return new(new ConfigurationProvider<OfflineImporterConfiguration>(service.Object));
    }

    private static IReadOnlyList<TaggedMetadataID> IDs(string relativePath)
    {
        var folder = new Mock<IManagedFolder>();
        folder.SetupGet(f => f.Path).Returns("/mnt/anime");
        return CreateService().Match(folder.Object, relativePath).IDs;
    }

    private static MetadataEntityType? Type(string prefix) => prefix.ToLowerInvariant() switch
    {
        "a" or "s" or "tv" => MetadataEntityType.Series,
        "m" => MetadataEntityType.Movie,
        "e" => MetadataEntityType.Episode,
        _ => null,
    };

    [Theory]
    [InlineData("a")]
    [InlineData("s")]
    [InlineData("TV")]
    [InlineData("m")]
    [InlineData("e")]
    public void FileTag_TypesEachPrefix(string prefix)
        => Assert.Equal([new TaggedMetadataID { Source = MetadataSource.TMDB, ID = "123", Type = Type(prefix), Depth = 0 }], IDs($"Show/Show - 01 [tmdb-{prefix}123].mkv"));

    [Theory]
    [InlineData("a")]
    [InlineData("s")]
    [InlineData("tv")]
    [InlineData("M")]
    [InlineData("e")]
    public void FolderTag_TypesEachPrefix(string prefix)
        => Assert.Equal([new TaggedMetadataID { Source = MetadataSource.TMDB, ID = "123", Type = Type(prefix), Depth = 1 }], IDs($"Show [tmdbid-{prefix}123]/Show - 01.mkv"));

    [Fact]
    public void FolderTag_KeepsTheBareNumbersMeaning()
        => Assert.Equal(
            [
                new() { Source = MetadataSource.TMDB, ID = "5", Type = null, Depth = 1 },
                new() { Source = MetadataSource.AniDB, ID = "6", Type = MetadataEntityType.Series, Depth = 1 },
            ],
            IDs("Show {tmdb=5} [anidb-6]/Show - 01.mkv")
        );

    [Theory]
    [InlineData("s123-e456")]
    [InlineData("tv123-456")]
    [InlineData("a123e456")]
    [InlineData("S123 E456")]
    public void FileTag_CombinesAnySeriesPrefixWithAnEpisode(string value)
        => Assert.Equal(
            [
                new() { Source = MetadataSource.TMDB, ID = "123", Type = MetadataEntityType.Series, Depth = 0 },
                new() { Source = MetadataSource.TMDB, ID = "456", Type = MetadataEntityType.Episode, Depth = 0 },
            ],
            IDs($"Show/Show - 01 [tmdb-{value}].mkv")
        );

    [Fact]
    public void FileTag_PutsAPartOnTheMovie()
        => Assert.Equal(
            [
                new() { Source = MetadataSource.TMDB, ID = "1", Type = MetadataEntityType.Movie, Depth = 0, PercentageStart = 0, PercentageEnd = 50 },
                new() { Source = MetadataSource.TMDB, ID = "2", Type = MetadataEntityType.Movie, Depth = 0, PercentageStart = 50, PercentageEnd = 100 },
                new() { Source = MetadataSource.TMDB, ID = "3", Type = MetadataEntityType.Movie, Depth = 0, PercentageStart = 25, PercentageEnd = 75 },
            ],
            IDs("Show/Show - 01 [tmdb-m1'50,m2@50-,m3@25-75%].mkv")
        );

    [Theory]
    [InlineData("m1-e2")]
    [InlineData("m1-2")]
    [InlineData("x5")]
    public void FileTag_RefusesAMovieCombinedOrAnUnknownPrefix(string value)
        => Assert.Empty(IDs($"Show/Show - 01 [tmdb-{value}].mkv"));

    [Fact]
    public void AnidbMovie_IsListedAsAMovie()
        => Assert.Equal([new TaggedMetadataID { Source = MetadataSource.AniDB, ID = "5", Type = MetadataEntityType.Movie, Depth = 0, PercentageStart = 0, PercentageEnd = 50 }], IDs("Show/Show - 01 [anidb-m5'50].mkv"));
}

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Configuration;
using Shoko.Plugin.OfflineImporter.Services;
using Xunit;

namespace Shoko.Plugin.OfflineImporter.Tests;

public class ParsingRuleTests
{
    private readonly Mock<IConfigurationService> _service = new();

    private OfflineImporterConfiguration _current = new();

    private int _saves;

    private OfflineMatchingService CreateService()
    {
        _service.Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(() => _current);
        _service.Setup(s => s.Save(It.IsAny<OfflineImporterConfiguration>()))
            .Callback<OfflineImporterConfiguration>(configuration =>
            {
                _current = configuration;
                _saves++;
                _service.Raise(s => s.Saved += null, new ConfigurationSavedEventArgs { ConfigurationInfo = null! });
            })
            .Returns(true);
        return new(new ConfigurationProvider<OfflineImporterConfiguration>(_service.Object));
    }

    private static CustomParseRule Rule(string name, string regex, CustomRuleType type = CustomRuleType.Default)
        => new() { Name = name, Regex = regex, Type = type };

    [Fact]
    public void ForcedIDs_ComeFirstInTheResult()
    {
        _current = new();
        foreach (var rule in _current.ParseRules)
            rule.ForcedIDs = [new() { Source = "themoviedb", IDs = "tv5" }, new() { Source = "anidb", IDs = "a1, e2'50" }];

        var ids = CreateService().Match("/mnt/anime/Show/Show - 01 [anidb-9].mkv").IDs;

        Assert.Equal(
            [
                new TaggedMetadataID { Source = MetadataSource.TMDB, ID = "5", Type = MetadataEntityType.Series, Depth = 0, IsForced = true },
                new TaggedMetadataID { Source = MetadataSource.AniDB, ID = "1", Type = MetadataEntityType.Series, Depth = 0, IsForced = true },
                new TaggedMetadataID { Source = MetadataSource.AniDB, ID = "2", Type = MetadataEntityType.Episode, Depth = 0, IsForced = true, PercentageStart = 0, PercentageEnd = 50 },
                new TaggedMetadataID { Source = MetadataSource.AniDB, ID = "9", Type = MetadataEntityType.Episode, Depth = 0 },
            ],
            ids
        );
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(params CustomParseRule[] rules)
        => OfflineImporterConfiguration.Validate(new() { ParseRules = [.. rules] }, null!, null!);

    [Fact]
    public void Validation_NamesForcedSourcesThatAreNotRegistered()
    {
        var rule = Rule("forced", "^(?<fallback>.+)$");
        rule.ForcedIDs = [new() { Source = "nope", IDs = "1" }, new() { Source = "anidb", IDs = "1" }];

        Assert.Equal(["ParseRules[0].ForcedIDs[0].Source"], Validate(rule).Keys);
    }

    [Theory]
    [InlineData("a123", true)]
    [InlineData("a123,e456'50-", true)]
    [InlineData(" 123-456 , m5@25-75% ", true)]
    [InlineData("TV1-E2", true)]
    [InlineData("x1", false)]
    [InlineData("a123-", false)]
    [InlineData("m1-2", false)]
    [InlineData("", false)]
    public void ForcedIDsPattern_AcceptsWhatReads(string ids, bool reads)
    {
        var pattern = typeof(ForcedMetadataIDs).GetProperty(nameof(ForcedMetadataIDs.IDs))!.GetCustomAttribute<RegularExpressionAttribute>()!;

        Assert.Equal(reads, ids.Length > 0 && pattern.IsValid(ids));
        Assert.Equal(reads, FileNameIds.ReadForced("anidb", ids) is not null);
    }

    [Fact]
    public void Validation_NamesEachParsingRuleProblem()
    {
        var errors = Validate(Rule("", "^a$"), Rule("b", "("), Rule("b", ""), Rule("c", "^c$"));

        Assert.Equal(["ParseRules[0].Name", "ParseRules[1].Regex", "ParseRules[2].Name", "ParseRules[2].Regex"], errors.Keys.Order());
    }

    [Fact]
    public void SavingTheSettings_AppliesTheRules()
    {
        var service = CreateService();

        _service.Object.Save(new OfflineImporterConfiguration { ParseRules = [Rule("everything", @"^(?<fallback>.+)\.(?<extension>[a-z0-9]+)$", CustomRuleType.Fallback)] });

        Assert.Equal(1, _saves);
        Assert.Equal("everything", service.Match("Show - 01.mkv").RuleName);
    }
    [Theory]
    [InlineData("/mnt/anime/Series/Tokyo.Revengers.S04E03.Crack.a.Smile.1080p.DSNP.WEB-DL.AAC2.0.H.264-NTb.mkv", "Tokyo Revengers")]
    [InlineData("/mnt/anime/Series/Space.Dandy.S01E01.1080p.WEB-DL.AAC2.0.H.264-NTb.mkv", "Space Dandy")]
    [InlineData("/mnt/anime/Series/Danger.Mail.S01E01.1080p.WEB-DL.mkv", "Danger Mail")]
    [InlineData("/mnt/anime/Series/Trip.Hazard.S01E01.1080p.WEB-DL.mkv", "Trip Hazard")]
    public void SeriesName_KeepsWordsThatHoldATagInside(string path, string seriesName)
    {
        _current = new();

        Assert.Equal(seriesName, CreateService().Match(path).SeriesName);
    }
}

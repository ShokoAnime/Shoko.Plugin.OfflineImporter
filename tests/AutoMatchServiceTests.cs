using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Release;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Configuration;
using Shoko.Plugin.OfflineImporter.Services;
using Xunit;

namespace Shoko.Plugin.OfflineImporter.Tests;

public class AutoMatchServiceTests
{
    private readonly Mock<IMetadataService> _metadata = new();

    private OfflineAutoMatchService CreateService()
    {
        return new(NullLogger<OfflineAutoMatchService>.Instance, _metadata.Object);
    }

    private static IVideoFile File(int managedFolderID, string relativePath)
        => Mock.Of<IVideoFile>(file => file.ManagedFolderID == managedFolderID && file.RelativePath == relativePath);

    private static ReleaseInfo Release(ReleaseGroup? group = null, params MetadataGuid[] episodes) => new()
    {
        Group = group,
        CrossReferences = episodes.Select(episode => new ReleaseVideoCrossReference { IDs = [episode] }).ToList(),
    };

    private static MetadataGuid AnidbEpisode(int id)
        => new(MetadataSource.AniDB, MetadataEntityType.Episode, id.ToString());

    private void AddAnidbEpisode(int id, EpisodeType type, int number, string title = "Episode")
        => _metadata.Setup(m => m.GetEntry<IAnidbEpisode>(AnidbEpisode(id))).Returns(Mock.Of<IAnidbEpisode>(episode =>
            episode.Type == type && episode.EpisodeNumber == number && episode.DefaultTitle == Mock.Of<ITitle>(t => t.Value == title)));

    private static AutoMatchRule Allow(string name = "allow") => new() { Name = name, MatchType = AutoMatchRuleMatchType.Allow };

    private static AutoMatchRule Deny(string name = "deny") => new() { Name = name, MatchType = AutoMatchRuleMatchType.Deny };

    private static readonly IReadOnlyList<IVideoFile> _files = [File(2, "/Seasonal/Show/Show - 01.mkv")];

    [Fact]
    public void Evaluate_AllowsAndDeniesByTheFirstRuleThatApplies()
    {
        var service = CreateService();
        var release = Release();

        var first = Allow();
        Assert.Equal(new() { IsAllowed = true, Rule = first }, service.Evaluate([first, Deny()], _files, release));
        var deny = Deny();
        Assert.Equal(new() { IsAllowed = false, Rule = deny }, service.Evaluate([deny, Allow()], _files, release));

        var skipped = Deny("elsewhere");
        skipped.LocationRules.Add(new() { ManagedFolderID = 9 });
        var allow = Allow();
        Assert.Equal(new() { IsAllowed = true, Rule = allow }, service.Evaluate([skipped, allow], _files, release));
    }

    [Fact]
    public void Evaluate_DeniesWhenNoRuleApplies()
    {
        var rule = Allow();
        rule.LocationRules.Add(new() { ManagedFolderID = 9 });

        Assert.Equal(new() { IsAllowed = false, Rule = null }, CreateService().Evaluate([rule], _files, Release()));
        Assert.Equal(new() { IsAllowed = false, Rule = null }, CreateService().Evaluate([], _files, Release()));
    }

    [Theory]
    [InlineData(2, "", true)]
    [InlineData(2, "Seasonal", true)]
    [InlineData(2, "/Seasonal/Show", true)]
    [InlineData(2, "/Archive", false)]
    [InlineData(3, "", false)]
    public void Evaluate_ChecksTheLocation(int managedFolderID, string relativePath, bool applies)
    {
        var rule = Allow();
        rule.LocationRules.Add(new() { ManagedFolderID = managedFolderID, RelativePath = relativePath });

        Assert.Equal(applies, CreateService().Evaluate([rule], _files, Release()).IsAllowed);
    }

    [Fact]
    public void Evaluate_ChecksTheReleaseGroup()
    {
        var service = CreateService();
        var anidb = Allow();
        anidb.GroupRules.Add(new() { RuleType = AutoMatchReleaseGroupRuleType.AniDB, AnidbID = 123 });
        var custom = Allow();
        custom.GroupRules.Add(new() { RuleType = AutoMatchReleaseGroupRuleType.Custom, GroupName = "grp", GroupSource = "Offline" });

        Assert.True(service.Evaluate([anidb], _files, Release(new() { ID = "123", Name = "Group", Source = "AniDB" })).IsAllowed);
        Assert.False(service.Evaluate([anidb], _files, Release(new() { ID = "124", Name = "Group", Source = "AniDB" })).IsAllowed);
        Assert.False(service.Evaluate([anidb], _files, Release()).IsAllowed);
        Assert.True(service.Evaluate([custom], _files, Release(new() { ID = "GRP", Name = "The Group", ShortName = "GRP", Source = "Offline" })).IsAllowed);
        Assert.False(service.Evaluate([custom], _files, Release(new() { ID = "GRP", Name = "The Group", ShortName = "GRP", Source = "AniDB" })).IsAllowed);
    }

    [Theory]
    [InlineData("1-12", true)]
    [InlineData("E5", true)]
    [InlineData("!5 *", false)]
    [InlineData("13-24", false)]
    [InlineData("S", false)]
    public void Evaluate_ChecksAnidbEpisodeRanges(string ranges, bool applies)
    {
        AddAnidbEpisode(1001, EpisodeType.Episode, 5);
        var rule = Allow();
        rule.EpisodeRanges = ranges;

        Assert.Equal(applies, CreateService().Evaluate([rule], _files, Release(null, AnidbEpisode(1001))).IsAllowed);
    }

    [Theory]
    [InlineData("OP2", true)]
    [InlineData("OP1", false)]
    [InlineData("ED", false)]
    [InlineData("C", true)]
    public void Evaluate_ChecksAnidbCredits(string ranges, bool applies)
    {
        AddAnidbEpisode(1002, EpisodeType.Credits, 2, "Opening 2");
        var rule = Allow();
        rule.EpisodeRanges = ranges;

        Assert.Equal(applies, CreateService().Evaluate([rule], _files, Release(null, AnidbEpisode(1002))).IsAllowed);
    }

    [Theory]
    [InlineData("1-12", true)]
    [InlineData("4-12", false)]
    public void Evaluate_ChecksTheEpisodeRangesOfOtherSources(string ranges, bool applies)
    {
        var tmdbEpisode = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "77");
        _metadata.Setup(m => m.GetEntry<IEpisode>(tmdbEpisode)).Returns(Mock.Of<IEpisode>(episode => episode.Type == EpisodeType.Episode && episode.EpisodeNumber == 3));
        var rule = Allow();
        rule.EpisodeRanges = ranges;

        Assert.Equal(applies, CreateService().Evaluate([rule], _files, Release(null, tmdbEpisode)).IsAllowed);
    }

    [Fact]
    public void Evaluate_DeniesAnEpisodeItCannotRead()
    {
        var rule = Allow();
        rule.EpisodeRanges = "*";

        Assert.False(CreateService().Evaluate([rule], _files, Release(null, AnidbEpisode(9999))).IsAllowed);
    }

    [Fact]
    public void Validation_NamesEachAutoMatchRuleProblem()
    {
        var unnamed = new AutoMatchRule { EpisodeRanges = "???" };
        var broken = Allow("broken");
        broken.LocationRules.Add(new());
        broken.GroupRules.Add(new() { RuleType = AutoMatchReleaseGroupRuleType.AniDB });
        broken.GroupRules.Add(new() { RuleType = AutoMatchReleaseGroupRuleType.Custom });
        broken.EpisodeRangeRules = [new() { RuleType = AutoMatchEpisodeRangeRuleType.Range, Start = 5, End = 2 }];

        var errors = OfflineImporterConfiguration.Validate(new() { AutoMatchRules = [unnamed, broken, Allow()] }, null!, null!);

        Assert.Equal(
            ["AutoMatchRules[0].EpisodeRanges", "AutoMatchRules[0].Name", "AutoMatchRules[1].EpisodeRangeRules[0].End", "AutoMatchRules[1].GroupRules[0].AnidbID", "AutoMatchRules[1].GroupRules[1]", "AutoMatchRules[1].LocationRules[0].ManagedFolderID"],
            errors.Keys.Order()
        );
    }
}

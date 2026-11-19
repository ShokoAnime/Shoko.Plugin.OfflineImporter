using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Plugin;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Configuration;
using Shoko.Plugin.OfflineImporter.Services;
using Xunit;

namespace Shoko.Plugin.OfflineImporter.Tests;

public class ReleaseGroupServiceTests
{
    // The group list is cached for the whole process, so every test reads
    // the same one, written fresh so it is not downloaded.
    private static readonly Lazy<string> _dataPath = new(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), $"offline-importer-tests-{Environment.ProcessId}");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "groups.json"), """
            [
              { "id": 1, "name": "Alpha Subs", "tag": "Alpha", "createdAt": 100 },
              { "id": 2, "name": "Alpha", "tag": "ALP", "createdAt": 200 },
              { "id": 3, "name": "Beta Fansubs", "tag": "Beta", "createdAt": 300 },
              { "id": 4, "name": "Other Beta", "tag": "Beta", "createdAt": 50 },
              { "id": 5, "name": "Gamma Raws", "tag": "GR", "createdAt": 400 },
              { "id": 6, "name": "Super Gamma", "tag": "SG", "createdAt": 500 },
              { "id": 7, "name": "Gama", "tag": "GM", "createdAt": 600 }
            ]
            """);
        return path;
    });

    private OfflineImporterConfiguration _current = new();

    private int _saves;

    private OfflineReleaseGroupService CreateService(OfflineImporterConfiguration? configuration = null)
    {
        _current = configuration ?? new();
        var service = new Mock<IConfigurationService>();
        service.Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(() => _current);
        service.Setup(s => s.Save(It.IsAny<OfflineImporterConfiguration>()))
            .Callback<OfflineImporterConfiguration>(saved =>
            {
                _current = saved;
                _saves++;
                service.Raise(s => s.Saved += null, new ConfigurationSavedEventArgs { ConfigurationInfo = null! });
            })
            .Returns(true);
        var paths = Mock.Of<IApplicationPaths>(p => p.DataPath == _dataPath.Value);
        return new(new ConfigurationProvider<OfflineImporterConfiguration>(service.Object), paths, new FakeFuzzySearchService());
    }

    private static MetadataGuid Group(int id)
        => MetadataGuid.Parse($"anidb://release-group/{id}");

    private static string[] IDs(IEnumerable<Shoko.Abstractions.Video.Release.ReleaseGroup> groups)
        => groups.Select(group => group.ID).ToArray();

    [Fact]
    public void GetManyReleaseGroupsByName_ListsEveryGroupWithTheNameOrShortName()
    {
        var service = CreateService();

        Assert.Equal(["4", "3"], IDs(service.GetManyReleaseGroupsByName("Beta")));
        Assert.Equal(["1", "2"], IDs(service.GetManyReleaseGroupsByName("Alpha")));
        Assert.Empty(service.GetManyReleaseGroupsByName("beta"));
        Assert.Equal("4", service.GetReleaseGroupByName("Beta")?.ID);
        Assert.Null(service.GetReleaseGroupByName("Gamma"));
    }

    [Fact]
    public void PreferredGroup_ComesFirst()
    {
        var service = CreateService(new() { PreferredReleaseGroups = new() { ["Beta"] = 3 } });

        Assert.Equal(["3", "4"], IDs(service.GetManyReleaseGroupsByName("Beta")));
        Assert.Equal("3", service.GetReleaseGroupByName("Beta")?.ID);
    }

    [Fact]
    public void ExcludedGroup_IsLeftOut_UnlessTheFlagIsOff()
    {
        var service = CreateService(new() { ExcludedReleaseGroups = [Group(4)], PreferredReleaseGroups = new() { ["Alpha"] = 2 } });

        Assert.Equal(["3"], IDs(service.GetManyReleaseGroupsByName("Beta")));
        Assert.Equal("3", service.GetReleaseGroupByName("Beta")?.ID);
        Assert.Equal(["4", "3"], IDs(service.GetManyReleaseGroupsByName("Beta", applyPreferencesAndExclusions: false)));
        Assert.Equal("4", service.GetReleaseGroupByName("Beta", applyPreferencesAndExclusions: false)?.ID);
        Assert.Equal("1", service.GetReleaseGroupByName("Alpha", applyPreferencesAndExclusions: false)?.ID);
    }

    [Fact]
    public void Lookups_WorkWithoutTheImportersMapping()
    {
        var service = CreateService(new() { MapAsAnidbGroupIfPossible = false });

        Assert.Equal("Other Beta", service.GetReleaseGroupByName("Beta")?.Name);
        Assert.Equal("Alpha", service.GetReleaseGroupByID(2)?.Name);
        Assert.Null(service.GetReleaseGroupByID(9));
    }

    [Fact]
    public void GetAllReleaseGroups_ListsEveryGroupByName()
    {
        var service = CreateService(new() { ExcludedReleaseGroups = [Group(4)] });

        Assert.Equal(["2", "1", "3", "7", "5", "4", "6"], IDs(service.GetAllReleaseGroups()));
        Assert.Equal(["2", "1", "3", "7", "5", "6"], IDs(service.GetAllReleaseGroups(applyExclusions: true)));
    }

    [Fact]
    public void GetManyReleaseGroupsBySearch_RanksExactThenPartialThenTypo()
    {
        var service = CreateService(new() { ExcludedReleaseGroups = [Group(6)] });

        // "Gamma Raws" starts with it, "Super Gamma" has it later, "Gama" is a typo.
        Assert.Equal(["5", "6", "7"], IDs(service.GetManyReleaseGroupsBySearch("gamma")));
        Assert.Equal(["5", "7"], IDs(service.GetManyReleaseGroupsBySearch("Gamma", applyExclusions: true)));
        Assert.Equal(["3", "4"], IDs(service.GetManyReleaseGroupsBySearch("beta")));
        Assert.Empty(service.GetManyReleaseGroupsBySearch("delta"));
    }

    [Fact]
    public void ReleaseGroupRoutes_PageAndKeepTheirDefaults()
    {
        var controller = new API.ReleaseGroupController(CreateService(new() { ExcludedReleaseGroups = [Group(4)], PreferredReleaseGroups = new() { ["Beta"] = 3 } }));

        var firstPage = controller.GetReleaseGroups(pageSize: 3).Value!;
        Assert.Equal(7, firstPage.Total);
        Assert.Equal(["2", "1", "3"], IDs(firstPage.List));
        Assert.Equal(["6"], IDs(controller.GetReleaseGroups(pageSize: 3, page: 3).Value!.List));
        Assert.Empty(controller.GetReleaseGroups(pageSize: 3, page: 4).Value!.List);
        Assert.Equal(7, controller.GetReleaseGroups(pageSize: 0).Value!.List.Count);
        Assert.Equal(6, controller.GetReleaseGroups(applyExclusions: true).Value!.Total);
        Assert.Equal(["5", "6", "7"], IDs(controller.GetReleaseGroups(search: "gamma").Value!.List));

        var byName = (Microsoft.AspNetCore.Mvc.OkObjectResult)controller.GetManyReleaseGroupsByName("Beta").Result!;
        Assert.Equal(["3"], IDs((IEnumerable<Shoko.Abstractions.Video.Release.ReleaseGroup>)byName.Value!));

        var shared = controller.GetAllSharedReleaseGroupNames(pageSize: 1, page: 2).Value!;
        Assert.Equal(2, shared.Total);
        Assert.Equal("Beta", Assert.Single(shared.List).Name);
    }

    [Fact]
    public void GetAllSharedReleaseGroupNames_ListsTheNamesWithSeveralGroups()
    {
        var shared = CreateService().GetAllSharedReleaseGroupNames();

        Assert.Equal(["Alpha", "Beta"], shared.Select(name => name.Name));
        Assert.Equal(["4", "3"], IDs(shared.Single(name => name.Name is "Beta").Groups));
    }

    [Fact]
    public void Validation_NamesEachReleaseGroupSettingProblem()
    {
        var errors = OfflineImporterConfiguration.Validate(new()
        {
            ExcludedReleaseGroups = [MetadataGuid.Parse("anidb://anime/1"), Group(3)],
            PreferredReleaseGroups = new() { ["Beta"] = 3, ["Alpha"] = 4, [" "] = 1 },
        }, null!, null!);

        Assert.Equal(["ExcludedReleaseGroups[0]", "PreferredReleaseGroups[ ]", "PreferredReleaseGroups[Beta]"], errors.Keys.Order());
    }
}

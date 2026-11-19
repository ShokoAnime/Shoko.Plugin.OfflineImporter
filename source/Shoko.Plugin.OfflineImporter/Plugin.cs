using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Services;
using Shoko.Plugin.OfflineImporter.Configuration;
using Shoko.Plugin.OfflineImporter.Services;

namespace Shoko.Plugin.OfflineImporter;

/// <summary>
/// Plugin responsible for importing releases based on file names.
/// </summary>
public partial class Plugin : IPlugin<OfflineImporterConfiguration>, IPluginServiceRegistration
{
    /// <inheritdoc/>
    public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddSingleton<OfflineMatchingService>();
        serviceCollection.AddSingleton<IOfflineMatchingService>(provider => provider.GetRequiredService<OfflineMatchingService>());
        serviceCollection.AddSingleton<OfflineReleaseGroupService>();
        serviceCollection.AddSingleton<IOfflineReleaseGroupService>(provider => provider.GetRequiredService<OfflineReleaseGroupService>());
        serviceCollection.AddSingleton<OfflineAutoMatchService>();
        serviceCollection.AddSingleton<IOfflineAutoMatchService>(provider => provider.GetRequiredService<OfflineAutoMatchService>());
    }
}

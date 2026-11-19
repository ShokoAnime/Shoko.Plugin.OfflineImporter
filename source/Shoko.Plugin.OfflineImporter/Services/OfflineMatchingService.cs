using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video;
using Shoko.Plugin.OfflineImporter.Abstractions.Configuration;
using Shoko.Plugin.OfflineImporter.Abstractions.Models;
using Shoko.Plugin.OfflineImporter.Abstractions.Services;
using Shoko.Plugin.OfflineImporter.Configuration;

namespace Shoko.Plugin.OfflineImporter.Services;

/// <summary>
/// Runs the importer's parsing rules, for the importer itself and for
/// anyone resolving <see cref="IOfflineMatchingService"/>, compiled again
/// whenever the settings are saved.
/// </summary>
public sealed class OfflineMatchingService : IOfflineMatchingService, IDisposable
{
    private readonly ConfigurationProvider<OfflineImporterConfiguration> _configurationProvider;

    private IReadOnlyList<FileNameParser.CompiledRule> _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineMatchingService"/> class.
    /// </summary>
    /// <param name="configurationProvider">The configuration provider.</param>
    public OfflineMatchingService(ConfigurationProvider<OfflineImporterConfiguration> configurationProvider)
    {
        _configurationProvider = configurationProvider;
        var configuration = configurationProvider.Load();
        _rules = Compile(configuration);
        _configurationProvider.Saved += OnConfigurationSaved;
    }

    /// <inheritdoc/>
    public void Dispose()
        => _configurationProvider.Saved -= OnConfigurationSaved;

    private void OnConfigurationSaved(object? sender, ConfigurationSavedEventArgs<OfflineImporterConfiguration> e)
        => _rules = Compile(e.Configuration);

    private static List<FileNameParser.CompiledRule> Compile(OfflineImporterConfiguration configuration)
        => configuration.ParseRules.Select(rule => rule.ToMatchRule()).ToList();

    /// <inheritdoc/>
    public ParsedFileResult Match(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(path, path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries), out _);
    }

    /// <inheritdoc/>
    public ParsedFileResult Match(IManagedFolder managedFolder, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(managedFolder);
        ArgumentNullException.ThrowIfNull(relativePath);
        var fullPath = Path.Join(managedFolder.Path, relativePath.TrimStart('/', '\\'));
        return Parse(fullPath, relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries), out _);
    }

    /// <summary>
    /// Runs the user's current rules on a path and reads the ID tags in it.
    /// </summary>
    /// <param name="filePath">The path the rules see.</param>
    /// <param name="segments">
    /// The names the ID tags are read from, the file name last: those within
    /// the managed folder, or the whole path's.
    /// </param>
    /// <param name="tags">The ID tags, as written.</param>
    /// <returns>The result, its IDs included.</returns>
    internal ParsedFileResult Parse(string filePath, IReadOnlyList<string> segments, out IReadOnlyList<FileNameIds.Tag> tags)
    {
        var result = FileNameParser.Match(filePath, _rules);
        var forcedTags = result.Success ? _rules.FirstOrDefault(rule => rule.Name == result.RuleName)?.ForcedTags ?? [] : [];
        var readTags = forcedTags.Concat(FileNameIds.ReadTags(segments)).ToList();
        result.IDs = FileNameIds.ToIDs(readTags);
        tags = readTags;
        return result;
    }
}

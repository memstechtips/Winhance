using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class ConfigFileWriter : IConfigFileWriter
{
    private readonly ICatalogSettingsRegistry _registry;
    private readonly IFileSystemService _files;
    private readonly ILogService _log;

    public ConfigFileWriter(ICatalogSettingsRegistry registry, IFileSystemService files, ILogService log)
    {
        _registry = registry;
        _files = files;
        _log = log;
    }

    public async Task WriteAsync(SelectionSet set, CatalogScope scope, string outputPath)
    {
        await _registry.InitializeAsync().ConfigureAwait(false);
        var file = ConfigFileMapper.ToFile(set, _registry.GetAll(scope));
        var json = JsonSerializer.Serialize(file, ConfigFileConstants.JsonOptions);
        await _files.WriteAllTextAsync(outputPath, json).ConfigureAwait(false);
        _log.Log(LogLevel.Info, $"Configuration written to {outputPath} ({set.Settings.Count} settings, {set.WindowsApps.Count + set.ExternalApps.Count} apps)");
    }
}

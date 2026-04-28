using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppIconResolver : IAppIconResolver
{
    private const string CacheSubDir = @"Winhance\IconCache";
    private static readonly Size LogoSize = new(48, 48);

    private readonly IAppxIconSource _iconSource;
    private readonly ILogService _logService;
    private readonly string _cacheRoot;

    /// <summary>Production constructor — uses %LOCALAPPDATA%\Winhance\IconCache.</summary>
    public AppIconResolver(IAppxIconSource iconSource, ILogService logService)
        : this(iconSource, logService, DefaultCacheRoot()) { }

    /// <summary>Test constructor — accepts a custom cache root for unit tests.</summary>
    internal AppIconResolver(IAppxIconSource iconSource, ILogService logService, string cacheRoot)
    {
        _iconSource = iconSource;
        _logService = logService;
        _cacheRoot = cacheRoot;
    }

    private static string DefaultCacheRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CacheSubDir);

    public async Task ResolveBatchAsync(IEnumerable<ItemDefinition> definitions, CancellationToken ct = default)
    {
        try
        {
            var candidates = definitions
                .Where(d => d.AppxPackageName?.Length > 0 && d.IsInstalled)
                .ToList();
            if (candidates.Count == 0)
                return;

            if (!EnsureCacheDir())
                return;

            var installedMap = await _iconSource.GetInstalledPackageMapAsync(ct).ConfigureAwait(false);
            if (installedMap.Count == 0)
                return;

            foreach (var def in candidates)
            {
                if (ct.IsCancellationRequested) return;

                try
                {
                    await ResolveOneAsync(def, installedMap, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("Icon resolution batch failed", ex);
        }
    }

    private async Task ResolveOneAsync(
        ItemDefinition def,
        IReadOnlyDictionary<string, string> installedMap,
        CancellationToken ct)
    {
        var packageName = def.AppxPackageName![0];
        if (!installedMap.TryGetValue(packageName, out var fullName))
            return;

        var cachePath = Path.Combine(_cacheRoot, fullName + ".png");
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return;
        }

        await using var stream = await _iconSource.GetLogoStreamAsync(fullName, LogoSize, ct).ConfigureAwait(false);
        if (stream is null)
            return;

        var tmpPath = cachePath + ".tmp";
        await using (var fileStream = File.Create(tmpPath))
        {
            await stream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
        }
        File.Move(tmpPath, cachePath, overwrite: true);

        PruneOldVersions(packageName, fullName);

        def.IconPath = cachePath;
    }

    private void PruneOldVersions(string packageName, string keepFullName)
    {
        try
        {
            var pattern = packageName + "_*.png";
            var keepFile = keepFullName + ".png";
            foreach (var path in Directory.EnumerateFiles(_cacheRoot, pattern))
            {
                if (!string.Equals(Path.GetFileName(path), keepFile, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(path); }
                    catch (Exception ex) { _logService.LogWarning($"Could not prune old icon {path}: {ex.Message}"); }
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not enumerate cache for prune: {ex.Message}");
        }
    }

    private bool EnsureCacheDir()
    {
        try
        {
            Directory.CreateDirectory(_cacheRoot);
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Icon cache directory unavailable ({_cacheRoot}): {ex.Message}");
            return false;
        }
    }
}

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
    private const string StoreCachePrefix = "MsStore_";
    // Concurrency limit for Store CDN requests. Each unresolved-with-MsStoreId
    // package costs two HTTP round-trips (catalog metadata + image download);
    // running them parallel keeps cold-cache load times bounded.
    private const int StoreFetchConcurrency = 5;

    // Logo extraction size, in pixels. Microsoft's GetLogo picks the closest
    // available asset/scale to this hint; 96 reliably returns the high-DPI
    // (200%) variant of Square44x44Logo (~88px native), which renders crisply
    // when displayed at 40 logical pixels on a 200% DPI screen.
    private static readonly Size LogoSize = new(96, 96);

    // Suffix appended to the cache filename so the cache key encodes the
    // extraction size. Bumping LogoSize requires bumping this suffix; that
    // invalidates older cache files (PruneOldVersions cleans them up the
    // next time their package version changes).
    private const string CacheFileSuffix = ".96.png";

    private readonly IAppxIconSource _appxSource;
    private readonly IStoreIconSource? _storeSource;
    private readonly ILogService _logService;
    private readonly string _cacheRoot;

    /// <summary>Production constructor — uses %LOCALAPPDATA%\Winhance\IconCache.</summary>
    public AppIconResolver(IAppxIconSource appxSource, ILogService logService, IStoreIconSource? storeSource = null)
        : this(appxSource, logService, DefaultCacheRoot(), storeSource) { }

    /// <summary>Test constructor — accepts a custom cache root.</summary>
    internal AppIconResolver(
        IAppxIconSource appxSource,
        ILogService logService,
        string cacheRoot,
        IStoreIconSource? storeSource = null)
    {
        _appxSource = appxSource;
        _logService = logService;
        _cacheRoot = cacheRoot;
        _storeSource = storeSource;
    }

    private static string DefaultCacheRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CacheSubDir);

    public async Task ResolveBatchAsync(IEnumerable<ItemDefinition> definitions, CancellationToken ct = default)
    {
        try
        {
            // Any entry with a routable identity gets a try. AppX-named entries go
            // through Layer 1 (local enumeration); MsStoreId-bearing entries that
            // didn't resolve via Layer 1 fall through to Layer 2 (Store CDN).
            var candidates = definitions
                .Where(d => (d.AppxPackageName?.Length > 0) || !string.IsNullOrEmpty(d.MsStoreId))
                .ToList();
            if (candidates.Count == 0)
                return;

            if (!EnsureCacheDir())
                return;

            // Layer 1: AppX (current user / all users / provisioned).
            var installedMap = await _appxSource.GetInstalledPackageMapAsync(ct).ConfigureAwait(false);

            foreach (var def in candidates)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    await TryResolveFromAppxAsync(def, installedMap, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"AppX icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                }
            }

            // Layer 2: Microsoft Store CDN, for entries with an MsStoreId where
            // Layer 1 didn't yield a result. Parallelized with a small concurrency
            // limit so cold-cache load doesn't pay a full sequential network cost.
            if (_storeSource is not null)
            {
                var storeCandidates = candidates
                    .Where(d => d.IconPath is null && !string.IsNullOrEmpty(d.MsStoreId))
                    .ToList();

                if (storeCandidates.Count > 0)
                {
                    using var sem = new SemaphoreSlim(StoreFetchConcurrency);
                    var tasks = storeCandidates.Select(async def =>
                    {
                        await sem.WaitAsync(ct).ConfigureAwait(false);
                        try
                        {
                            await TryResolveFromStoreAsync(def, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logService.LogWarning($"Store icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                        }
                        finally
                        {
                            sem.Release();
                        }
                    });
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("Icon resolution batch failed", ex);
        }
    }

    private async Task TryResolveFromAppxAsync(
        ItemDefinition def,
        IReadOnlyDictionary<string, string> installedMap,
        CancellationToken ct)
    {
        var packageName = def.AppxPackageName?.Length > 0 ? def.AppxPackageName[0] : null;
        if (packageName is null) return;
        if (!installedMap.TryGetValue(packageName, out var fullName)) return;

        var cachePath = Path.Combine(_cacheRoot, fullName + CacheFileSuffix);
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return;
        }

        await using var stream = await _appxSource.GetLogoStreamAsync(fullName, LogoSize, ct).ConfigureAwait(false);
        if (stream is null)
            return;

        await WriteStreamToCacheAsync(stream, cachePath, ct).ConfigureAwait(false);
        PruneOldVersions(packageName, fullName);
        def.IconPath = cachePath;
    }

    private async Task TryResolveFromStoreAsync(ItemDefinition def, CancellationToken ct)
    {
        var msStoreId = def.MsStoreId!;
        var cachePath = Path.Combine(_cacheRoot, StoreCachePrefix + msStoreId + CacheFileSuffix);
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return;
        }

        await using var stream = await _storeSource!.GetIconStreamAsync(msStoreId, ct).ConfigureAwait(false);
        if (stream is null)
            return;

        await WriteStreamToCacheAsync(stream, cachePath, ct).ConfigureAwait(false);
        def.IconPath = cachePath;
    }

    private static async Task WriteStreamToCacheAsync(Stream source, string cachePath, CancellationToken ct)
    {
        var tmpPath = cachePath + ".tmp";
        await using (var fileStream = File.Create(tmpPath))
        {
            await source.CopyToAsync(fileStream, ct).ConfigureAwait(false);
        }
        File.Move(tmpPath, cachePath, overwrite: true);
    }

    private void PruneOldVersions(string packageName, string keepFullName)
    {
        try
        {
            // Pattern matches both old-format (.png) and new-format (.96.png)
            // files for this package, so this prune step also cleans up cache
            // files left over from prior CacheFileSuffix versions.
            var pattern = packageName + "_*.png";
            var keepFile = keepFullName + CacheFileSuffix;
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

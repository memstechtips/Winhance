using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
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
    // extraction parameters. Bumping LogoSize, AlphaTrimThreshold, or the
    // post-process pipeline requires bumping this suffix; that invalidates
    // older cache files (PruneOldVersions cleans them up the next time
    // their package version changes). The "-trim" segment indicates icons
    // are cropped to their alpha bounding box so they display at uniform
    // visible size regardless of the source's original transparent-padding
    // convention. The version digit (-trim, -trim2, ...) bumps every time
    // we change the trim behavior so cached PNGs from older code re-extract.
    private const string CacheFileSuffix = ".96-trim2.png";

    // ====== TRIM TUNING KNOB ======
    // Pixels with alpha at or below this threshold are treated as transparent
    // when computing the trim bounding box. The Square44x44Logo PNGs returned
    // by DisplayInfo.GetLogo for installed AppX packages typically have
    // antialiased soft-edge halos around the visible art (alpha ~5-30) which
    // a low threshold preserves, expanding the bbox and making the cached
    // icon appear smaller than equivalent Store-CDN icons (which are
    // hard-edge cropped). Higher threshold → tighter bbox → larger visible
    // icon at fixed display size, at the cost of clipping legitimately
    // translucent icon parts. 32 is a reasonable middle ground; 64 if you
    // want maximum tightening; 8 (original) for maximum softness preservation.
    // Bump CacheFileSuffix above whenever you change this value.
    private const byte AlphaTrimThreshold = 32;

    private readonly IAppxIconSource _appxSource;
    private readonly IStoreIconSource? _storeSource;
    private readonly ILogService _logService;
    private readonly string _cacheRoot;

    /// <summary>Production constructor — uses %LOCALAPPDATA%\Winhance\IconCache.</summary>
    public AppIconResolver(IAppxIconSource appxSource, ILogService logService, IStoreIconSource storeSource)
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

            int appxResolved = 0;
            foreach (var def in candidates)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    if (await TryResolveFromAppxAsync(def, installedMap, ct).ConfigureAwait(false))
                        appxResolved++;
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"AppX icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                }
            }

            // Layer 2: Microsoft Store CDN, for entries with an MsStoreId where
            // Layer 1 didn't yield a result. Parallelized with a small concurrency
            // limit so cold-cache load doesn't pay a full sequential network cost.
            int storeAttempted = 0;
            int storeResolved = 0;
            var storeCandidates = candidates
                .Where(d => d.IconPath is null && !string.IsNullOrEmpty(d.MsStoreId))
                .ToList();

            if (storeCandidates.Count > 0)
            {
                storeAttempted = storeCandidates.Count;
                using var sem = new SemaphoreSlim(StoreFetchConcurrency);
                var tasks = storeCandidates.Select(async def =>
                {
                    await sem.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        if (await TryResolveFromStoreAsync(def, ct).ConfigureAwait(false))
                            Interlocked.Increment(ref storeResolved);
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

            int unresolved = candidates.Count - appxResolved - storeResolved;
            _logService.LogInformation(
                $"AppIconResolver: {candidates.Count} candidates → " +
                $"{appxResolved} via AppX, {storeResolved}/{storeAttempted} via Store, {unresolved} unresolved");
        }
        catch (Exception ex)
        {
            _logService.LogError("Icon resolution batch failed", ex);
        }
    }

    private async Task<bool> TryResolveFromAppxAsync(
        ItemDefinition def,
        IReadOnlyDictionary<string, string> installedMap,
        CancellationToken ct)
    {
        var packageName = def.AppxPackageName?.Length > 0 ? def.AppxPackageName[0] : null;
        if (packageName is null) return false;
        if (!installedMap.TryGetValue(packageName, out var fullName)) return false;

        var cachePath = Path.Combine(_cacheRoot, fullName + CacheFileSuffix);
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return true;
        }

        await using var stream = await _appxSource.GetLogoStreamAsync(fullName, LogoSize, ct).ConfigureAwait(false);
        if (stream is null)
            return false;

        await WriteStreamToCacheAsync(stream, cachePath, ct).ConfigureAwait(false);
        PruneOldVersions(packageName, fullName);
        def.IconPath = cachePath;
        return true;
    }

    private async Task<bool> TryResolveFromStoreAsync(ItemDefinition def, CancellationToken ct)
    {
        var msStoreId = def.MsStoreId!;
        var cachePath = Path.Combine(_cacheRoot, StoreCachePrefix + msStoreId + CacheFileSuffix);
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return true;
        }

        await using var stream = await _storeSource!.GetIconStreamAsync(msStoreId, ct).ConfigureAwait(false);
        if (stream is null)
            return false;

        await WriteStreamToCacheAsync(stream, cachePath, ct).ConfigureAwait(false);
        def.IconPath = cachePath;
        return true;
    }

    private async Task WriteStreamToCacheAsync(Stream source, string cachePath, CancellationToken ct)
    {
        var tmpPath = cachePath + ".tmp";

        var sourceBytes = await ReadAllBytesAsync(source, ct).ConfigureAwait(false);
        var bytesToWrite = await TryTrimTransparentBordersAsync(sourceBytes, ct).ConfigureAwait(false)
                          ?? sourceBytes;

        await File.WriteAllBytesAsync(tmpPath, bytesToWrite, ct).ConfigureAwait(false);
        File.Move(tmpPath, cachePath, overwrite: true);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        if (stream is MemoryStream ms && ms.Position == 0)
            return ms.ToArray();

        if (stream.CanSeek) stream.Position = 0;
        using var collector = new MemoryStream();
        await stream.CopyToAsync(collector, ct).ConfigureAwait(false);
        return collector.ToArray();
    }

    /// <summary>
    /// Crops the source PNG/JPG to the bounding box of its non-transparent
    /// pixels and re-encodes as PNG. Normalizes icons across AppX packages
    /// that follow Microsoft's tile-with-padding convention vs. those that
    /// fill the canvas (e.g. Edge), so all cached icons display at uniform
    /// visible size when rendered at a fixed Image control size.
    /// Returns null on any decoder/encoder failure — caller falls back to
    /// the untrimmed source bytes.
    /// </summary>
    private async Task<byte[]?> TryTrimTransparentBordersAsync(byte[] source, CancellationToken ct)
    {
        try
        {
            using var inStream = new InMemoryRandomAccessStream();
            await inStream.WriteAsync(source.AsBuffer());
            inStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(inStream);
            var swBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            int width = (int)swBitmap.PixelWidth;
            int height = (int)swBitmap.PixelHeight;
            if (width <= 0 || height <= 0) return null;

            var pixelBuffer = new Windows.Storage.Streams.Buffer((uint)(width * height * 4));
            swBitmap.CopyToBuffer(pixelBuffer);
            var pixels = pixelBuffer.ToArray();

            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    byte alpha = pixels[rowStart + x * 4 + 3];
                    if (alpha > AlphaTrimThreshold)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            // Fully transparent input — nothing to trim, return original bytes.
            if (maxX < minX || maxY < minY) return null;

            uint cropX = (uint)minX;
            uint cropY = (uint)minY;
            uint cropW = (uint)(maxX - minX + 1);
            uint cropH = (uint)(maxY - minY + 1);

            // Source has no transparent border — re-encoding wouldn't change
            // anything visible, so skip the round-trip.
            if (cropX == 0 && cropY == 0 && cropW == width && cropH == height)
                return null;

            using var outStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
            encoder.SetSoftwareBitmap(swBitmap);
            encoder.BitmapTransform.Bounds = new BitmapBounds
            {
                X = cropX,
                Y = cropY,
                Width = cropW,
                Height = cropH,
            };
            await encoder.FlushAsync();

            outStream.Seek(0);
            using var managedStream = outStream.AsStreamForRead();
            using var resultMs = new MemoryStream();
            await managedStream.CopyToAsync(resultMs, ct).ConfigureAwait(false);
            return resultMs.ToArray();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Icon trim failed; saving raw bytes instead: {ex.Message}");
            return null;
        }
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

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
    private const string BinaryCachePrefix = "Bin_";
    private const string WinGetCachePrefix = "WinGet_";
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
    private readonly IBinaryIconSource? _binarySource;
    private readonly IWinGetIconSource? _winGetSource;
    private readonly ILogService _logService;
    private readonly string _cacheRoot;

    /// <summary>Production constructor — uses %LOCALAPPDATA%\Winhance\IconCache.</summary>
    public AppIconResolver(
        IAppxIconSource appxSource,
        ILogService logService,
        IStoreIconSource storeSource,
        IBinaryIconSource binarySource,
        IWinGetIconSource winGetSource)
        : this(appxSource, logService, DefaultCacheRoot(), storeSource, binarySource, winGetSource) { }

    /// <summary>Test constructor — accepts a custom cache root and optional sources.</summary>
    internal AppIconResolver(
        IAppxIconSource appxSource,
        ILogService logService,
        string cacheRoot,
        IStoreIconSource? storeSource = null,
        IBinaryIconSource? binarySource = null,
        IWinGetIconSource? winGetSource = null)
    {
        _appxSource = appxSource;
        _logService = logService;
        _cacheRoot = cacheRoot;
        _storeSource = storeSource;
        _binarySource = binarySource;
        _winGetSource = winGetSource;
    }

    private static string DefaultCacheRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CacheSubDir);

    public async Task ResolveBatchAsync(IEnumerable<ItemDefinition> definitions, CancellationToken ct = default)
    {
        try
        {
            // Any entry with a routable identity gets a try. AppX-named entries go
            // through Layer 1a (local enumeration); entries with InstalledBinaryHint
            // go through Layer 1b; MsStoreId / WinGetPackageId entries that didn't
            // resolve locally fall through to Layer 2 (online sources).
            var candidates = definitions
                .Where(d => (d.AppxPackageName?.Length > 0)
                         || !string.IsNullOrEmpty(d.MsStoreId)
                         || (d.WinGetPackageId?.Length > 0)
                         || !string.IsNullOrEmpty(d.InstalledBinaryHint))
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

            // Layer 1b: Win32 binary extraction for installed externals.
            int binaryResolved = 0;
            foreach (var def in candidates)
            {
                if (ct.IsCancellationRequested) return;
                if (def.IconPath is not null) continue;            // Already resolved by Layer 1a.
                if (string.IsNullOrEmpty(def.InstalledBinaryHint)) continue;
                if (_binarySource is null) continue;               // No source registered (test constructor without it).

                try
                {
                    if (await TryResolveFromBinaryAsync(def, ct).ConfigureAwait(false))
                        binaryResolved++;
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Binary icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                }
            }

            // Layer 2: Online sources, preference-ordered per entry, parallel across entries.
            int storeAttempted = 0, storeResolved = 0;
            int winGetAttempted = 0, winGetResolved = 0;

            var layer2Candidates = candidates
                .Where(d => d.IconPath is null
                            && (!string.IsNullOrEmpty(d.MsStoreId) || (d.WinGetPackageId?.Length > 0)))
                .ToList();

            if (layer2Candidates.Count > 0)
            {
                using var sem = new SemaphoreSlim(StoreFetchConcurrency);
                var tasks = layer2Candidates.Select(async def =>
                {
                    await sem.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        // 2a — Store CDN, preferred when MsStoreId is present.
                        if (!string.IsNullOrEmpty(def.MsStoreId) && _storeSource is not null)
                        {
                            Interlocked.Increment(ref storeAttempted);
                            try
                            {
                                if (await TryResolveFromStoreAsync(def, ct).ConfigureAwait(false))
                                {
                                    Interlocked.Increment(ref storeResolved);
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logService.LogWarning($"Store icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                            }
                        }

                        // 2b — WinGet manifest fallback (or only path, if no MsStoreId).
                        if (def.IconPath is null && (def.WinGetPackageId?.Length > 0) && _winGetSource is not null)
                        {
                            Interlocked.Increment(ref winGetAttempted);
                            try
                            {
                                if (await TryResolveFromWinGetAsync(def, ct).ConfigureAwait(false))
                                    Interlocked.Increment(ref winGetResolved);
                            }
                            catch (Exception ex)
                            {
                                _logService.LogWarning($"WinGet icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                            }
                        }
                    }
                    finally { sem.Release(); }
                });
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            int unresolved = candidates.Count - appxResolved - binaryResolved - storeResolved - winGetResolved;
            _logService.LogInformation(
                $"AppIconResolver: {candidates.Count} candidates → " +
                $"{appxResolved} via AppX, {binaryResolved} via Binary, " +
                $"{storeResolved}/{storeAttempted} via Store, " +
                $"{winGetResolved}/{winGetAttempted} via WinGet, " +
                $"{unresolved} unresolved");
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

    private async Task<bool> TryResolveFromBinaryAsync(ItemDefinition def, CancellationToken ct)
    {
        var hint = def.InstalledBinaryHint!;

        // Directory hints (from InstallLocation fallback) would return a generic
        // folder icon via Shell APIs — not useful. Skip Layer 1b for those entries
        // so they fall through to Layer 2. A future enhancement could scan the
        // directory for an exe, but that's out of scope.
        if (Directory.Exists(hint) && !File.Exists(hint))
            return false;

        var key = BinaryCachePrefix + Sha1Hex(hint) + CacheFileSuffix;
        var cachePath = Path.Combine(_cacheRoot, key);
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return true;
        }

        await using var stream = await _binarySource!.GetIconStreamAsync(hint, LogoSize, ct).ConfigureAwait(false);
        if (stream is null) return false;

        await WriteStreamToCacheAsync(stream, cachePath, ct).ConfigureAwait(false);
        def.IconPath = cachePath;
        return true;
    }

    private async Task<bool> TryResolveFromWinGetAsync(ItemDefinition def, CancellationToken ct)
    {
        var packageId = def.WinGetPackageId![0];
        var cachePath = Path.Combine(_cacheRoot, WinGetCachePrefix + packageId + CacheFileSuffix);
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return true;
        }

        await using var stream = await _winGetSource!.GetIconStreamAsync(packageId, ct).ConfigureAwait(false);
        if (stream is null) return false;

        await WriteStreamToCacheAsync(stream, cachePath, ct).ConfigureAwait(false);
        def.IconPath = cachePath;
        return true;
    }

    private static string Sha1Hex(string input)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var bytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

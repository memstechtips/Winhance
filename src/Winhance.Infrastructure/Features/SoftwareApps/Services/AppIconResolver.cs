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

    // Cache filenames are <def.Id>.<short-hash>.png. The id makes them readable
    // when poking around %ProgramData%\Winhance\IconCache; the 8-char hash of
    // the layer-specific source key (AppX full-name, expanded local path, or
    // "repo:" + sha256) flips when the source changes so old files get bypassed
    // and PruneOldVersions can clean them up.
    private const string CacheFileExtension = ".png";

    // Bounded concurrency for repo (jsDelivr) icon fetches. These are network
    // round-trips on the (blocking) Windows Apps startup path, so they MUST run
    // in parallel — a serial loop over ~50 icons freezes the load window.
    private const int RepoFetchConcurrency = 8;

    // Logo extraction size, in pixels. Microsoft's GetLogo picks the closest
    // available asset/scale to this hint; 96 reliably returns the high-DPI
    // (200%) variant of Square44x44Logo (~88px native), which renders crisply
    // when displayed at 40 logical pixels on a 200% DPI screen.
    private static readonly Size LogoSize = new(96, 96);

    // ====== TRIM TUNING KNOBS ======
    // Pixels with alpha at or below this threshold are treated as transparent
    // when computing the trim bounding box. The Square44x44Logo PNGs returned
    // by DisplayInfo.GetLogo for installed AppX packages typically have
    // antialiased soft-edge halos around the visible art (alpha ~5-30) which
    // a low threshold preserves, expanding the bbox and making the cached
    // icon appear smaller than equivalent repo icons (which are hard-edge
    // cropped). Higher threshold → tighter bbox → larger visible icon at fixed
    // display size, at the cost of clipping legitimately translucent icon parts.
    // 32 is a reasonable middle ground; 64 if you want maximum tightening; 8
    // (original) for maximum softness preservation. If this changes meaningfully,
    // manually wipe %ProgramData%\Winhance\IconCache so cached files re-extract.
    private const byte AlphaTrimThreshold = 32;

    // ====== BACKPLATE DETECTION KNOBS ======
    // Two unrelated icon sources ship art on a uniform opaque background:
    //   - Sticky Notes (and similar UWP icons) come back from AppX GetLogo
    //     as a small shape on a fully-opaque colored card.
    //   - Some repo / vendor icons arrive as a logo on a flat WHITE background.
    // The plain alpha trim can't touch either because there's no transparency.
    //
    // The backplate pass looks at the 4 corners — if they're all near-opaque
    // AND agree on a single color within BackplateCornerColorTolerance, that
    // color is the backplate. It is then flood-filled to transparent from the
    // image edges inward (FloodFillBackplateToTransparent): every backplate-
    // colored pixel reachable from the border becomes transparent, while
    // backplate-colored pixels *enclosed* by the artwork are preserved. The
    // ordinary alpha trim afterwards crops the now-transparent border.
    //
    // Corner tolerance is intentionally tight (4): we want to detect
    // *deliberate* uniform backplates, not icons whose content happens to
    // share approximate corner colors. Match tolerance is slightly looser (8)
    // to absorb encoding/scaling noise within the backplate itself.
    // MinAlpha guards against detecting backplate from transparent corners.
    private const byte BackplateMinAlpha = 240;
    private const int BackplateCornerColorTolerance = 4;
    private const int BackplateMatchColorTolerance = 8;

    private readonly IAppxIconSource _appxSource;
    private readonly IRepoIconSource? _repoSource;
    private readonly IIconManifestService? _manifest;
    private readonly ILogService _logService;
    private readonly IconCacheMigration _migration;
    private readonly string _cacheRoot;

    // The one-time cache-schema migration must run at most once per process,
    // not once per batch (a wipe mid-session would clear icons resolved earlier
    // in the same run). Guarded by a static flag + lock so concurrent first
    // batches don't both wipe.
    private static bool _schemaEnsured;
    private static readonly object _schemaLock = new();

    /// <summary>Production constructor — uses %ProgramData%\Winhance\IconCache.</summary>
    public AppIconResolver(
        IAppxIconSource appxSource,
        ILogService logService,
        IRepoIconSource repoSource,
        IIconManifestService manifest)
        : this(appxSource, logService, DefaultCacheRoot(), repoSource, manifest) { }

    /// <summary>Test constructor — accepts a custom cache root and optional sources.</summary>
    internal AppIconResolver(
        IAppxIconSource appxSource,
        ILogService logService,
        string cacheRoot,
        IRepoIconSource? repoSource = null,
        IIconManifestService? manifest = null)
    {
        _appxSource = appxSource;
        _logService = logService;
        _cacheRoot = cacheRoot;
        _repoSource = repoSource;
        _manifest = manifest;
        _migration = new IconCacheMigration(logService);
    }

    // Icons aren't per-user state — they're app-wide reference data that any
    // logged-in user looking at Winhance's catalog should see. ProgramData
    // (%CommonApplicationData%) puts the cache alongside Winhance's other
    // shared state (e.g. C:\ProgramData\Winhance\Logs) and means a single
    // download per machine instead of per user.
    private static string DefaultCacheRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), CacheSubDir);

    public async Task ResolveBatchAsync(
        IEnumerable<ItemDefinition> definitions,
        bool applyThemeAdaptation = true,
        CancellationToken ct = default)
    {
        try
        {
            // Any entry with a routable identity gets a try:
            //   - AppX names → installed-package extraction (Layer 1).
            //   - external-app-* / windows-app-* / capability-* / feature-* ids whose
            //     RepoIconKey resolves → package-icons repo (Layer 2).
            // MsStoreId is no longer an icon identity (the live Store API was
            // removed); it remains on the definition for the installer.
            var candidates = definitions
                .Where(d => (d.AppxPackageName?.Length > 0)
                         || RepoIconKey.For(d) is not null
                         || RepoIconKey.WindowsCandidates(d).Any())
                .ToList();
            if (candidates.Count == 0)
                return;

            EnsureSchemaOnce();

            if (!EnsureCacheDir())
                return;

            // Best-effort manifest load (sha256 lookups). A failed load just
            // means repo fetches run without sha verification — still safe,
            // because RepoIconSource validates the image decodes.
            if (_manifest is not null)
            {
                try { await _manifest.LoadAsync(ct).ConfigureAwait(false); }
                catch (Exception ex) { _logService.LogWarning($"Icon manifest load failed: {ex.Message}"); }
            }

            // Layer 1: AppX (current user / all users / provisioned). The
            // canonical source for windows-app-* entries that are actually
            // present on this machine — theme-synthesized light/dark variants.
            var installedMap = await _appxSource.GetInstalledPackageMapAsync(ct).ConfigureAwait(false);

            int appxResolved = 0;
            foreach (var def in candidates)
            {
                if (ct.IsCancellationRequested) return;
                if (def.IconPath is not null) continue;
                try
                {
                    if (await TryResolveFromAppxAsync(def, installedMap, applyThemeAdaptation, ct).ConfigureAwait(false))
                        appxResolved++;
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"AppX icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                }
            }

            // Layer 2: package-icons repo (jsDelivr @main). external-app-*,
            // windows-app-*, capability-*, and feature-* entries that weren't
            // resolved by AppX extraction pull their icon from the hosted repo,
            // sha256-verified against the manifest. Capabilities and optional
            // features resolve purely here (RepoIconKey returns their path).
            int repoResolved = 0;
            if (_repoSource is not null)
            {
                // Parallel (bounded): repo fetches are network round-trips on the
                // blocking startup path; a serial loop over ~50 icons freezes the UI.
                var repoCandidates = candidates.Where(d => d.IconPath is null).ToList();
                if (repoCandidates.Count > 0)
                {
                    using var sem = new SemaphoreSlim(RepoFetchConcurrency);
                    var tasks = repoCandidates.Select(async def =>
                    {
                        await sem.WaitAsync(ct).ConfigureAwait(false);
                        try
                        {
                            if (await TryResolveFromRepoAsync(def, applyThemeAdaptation, ct).ConfigureAwait(false))
                                Interlocked.Increment(ref repoResolved);
                        }
                        catch (Exception ex)
                        {
                            _logService.LogWarning($"Repo icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                        }
                        finally { sem.Release(); }
                    });
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }

            int unresolved = candidates.Count - appxResolved - repoResolved;
            _logService.LogInformation(
                $"AppIconResolver: {candidates.Count} candidates → " +
                $"{appxResolved} via AppX, {repoResolved} via repo, {unresolved} unresolved");
        }
        catch (Exception ex)
        {
            _logService.LogError("Icon resolution batch failed", ex);
        }
    }

    /// <summary>Runs the one-time cache-schema migration at most once per process.</summary>
    private void EnsureSchemaOnce()
    {
        if (_schemaEnsured) return;
        lock (_schemaLock)
        {
            if (_schemaEnsured) return;
            _migration.EnsureSchema(_cacheRoot);
            _schemaEnsured = true;
        }
    }

    private async Task<bool> TryResolveFromAppxAsync(
        ItemDefinition def,
        IReadOnlyDictionary<string, string> installedMap,
        bool applyThemeAdaptation,
        CancellationToken ct)
    {
        var packageNames = def.AppxPackageName;
        if (packageNames is null || packageNames.Length == 0) return false;

        // Walk every declared AppX name and use the first one present in the
        // installed map. Apps like Xbox declare BOTH a modern and a legacy
        // identity (Microsoft.GamingApp + Microsoft.XboxApp); checking only
        // the first name would silently skip the resolve on machines where
        // only the other one is installed.
        string? fullName = null;
        foreach (var packageName in packageNames)
        {
            if (string.IsNullOrEmpty(packageName)) continue;
            if (installedMap.TryGetValue(packageName, out fullName))
                break;
            fullName = null;
        }
        if (fullName is null) return false;

        var cachePath = Path.Combine(_cacheRoot, BuildCacheFileName(def.Id, fullName));
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return true;
        }

        await using var stream = await _appxSource.GetLogoStreamAsync(fullName, LogoSize, ct).ConfigureAwait(false);
        if (stream is null)
            return false;

        await WriteStreamToCacheAsync(stream, cachePath, applyThemeAdaptation, ct).ConfigureAwait(false);
        PruneOldVersions(def.Id, Path.GetFileName(cachePath));
        def.IconPath = cachePath;
        return true;
    }

    /// <summary>
    /// Layer 2: package-icons repo. For external-app-* / capability-* / feature-*
    /// the repo path is <see cref="RepoIconKey.For"/>; for windows-app-* each
    /// <see cref="RepoIconKey.WindowsCandidates"/> is tried in order (first that
    /// fetches wins). Bytes are sha256-verified against the manifest when known,
    /// then cached under <c>"repo:" + sha</c> (or the path when no sha is known).
    /// </summary>
    private async Task<bool> TryResolveFromRepoAsync(
        ItemDefinition def,
        bool applyThemeAdaptation,
        CancellationToken ct)
    {
        if (_repoSource is null) return false;

        IEnumerable<string> paths = def.Id.StartsWith("windows-app-", StringComparison.Ordinal)
            ? RepoIconKey.WindowsCandidates(def)
            : RepoIconKey.For(def) is { } single ? new[] { single } : Array.Empty<string>();

        foreach (var path in paths)
        {
            if (ct.IsCancellationRequested) return false;
            if (string.IsNullOrEmpty(path)) continue;

            var sha = _manifest?.Sha256For(path);

            // Cache key is "repo:" + sha when known (content-addressed: changes
            // when the icon's bytes change), else "repo:" + path so the entry
            // still caches and invalidates on path change.
            var cacheKey = "repo:" + (sha ?? path);
            var cachePath = Path.Combine(_cacheRoot, BuildCacheFileName(def.Id, cacheKey));
            if (File.Exists(cachePath))
            {
                def.IconPath = cachePath;
                return true;
            }

            var bytes = await _repoSource.GetIconBytesAsync(path, sha, ct).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0) continue;

            using var ms = new MemoryStream(bytes);
            // Repo bytes are already validated as a decodable image by
            // RepoIconSource, so a trim failure can safely fall back to raw.
            await WriteStreamToCacheAsync(ms, cachePath, applyThemeAdaptation, ct,
                allowRawFallback: true).ConfigureAwait(false);
            PruneOldVersions(def.Id, Path.GetFileName(cachePath));
            def.IconPath = cachePath;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds the cache filename for an entry: <c>&lt;def.Id&gt;.&lt;short-hash&gt;.png</c>.
    /// The id makes filenames readable; the 8-char SHA1-derived suffix flips when
    /// the layer-specific source key changes (AppX full-name on version bump,
    /// repo sha on icon change, local path on retarget) so the cache invalidates
    /// automatically. 8 hex chars give 32 bits of distinguishing power —
    /// collision probability across the catalog is negligible, and a collision
    /// would only ever cause one entry to display the wrong icon (not a security
    /// concern).
    /// </summary>
    private static string BuildCacheFileName(string defId, string sourceKey) =>
        $"{defId}.{ShortSha1Hex(sourceKey)}{CacheFileExtension}";

    private static string ShortSha1Hex(string input)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var bytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }

    private async Task WriteStreamToCacheAsync(
        Stream source, string cachePath, bool applyThemeAdaptation, CancellationToken ct,
        bool allowRawFallback = true)
    {
        var sourceBytes = await ReadAllBytesAsync(source, ct).ConfigureAwait(false);

        // Backplate detection is theme adaptation — Windows Apps only. External
        // App vendor logos keep whatever framing the vendor shipped; only the
        // basic transparent-border trim runs for them.
        var trimmed = await TryTrimTransparentBordersAsync(sourceBytes, applyThemeAdaptation, ct).ConfigureAwait(false);

        // Untrusted sources (allowRawFallback == false) must decode as an image before
        // we cache them. TryTrimTransparentBordersAsync returns null on any decode failure, so
        // a null here means the bytes aren't a usable image. Reject rather than write arbitrary
        // bytes to %ProgramData%; the caller logs and falls through to the next source / placeholder.
        if (trimmed is null && !allowRawFallback)
            throw new InvalidOperationException("Source bytes did not decode as an image; refusing to cache.");

        var primaryBytes = trimmed ?? sourceBytes;

        await WriteBytesAtomicAsync(cachePath, primaryBytes, ct).ConfigureAwait(false);

        // Light/dark variant synthesis is theme adaptation — Windows Apps only.
        // External App vendor logos are single brand marks (no monochrome
        // light/dark pair), so we never recolor them.
        if (!applyThemeAdaptation)
            return;

        // Variant synthesis is best-effort: the primary is already on disk
        // and IconPath will still resolve. Any WinRT failure inside the
        // synthesizer lands here with a logged warning so it shows up in
        // %ProgramData%\Winhance\Logs instead of silently dropping the
        // light/dark companions.
        byte[]? lightBytes = null;
        byte[]? darkBytes = null;
        try
        {
            (lightBytes, darkBytes) = await LightVariantSynthesizer
                .TryGenerateAsync(primaryBytes, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.LogWarning(
                $"Light/dark variant synthesis failed for {Path.GetFileName(cachePath)}: {ex.Message}");
        }

        if (lightBytes is not null)
            await WriteBytesAtomicAsync(LightVariantPath(cachePath), lightBytes, ct).ConfigureAwait(false);

        if (darkBytes is not null)
            await WriteBytesAtomicAsync(DarkVariantPath(cachePath), darkBytes, ct).ConfigureAwait(false);
    }

    private static async Task WriteBytesAtomicAsync(string path, byte[] bytes, CancellationToken ct)
    {
        var tmpPath = path + ".tmp";
        await File.WriteAllBytesAsync(tmpPath, bytes, ct).ConfigureAwait(false);
        File.Move(tmpPath, path, overwrite: true);
    }

    /// <summary>
    /// Sibling path for the light-mode variant of <paramref name="primaryPath"/>.
    /// Replaces the trailing <c>.png</c> with <c>.light.png</c>. Survives prune
    /// alongside the primary — see <see cref="PruneOldVersions"/>.
    /// </summary>
    internal static string LightVariantPath(string primaryPath) =>
        Path.ChangeExtension(primaryPath, null) + ".light.png";

    /// <summary>
    /// Sibling path for the dark-mode variant of <paramref name="primaryPath"/>.
    /// Replaces the trailing <c>.png</c> with <c>.dark.png</c>. Only written for
    /// mono-dark source icons (e.g. Xbox Game Bar's <c>#333</c> grey) where the
    /// primary's tone reads as faded against the dark card background.
    /// </summary>
    internal static string DarkVariantPath(string primaryPath) =>
        Path.ChangeExtension(primaryPath, null) + ".dark.png";

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
    /// Normalizes a source PNG/JPG: when a uniform opaque backplate is
    /// detected, it is flood-filled to transparent from the image edges; the
    /// result is then cropped to the bounding box of its non-transparent
    /// pixels and re-encoded as PNG.
    ///
    /// This handles two cases the plain alpha trim cannot:
    ///   - Sticky Notes ships its AppX logo as a small shape on a fully-opaque
    ///     colored card — flood-filling the card leaves just the inner art.
    ///   - Some icons arrive as a logo on a flat white background —
    ///     flood-filling the white yields a clean transparent icon.
    ///
    /// Backplate detection requires all four corner pixels to be near-opaque
    /// AND match each other within <see cref="BackplateCornerColorTolerance"/>.
    /// The flood-fill clears every backplate-colored pixel reachable from the
    /// border (<see cref="BackplateMatchColorTolerance"/>); backplate-colored
    /// pixels enclosed by the artwork are preserved. Tolerances are tight on
    /// purpose — we want deliberate backplates, not icons whose content
    /// happens to share corner colors.
    ///
    /// Returns null on any decoder/encoder failure or when the input has no
    /// visible content — caller falls back to the untrimmed source bytes.
    ///
    /// <paramref name="detectBackplate"/> gates the backplate flood-fill:
    /// true for Windows Apps, false for External App vendor logos (which keep
    /// whatever framing the vendor shipped). The basic transparent-border trim
    /// runs regardless.
    /// </summary>
    private async Task<byte[]?> TryTrimTransparentBordersAsync(
        byte[] source, bool detectBackplate, CancellationToken ct)
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

            // Pre-declared (not inline `out`) so they stay definitely-assigned
            // when the short-circuit skips TryDetectUniformBackplate.
            byte bpR = 0, bpG = 0, bpB = 0;
            bool hasBackplate = detectBackplate
                && TryDetectUniformBackplate(pixels, width, height, out bpR, out bpG, out bpB);

            // Flood-fill the detected backplate to transparent. After this the
            // bbox computation below is a pure alpha trim — the formerly-opaque
            // backplate now reads as transparent border.
            if (hasBackplate)
                FloodFillBackplateToTransparent(pixels, width, height, bpR, bpG, bpB);

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

            // No visible content (all transparent, or all backplate) — caller
            // falls back to source bytes unchanged.
            if (maxX < minX || maxY < minY) return null;

            uint cropX = (uint)minX;
            uint cropY = (uint)minY;
            uint cropW = (uint)(maxX - minX + 1);
            uint cropH = (uint)(maxY - minY + 1);

            // No crop needed AND no flood-fill happened — re-encoding wouldn't
            // change anything, so skip the round-trip. When a backplate was
            // flood-filled the pixels changed even if the bbox is full, so we
            // must re-encode.
            if (!hasBackplate && cropX == 0 && cropY == 0 && cropW == width && cropH == height)
                return null;

            // Encode from the (possibly flood-filled) pixel buffer rather than
            // the original decoder bitmap, so the transparency edits land in
            // the output. The crop transform tightens to the visible bbox.
            using var outBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
            outBitmap.CopyFromBuffer(pixels.AsBuffer());

            using var outStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
            encoder.SetSoftwareBitmap(outBitmap);
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

    /// <summary>
    /// Inspects the 4 corner pixels. If all four are near-opaque AND match
    /// each other within <see cref="BackplateCornerColorTolerance"/>, returns
    /// true with the detected backplate color set into the out parameters.
    /// Returns false (and zeroed out params) otherwise — the trim then falls
    /// back to alpha-only border detection.
    /// </summary>
    private static bool TryDetectUniformBackplate(
        byte[] pixels, int width, int height,
        out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (width < 2 || height < 2) return false;

        int[] offsets =
        {
            0,                                                  // (0, 0)
            (width - 1) * 4,                                    // (w-1, 0)
            (height - 1) * width * 4,                           // (0, h-1)
            ((height - 1) * width + (width - 1)) * 4,           // (w-1, h-1)
        };

        byte[] cr = new byte[4], cg = new byte[4], cb = new byte[4], ca = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            cb[i] = pixels[offsets[i] + 0];
            cg[i] = pixels[offsets[i] + 1];
            cr[i] = pixels[offsets[i] + 2];
            ca[i] = pixels[offsets[i] + 3];

            if (ca[i] < BackplateMinAlpha) return false;
        }

        for (int i = 1; i < 4; i++)
        {
            if (Math.Abs(cr[i] - cr[0]) > BackplateCornerColorTolerance) return false;
            if (Math.Abs(cg[i] - cg[0]) > BackplateCornerColorTolerance) return false;
            if (Math.Abs(cb[i] - cb[0]) > BackplateCornerColorTolerance) return false;
        }

        r = cr[0]; g = cg[0]; b = cb[0];
        return true;
    }

    private static bool IsBackplateColor(
        byte pixelR, byte pixelG, byte pixelB,
        byte bpR, byte bpG, byte bpB) =>
        Math.Abs(pixelR - bpR) <= BackplateMatchColorTolerance
        && Math.Abs(pixelG - bpG) <= BackplateMatchColorTolerance
        && Math.Abs(pixelB - bpB) <= BackplateMatchColorTolerance;

    /// <summary>
    /// 4-connected flood fill from the image border: every near-opaque pixel
    /// matching the backplate color, reachable from an edge without crossing
    /// the artwork, is set fully transparent. Backplate-colored pixels that
    /// are enclosed by the artwork are not reached and stay intact. The
    /// <paramref name="pixels"/> buffer is mutated in place (BGRA, premultiplied).
    /// </summary>
    private static void FloodFillBackplateToTransparent(
        byte[] pixels, int width, int height, byte bpR, byte bpG, byte bpB)
    {
        var visited = new bool[width * height];
        var queue = new Queue<int>();

        void TryEnqueue(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int idx = y * width + x;
            if (visited[idx]) return;

            int p = idx * 4;
            if (pixels[p + 3] < BackplateMinAlpha) return;
            if (!IsBackplateColor(pixels[p + 2], pixels[p + 1], pixels[p + 0], bpR, bpG, bpB)) return;

            visited[idx] = true;
            queue.Enqueue(idx);
        }

        for (int x = 0; x < width; x++)
        {
            TryEnqueue(x, 0);
            TryEnqueue(x, height - 1);
        }
        for (int y = 0; y < height; y++)
        {
            TryEnqueue(0, y);
            TryEnqueue(width - 1, y);
        }

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int p = idx * 4;
            pixels[p + 0] = 0;
            pixels[p + 1] = 0;
            pixels[p + 2] = 0;
            pixels[p + 3] = 0;

            int x = idx % width;
            int y = idx / width;
            TryEnqueue(x - 1, y);
            TryEnqueue(x + 1, y);
            TryEnqueue(x, y - 1);
            TryEnqueue(x, y + 1);
        }
    }

    /// <summary>
    /// After a successful cache write, deletes any other cache files that
    /// share this entry's def.Id but a different short-hash — i.e. icons cached
    /// under older AppX package full-names or older repo shas from prior versions
    /// of the same app. Keeps the cache from accumulating stale per-version
    /// entries on long-lived installs.
    /// </summary>
    private void PruneOldVersions(string defId, string keepFileName)
    {
        try
        {
            // Keep <stem>.png (primary) plus its synthesized variants
            // <stem>.light.png and <stem>.dark.png from LightVariantSynthesizer.
            // Without the sibling carve-out, the freshly-written variants
            // would be deleted here on every resolve, since they don't
            // equal keepFileName.
            var keepStem = Path.GetFileNameWithoutExtension(keepFileName);
            var keepLightName = keepStem + ".light" + CacheFileExtension;
            var keepDarkName = keepStem + ".dark" + CacheFileExtension;

            var pattern = defId + ".*" + CacheFileExtension;
            foreach (var path in Directory.EnumerateFiles(_cacheRoot, pattern))
            {
                var name = Path.GetFileName(path);
                if (string.Equals(name, keepFileName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, keepLightName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, keepDarkName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try { File.Delete(path); }
                catch (Exception ex) { _logService.LogWarning($"Could not prune old icon {path}: {ex.Message}"); }
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

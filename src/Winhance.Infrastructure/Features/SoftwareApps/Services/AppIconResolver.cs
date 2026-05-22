using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
    // when poking around %ProgramData%\Winhance\IconCache, the 8-char hash of
    // the layer-specific source key (AppX full-name, binary path, MsStoreId,
    // or IconSources entry) flips when the source changes so old files get
    // bypassed and PruneOldVersions can clean them up.
    private const string CacheFileExtension = ".png";

    // Per-call timeout for an IconSources URL fetch. Caps per-entry cost so one
    // slow vendor CDN can't stall the resolver for everyone else.
    private static readonly TimeSpan IconSourceFetchTimeout = TimeSpan.FromSeconds(8);

    // User-Agent for icon fetches. Wikimedia's UA policy rejects empty / generic
    // UAs with HTTP 403 — see https://meta.wikimedia.org/wiki/User-Agent_policy —
    // and several vendor sites behind Cloudflare do the same. Identifying the
    // app + a contact URL satisfies both. Set per-request rather than on the
    // shared HttpClient so other services (download, WIM tooling, etc.) keep
    // their existing behavior.
    private const string IconFetchUserAgent = "Winhance/1.0 (+https://github.com/memstechtips/Winhance)";
    // Concurrency limit for the parallel network batches (IconSources fetches and
    // Store CDN lookups). Each entry costs at least one HTTP round-trip; running
    // them parallel keeps cold-cache load times bounded. Hosts known to rate-limit
    // aggressive callers get an additional per-host cap below.
    private const int NetworkBatchConcurrency = 5;

    // Per-host concurrency caps for hosts that throttle aggressive callers below
    // the global limit. Wikimedia's upload CDN returns HTTP 429 on cold-start
    // bursts when all slots of the global limit pile onto a single host (Layer 1
    // piles its slots onto upload.wikimedia.org because that's where most icons
    // live). Capping in-flight Wikimedia fetches at 2 stays comfortably below
    // their documented soft limit (~5 RPS for an identified UA) while keeping
    // cold start fast; any transient stragglers are picked up by the
    // batch-level retry loop in ResolveBatchAsync. Static so the gate state is
    // shared across all batches in the process. Never disposed — lifetime is
    // the resolver class, not the batch call.
    private static readonly IReadOnlyDictionary<string, SemaphoreSlim> PerHostGates =
        new Dictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase)
        {
            ["upload.wikimedia.org"] = new SemaphoreSlim(2, 2),
        };

    // 429 retry schedule for the batch-level Layer 1 retry loop. After the first
    // pass through IconSources, any entry that 429'd at least once is retried in
    // a follow-up pass with this delay in front of it. Schedule is escalating
    // because Wikimedia's burst limiter usually clears in 1-3 s but a sustained
    // throttle can stick for tens of seconds. The array length caps total
    // attempts — after the last delay the loop gives up so the loading spinner
    // eventually drops on a Wikimedia outage. Total worst case ~3 minutes.
    private static readonly TimeSpan[] Layer1RetryDelays = new[]
    {
        TimeSpan.FromMilliseconds(1500),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(6),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
    };

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
    // icon appear smaller than equivalent Store-CDN icons (which are
    // hard-edge cropped). Higher threshold → tighter bbox → larger visible
    // icon at fixed display size, at the cost of clipping legitimately
    // translucent icon parts. 32 is a reasonable middle ground; 64 if you
    // want maximum tightening; 8 (original) for maximum softness preservation.
    // If this changes meaningfully, manually wipe %ProgramData%\Winhance\IconCache
    // so cached files re-extract.
    private const byte AlphaTrimThreshold = 32;

    // ====== BACKPLATE DETECTION KNOBS ======
    // Two unrelated icon sources ship art on a uniform opaque background:
    //   - Sticky Notes (and similar UWP icons) come back from AppX GetLogo
    //     as a small shape on a fully-opaque colored card.
    //   - Microsoft Store CDN icons (the Layer 4 fallback) often arrive as a
    //     logo on a flat WHITE background — e.g. Teams / To Do when the
    //     curated IconSources URL is unreachable and the resolver falls back.
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
    private readonly IStoreIconSource? _storeSource;
    private readonly IBinaryIconSource? _binarySource;
    private readonly HttpClient? _httpClient;
    private readonly ILogService _logService;
    private readonly string _cacheRoot;

    /// <summary>Production constructor — uses %ProgramData%\Winhance\IconCache.</summary>
    public AppIconResolver(
        IAppxIconSource appxSource,
        ILogService logService,
        IStoreIconSource storeSource,
        IBinaryIconSource binarySource,
        HttpClient httpClient)
        : this(appxSource, logService, DefaultCacheRoot(), storeSource, binarySource, httpClient) { }

    /// <summary>Test constructor — accepts a custom cache root and optional sources.</summary>
    internal AppIconResolver(
        IAppxIconSource appxSource,
        ILogService logService,
        string cacheRoot,
        IStoreIconSource? storeSource = null,
        IBinaryIconSource? binarySource = null,
        HttpClient? httpClient = null)
    {
        _appxSource = appxSource;
        _logService = logService;
        _cacheRoot = cacheRoot;
        _storeSource = storeSource;
        _binarySource = binarySource;
        _httpClient = httpClient;
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
            // Any entry with a routable identity gets a try. IconSources entries
            // go through Layer 1 (URLs / data: URIs / local file paths) — the
            // canonical source when set. Without (or after) IconSources, AppX-
            // named entries fall back to Layer 2 (local AppX enumeration),
            // InstalledBinaryHint to Layer 3 (binary extraction), and MsStoreId
            // to Layer 4 (Store CDN).
            var candidates = definitions
                .Where(d => (d.AppxPackageName?.Length > 0)
                         || !string.IsNullOrEmpty(d.MsStoreId)
                         || !string.IsNullOrEmpty(d.InstalledBinaryHint)
                         || (d.IconSources?.Length > 0))
                .ToList();
            if (candidates.Count == 0)
                return;

            if (!EnsureCacheDir())
                return;

            // Layer 1: IconSources — the canonical source when set. Runs first
            // and in parallel because URL fetches benefit from concurrency. Other
            // layers fall back only for entries where IconSources is unset or
            // every entry in the array missed.
            //
            // 429 handling: rate-limit responses are tracked per-entry in
            // 'rateLimited'. After the first pass, any entry that 429'd and is
            // still unresolved gets re-attempted in a follow-up pass with
            // backoff (Layer1RetryDelays). Other failure modes (404/403/timeout)
            // are NOT retried — they fall through to the lower layers as today.
            int sourcesAttempted = 0, sourcesResolved = 0;
            var sourceCandidates = candidates.Where(d => d.IconSources?.Length > 0).ToList();

            if (sourceCandidates.Count > 0)
            {
                var rateLimited = new ConcurrentDictionary<string, byte>();
                sourcesAttempted = sourceCandidates.Count;
                sourcesResolved += await RunLayer1PassAsync(sourceCandidates, rateLimited, applyThemeAdaptation, ct).ConfigureAwait(false);

                for (int attempt = 0; attempt < Layer1RetryDelays.Length; attempt++)
                {
                    if (ct.IsCancellationRequested) break;

                    var retryCandidates = sourceCandidates
                        .Where(d => d.IconPath is null && rateLimited.ContainsKey(d.Id))
                        .ToList();
                    if (retryCandidates.Count == 0) break;

                    _logService.LogInformation(
                        $"AppIconResolver: Layer 1 retry pass {attempt + 1}/{Layer1RetryDelays.Length} — " +
                        $"{retryCandidates.Count} entries still rate-limited, waiting {Layer1RetryDelays[attempt].TotalSeconds:0.0}s");

                    try { await Task.Delay(Layer1RetryDelays[attempt], ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }

                    rateLimited.Clear();
                    sourcesResolved += await RunLayer1PassAsync(retryCandidates, rateLimited, applyThemeAdaptation, ct).ConfigureAwait(false);
                }
            }

            // Layer 2: AppX (current user / all users / provisioned) — fallback for
            // entries without IconSources, or where IconSources came up empty.
            var installedMap = await _appxSource.GetInstalledPackageMapAsync(ct).ConfigureAwait(false);

            int appxResolved = 0;
            foreach (var def in candidates)
            {
                if (ct.IsCancellationRequested) return;
                if (def.IconPath is not null) continue;            // Already resolved by Layer 1.
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

            // Layer 3: Win32 binary extraction for installed externals.
            int binaryResolved = 0;
            foreach (var def in candidates)
            {
                if (ct.IsCancellationRequested) return;
                if (def.IconPath is not null) continue;
                if (string.IsNullOrEmpty(def.InstalledBinaryHint)) continue;
                if (_binarySource is null) continue;               // No source registered (test constructor without it).

                try
                {
                    if (await TryResolveFromBinaryAsync(def, applyThemeAdaptation, ct).ConfigureAwait(false))
                        binaryResolved++;
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Binary icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                }
            }

            // Layer 4: Store CDN — final fallback for entries with MsStoreId where
            // none of the above paid out. Parallel because each is a network call.
            int storeAttempted = 0, storeResolved = 0;
            var storeCandidates = candidates
                .Where(d => d.IconPath is null && !string.IsNullOrEmpty(d.MsStoreId))
                .ToList();

            if (storeCandidates.Count > 0 && _storeSource is not null)
            {
                using var sem = new SemaphoreSlim(NetworkBatchConcurrency);
                var tasks = storeCandidates.Select(async def =>
                {
                    await sem.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        Interlocked.Increment(ref storeAttempted);
                        if (await TryResolveFromStoreAsync(def, applyThemeAdaptation, ct).ConfigureAwait(false))
                            Interlocked.Increment(ref storeResolved);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Store icon resolution failed for {def.Id} ({def.Name}): {ex.Message}");
                    }
                    finally { sem.Release(); }
                });
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            int unresolved = candidates.Count - sourcesResolved - appxResolved - binaryResolved - storeResolved;
            _logService.LogInformation(
                $"AppIconResolver: {candidates.Count} candidates → " +
                $"{sourcesResolved}/{sourcesAttempted} via IconSources, " +
                $"{appxResolved} via AppX, {binaryResolved} via Binary, " +
                $"{storeResolved}/{storeAttempted} via Store, " +
                $"{unresolved} unresolved");
        }
        catch (Exception ex)
        {
            _logService.LogError("Icon resolution batch failed", ex);
        }
    }

    /// <summary>
    /// Layer 1: walk <see cref="ItemDefinition.IconSources"/> in order, return on
    /// the first entry that yields a non-empty image. Each entry is one of:
    /// <list type="bullet">
    /// <item><description><c>http(s)://</c> URL — fetched via <see cref="HttpClient"/>.</description></item>
    /// <item><description><c>data:image/&lt;type&gt;;base64,&lt;payload&gt;</c> URI —
    /// the base64 payload is decoded directly. Useful when a vendor only ships their
    /// logo embedded in HTML/CSS and no stable raw URL exists.</description></item>
    /// <item><description>Local file path — read with
    /// <see cref="File.ReadAllBytesAsync(string, CancellationToken)"/> after env-var expansion.</description></item>
    /// </list>
    /// </summary>
    /// <summary>
    /// Runs one Layer 1 pass over the given candidates with parallel HTTP fetches
    /// bounded by <see cref="NetworkBatchConcurrency"/>. Per-entry 429 outcomes
    /// are recorded in <paramref name="rateLimited"/> so the caller can drive the
    /// batch-level retry loop. Returns the count of entries that resolved on this pass.
    /// </summary>
    private async Task<int> RunLayer1PassAsync(
        List<ItemDefinition> candidates,
        ConcurrentDictionary<string, byte> rateLimited,
        bool applyThemeAdaptation,
        CancellationToken ct)
    {
        int resolved = 0;
        using var sem = new SemaphoreSlim(NetworkBatchConcurrency);
        var tasks = candidates.Select(async def =>
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (await TryResolveFromIconSourcesAsync(def, rateLimited, applyThemeAdaptation, ct).ConfigureAwait(false))
                    Interlocked.Increment(ref resolved);
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"IconSources resolution failed for {def.Id} ({def.Name}): {ex.Message}");
            }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return resolved;
    }

    private async Task<bool> TryResolveFromIconSourcesAsync(
        ItemDefinition def,
        ConcurrentDictionary<string, byte> rateLimited,
        bool applyThemeAdaptation,
        CancellationToken ct)
    {
        var sources = def.IconSources;
        if (sources is null || sources.Length == 0) return false;

        foreach (var source in sources)
        {
            if (ct.IsCancellationRequested) return false;
            if (string.IsNullOrWhiteSpace(source)) continue;

            var kind = ClassifyIconSource(source);

            // Cache key encodes the source string so any change invalidates the cache
            // automatically. Local paths are env-expanded first so two definitions
            // pointing at the same expanded file share one cache entry. URLs and data:
            // URIs are hashed verbatim. SHA1 is fine — purely a cache key, not a trust boundary.
            var cacheKeyInput = kind == IconSourceKind.LocalPath
                ? Environment.ExpandEnvironmentVariables(source)
                : source;
            var cachePath = Path.Combine(_cacheRoot, BuildCacheFileName(def.Id, cacheKeyInput));
            if (File.Exists(cachePath))
            {
                def.IconPath = cachePath;
                return true;
            }

            try
            {
                byte[]? bytes;
                if (kind == IconSourceKind.Url)
                {
                    var (b, wasRateLimited) = await FetchUrlBytesAsync(source, def, ct).ConfigureAwait(false);
                    if (wasRateLimited) rateLimited.TryAdd(def.Id, 0);
                    bytes = b;
                }
                else
                {
                    bytes = kind switch
                    {
                        IconSourceKind.DataUri => DecodeBase64DataUri(source),
                        IconSourceKind.LocalPath => await ReadLocalSourceBytesAsync(source, ct).ConfigureAwait(false),
                        _ => null,
                    };
                }
                if (bytes is null || bytes.Length == 0) continue;

                using var ms = new MemoryStream(bytes);
                await WriteStreamToCacheAsync(ms, cachePath, applyThemeAdaptation, ct).ConfigureAwait(false);
                def.IconPath = cachePath;
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogWarning(
                    $"IconSources entry failed for {def.Id} ({def.Name}) <{Truncate(source, 80)}>: {ex.Message}");
                // Continue to the next source in the array.
            }
        }

        return false;
    }

    private enum IconSourceKind { Url, DataUri, LocalPath }

    private static IconSourceKind ClassifyIconSource(string source)
    {
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return IconSourceKind.Url;
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return IconSourceKind.DataUri;
        return IconSourceKind.LocalPath;
    }

    /// <summary>
    /// Decodes a <c>data:&lt;media-type&gt;;base64,&lt;payload&gt;</c> URI into raw bytes.
    /// Returns null for unsupported variants (no <c>;base64</c> marker, missing comma,
    /// invalid base64). Non-base64 (URL-encoded) payloads are deliberately rejected —
    /// IconSources is for binary image data, not text.
    /// </summary>
    private static byte[]? DecodeBase64DataUri(string source)
    {
        // Format: data:[<media-type>][;base64],<payload>
        var commaIndex = source.IndexOf(',');
        if (commaIndex < 0) return null;

        var header = source.AsSpan(5, commaIndex - 5); // skip "data:"
        if (!header.Contains(";base64".AsSpan(), StringComparison.OrdinalIgnoreCase))
            return null;

        var payload = source.AsSpan(commaIndex + 1);
        if (payload.IsEmpty) return null;

        try
        {
            return Convert.FromBase64String(payload.ToString());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "...";

    /// <summary>
    /// Fetches the bytes for a URL IconSources entry. Returns
    /// <c>(bytes, wasRateLimited)</c>. <c>wasRateLimited == true</c> means the
    /// server returned HTTP 429 — the caller (TryResolveFromIconSourcesAsync)
    /// records this so the batch-level retry loop in ResolveBatchAsync can
    /// re-attempt the entry. Other failure modes (404, timeouts, network errors)
    /// produce <c>(null, false)</c> and are not retried.
    /// </summary>
    private async Task<(byte[]? Bytes, bool WasRateLimited)> FetchUrlBytesAsync(
        string url, ItemDefinition def, CancellationToken ct)
    {
        if (_httpClient is null) return (null, false);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Acquire the per-host gate BEFORE arming the per-fetch timeout. If we
        // armed the timeout first, an entry queued behind a slow earlier fetch
        // would burn its 8 s budget waiting for the gate to free up and then
        // get cancelled with no network turn. Linking the wait to the parent
        // cancellation token lets a batch-cancel still tear it down.
        SemaphoreSlim? hostGate = null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && PerHostGates.TryGetValue(uri.Host, out var gate))
        {
            hostGate = gate;
            await hostGate.WaitAsync(linked.Token).ConfigureAwait(false);
        }

        try
        {
            linked.CancelAfter(IconSourceFetchTimeout);

            using var resp = await SendIconRequestAsync(url, linked.Token).ConfigureAwait(false);

            if ((int)resp.StatusCode == 429)
            {
                _logService.LogInformation(
                    $"IconSources URL returned HTTP 429 for {def.Id} ({def.Name}) <{url}>");
                return (null, true);
            }
            if (!resp.IsSuccessStatusCode)
            {
                _logService.LogInformation(
                    $"IconSources URL returned HTTP {(int)resp.StatusCode} for {def.Id} ({def.Name}) <{url}>");
                return (null, false);
            }

            await using var srcStream = await resp.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            using var collector = new MemoryStream();
            await srcStream.CopyToAsync(collector, linked.Token).ConfigureAwait(false);
            return (collector.Length > 0 ? collector.ToArray() : null, false);
        }
        finally
        {
            hostGate?.Release();
        }
    }

    private async Task<HttpResponseMessage> SendIconRequestAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd(IconFetchUserAgent);
        req.Headers.Accept.ParseAdd("image/*");
        return await _httpClient!
            .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads bytes for a non-URL <see cref="ItemDefinition.IconSources"/> entry. For
    /// Win32 executables (<c>.exe</c>/<c>.dll</c>) delegates to the binary icon
    /// extractor — the same code path Layer 3 uses for <see cref="ItemDefinition.InstalledBinaryHint"/>.
    /// This lets entries reuse system binaries (e.g. <c>%SystemRoot%\explorer.exe</c>
    /// for ExplorerPatcher) without per-app code in the resolver. For everything else
    /// (icon files like <c>.ico</c>/<c>.png</c>) reads the bytes directly.
    /// </summary>
    private async Task<byte[]?> ReadLocalSourceBytesAsync(string path, CancellationToken ct)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (!File.Exists(expanded)) return null;

        if (IsExecutableExtension(expanded))
        {
            // .exe/.dll: raw bytes aren't a valid image — must go through the
            // binary extractor. If no source is registered (test constructor
            // without one), treat as a miss so the resolver tries the next entry.
            if (_binarySource is null) return null;

            await using var stream = await _binarySource
                .GetIconStreamAsync(expanded, LogoSize, ct)
                .ConfigureAwait(false);
            if (stream is null) return null;
            using var collector = new MemoryStream();
            await stream.CopyToAsync(collector, ct).ConfigureAwait(false);
            return collector.Length > 0 ? collector.ToArray() : null;
        }

        return await File.ReadAllBytesAsync(expanded, ct).ConfigureAwait(false);
    }

    private static bool IsExecutableExtension(string path) =>
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

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

    private async Task<bool> TryResolveFromStoreAsync(ItemDefinition def, bool applyThemeAdaptation, CancellationToken ct)
    {
        var msStoreId = def.MsStoreId!;
        var cachePath = Path.Combine(_cacheRoot, BuildCacheFileName(def.Id, msStoreId));
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return true;
        }

        await using var stream = await _storeSource!.GetIconStreamAsync(msStoreId, ct).ConfigureAwait(false);
        if (stream is null)
            return false;

        await WriteStreamToCacheAsync(stream, cachePath, applyThemeAdaptation, ct).ConfigureAwait(false);
        def.IconPath = cachePath;
        return true;
    }

    private async Task<bool> TryResolveFromBinaryAsync(ItemDefinition def, bool applyThemeAdaptation, CancellationToken ct)
    {
        var hint = def.InstalledBinaryHint!;

        // Directory hints (from InstallLocation fallback) would return a generic
        // folder icon via Shell APIs — not useful. Skip binary extraction for
        // those entries so they fall through to Store CDN. A future enhancement
        // could scan the directory for an exe, but that's out of scope.
        // Directory.Exists returns true only for directories on Windows;
        // File.Exists check is redundant. Drop the second clause for clarity.
        if (Directory.Exists(hint))
            return false;

        var cachePath = Path.Combine(_cacheRoot, BuildCacheFileName(def.Id, hint));
        if (File.Exists(cachePath))
        {
            def.IconPath = cachePath;
            return true;
        }

        await using var stream = await _binarySource!.GetIconStreamAsync(hint, LogoSize, ct).ConfigureAwait(false);
        if (stream is null) return false;

        await WriteStreamToCacheAsync(stream, cachePath, applyThemeAdaptation, ct).ConfigureAwait(false);
        def.IconPath = cachePath;
        return true;
    }

    /// <summary>
    /// Builds the cache filename for an entry: <c>&lt;def.Id&gt;.&lt;short-hash&gt;.png</c>.
    /// The id makes filenames readable; the 8-char SHA1-derived suffix flips when
    /// the layer-specific source key changes (AppX full-name on version bump,
    /// IconSources URL on URL-rot, etc.) so the cache invalidates automatically.
    /// 8 hex chars give 32 bits of distinguishing power — collision probability
    /// across the catalog is negligible, and a collision would only ever cause
    /// one entry to display the wrong icon (not a security concern).
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
        Stream source, string cachePath, bool applyThemeAdaptation, CancellationToken ct)
    {
        var sourceBytes = await ReadAllBytesAsync(source, ct).ConfigureAwait(false);

        // Backplate detection is theme adaptation — Windows Apps only. External
        // App vendor logos keep whatever framing the vendor shipped; only the
        // basic transparent-border trim runs for them.
        var primaryBytes = await TryTrimTransparentBordersAsync(sourceBytes, applyThemeAdaptation, ct).ConfigureAwait(false)
                          ?? sourceBytes;

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
    ///   - Store CDN icons (Teams / To Do) often arrive as a logo on a flat
    ///     white background — flood-filling the white yields a clean
    ///     transparent icon.
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
    /// After a successful AppX cache write, deletes any other cache files that
    /// share this entry's def.Id but a different short-hash — i.e. icons cached
    /// under older AppX package full-names from prior versions of the same app.
    /// Keeps the cache from accumulating stale per-version entries on long-lived
    /// installs.
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

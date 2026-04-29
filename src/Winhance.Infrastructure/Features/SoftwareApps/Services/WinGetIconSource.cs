using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Deployment;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Layer 2b orchestration: resolves WinGet package icons by trying the bundled
/// COM API first (fast, in-process), falling back to GitHub raw-manifest fetch
/// when COM is unavailable or specifically failed for the package. Maintains
/// session-level short-circuit flags so a layer-wide failure (COM bootstrap dead,
/// GitHub rate-limit hit) doesn't pay the full per-entry cost on every later call.
/// </summary>
public class WinGetIconSource : IWinGetIconSource
{
    private static readonly TimeSpan PerCallTimeout = TimeSpan.FromSeconds(8);

    private readonly IWinGetBootstrapper _bootstrapper;
    private readonly IWinGetManifestFetcher _fetcher;
    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;
    private readonly Func<string, CancellationToken, Task<string?>> _comIconUrlsAsync;

    private bool _gitHubRateLimited;

    /// <summary>Production constructor — wires the COM path to the real session-based catalog call.</summary>
    public WinGetIconSource(
        IWinGetBootstrapper bootstrapper,
        IWinGetManifestFetcher fetcher,
        HttpClient httpClient,
        ILogService logService,
        WinGetComSession comSession)
        : this(bootstrapper, fetcher, httpClient, logService,
               comIconUrlsAsync: (id, ct) => GetIconUrlViaComAsync(id, ct, comSession, logService))
    {
    }

    /// <summary>Test constructor — accepts a fake COM callable for unit tests.</summary>
    internal WinGetIconSource(
        IWinGetBootstrapper bootstrapper,
        IWinGetManifestFetcher fetcher,
        HttpClient httpClient,
        ILogService logService,
        Func<string, CancellationToken, Task<string?>> comIconUrlsAsync)
    {
        _bootstrapper = bootstrapper;
        _fetcher = fetcher;
        _httpClient = httpClient;
        _logService = logService;
        _comIconUrlsAsync = comIconUrlsAsync;
    }

    public async Task<Stream?> GetIconStreamAsync(string winGetPackageId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(winGetPackageId)) return null;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(PerCallTimeout);

            string? iconUrl = null;
            bool comCleanMiss = false; // true = COM ran successfully and returned null (no Icons)

            // Phase 1: COM (skipped if bootstrap reports unavailable).
            if (_bootstrapper.IsSystemWinGetAvailable)
            {
                try
                {
                    iconUrl = await _comIconUrlsAsync(winGetPackageId, linked.Token).ConfigureAwait(false);
                    if (iconUrl is null)
                        comCleanMiss = true; // COM ran, found the package, but no Icons in manifest
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"WinGetIconSource: COM lookup failed for '{winGetPackageId}', trying GitHub fallback: {ex.Message}");
                    // iconUrl stays null, comCleanMiss stays false → fall through to fetcher below
                }
            }

            // Phase 2: GitHub fallback.
            // Consult the fetcher when:
            //   - COM was skipped (WinGet unavailable), OR
            //   - COM threw (exception path, not a clean null return)
            // Do NOT consult the fetcher when COM returned null cleanly (no Icons in manifest):
            // that means the package genuinely has no icon in the WinGet catalog; the GitHub
            // manifest fetcher would hit the same upstream data and return null too.
            if (iconUrl is null && !comCleanMiss && !_gitHubRateLimited)
            {
                try
                {
                    iconUrl = await _fetcher.GetIconUrlAsync(winGetPackageId, linked.Token).ConfigureAwait(false);
                }
                catch (WinGetManifestFetcher.RateLimitExceededException rl)
                {
                    _gitHubRateLimited = true;
                    var resetMsg = rl.ResetAt is DateTimeOffset reset
                        ? $" (resets at {reset:O})"
                        : string.Empty;
                    _logService.LogWarning($"WinGetIconSource: GitHub Contents API rate limit hit{resetMsg} — skipping Layer 2b for the rest of the session.");
                    return null;
                }
            }

            if (string.IsNullOrEmpty(iconUrl)) return null;

            // Phase 3: download.
            using var resp = await _httpClient.GetAsync(iconUrl, linked.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            var bytes = await resp.Content.ReadAsByteArrayAsync(linked.Token).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0) return null;

            return new MemoryStream(bytes, writable: false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"WinGetIconSource: unexpected failure for '{winGetPackageId}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Calls the WinGet COM catalog to resolve a package's manifest icon URL.
    /// Uses the shared <see cref="WinGetComSession"/> (same bootstrap pattern as
    /// <c>WinGetDetectionService</c>) to open the predefined OpenWindowsCatalog,
    /// queries by exact package ID, reads <c>GetCatalogPackageMetadata("en-US").Icons</c>
    /// and returns the first non-empty <c>Icon.Url</c>.
    /// Returns null when the package has no Icons in its manifest (clean miss).
    /// Throws on COM bootstrap or activation failures so the caller can translate to fallback.
    /// </summary>
    private static async Task<string?> GetIconUrlViaComAsync(
        string winGetPackageId,
        CancellationToken ct,
        WinGetComSession comSession,
        ILogService logService)
    {
        if (!comSession.EnsureComInitialized() || comSession.PackageManager == null || comSession.Factory == null)
            throw new InvalidOperationException("WinGet COM session is not initialized.");

        // COM WinRT calls are synchronous under the hood; run on a thread-pool thread
        // so we don't block the calling async context, mirroring WinGetDetectionService.
        return await Task.Run(async () =>
        {
            ct.ThrowIfCancellationRequested();

            // Use the predefined OpenWindowsCatalog (the public winget source).
            // ConnectAsync is available on this interop build and preferred over
            // the synchronous Connect() to avoid blocking thread-pool threads on
            // network I/O during catalog connect.
            var catalogRef = comSession.PackageManager.GetPredefinedPackageCatalog(
                PredefinedPackageCatalog.OpenWindowsCatalog);

            var connectResult = await catalogRef.ConnectAsync().AsTask(ct).ConfigureAwait(false);
            if (connectResult.Status != ConnectResultStatus.Ok)
                throw new InvalidOperationException($"WinGet catalog connect failed: {connectResult.Status}");

            var catalog = connectResult.PackageCatalog;

            // Use the factory to create options/filter objects — the interop types
            // are COM-activated; direct 'new' construction is not supported.
            var findOptions = comSession.Factory.CreateFindPackagesOptions();
            var filter = comSession.Factory.CreatePackageMatchFilter();
            filter.Field = PackageMatchField.Id;
            filter.Option = PackageFieldMatchOption.EqualsCaseInsensitive;
            filter.Value = winGetPackageId;
            findOptions.Filters.Add(filter);

            ct.ThrowIfCancellationRequested();

            // FindPackagesAsync is available and preferred for async contexts.
            var findResult = await catalog.FindPackagesAsync(findOptions).AsTask(ct).ConfigureAwait(false);
            if (findResult.Matches.Count == 0) return null;

            var version = findResult.Matches[0].CatalogPackage?.DefaultInstallVersion;
            if (version is null) return null;

            // GetCatalogPackageMetadata(string preferredLocale) is available in
            // WindowsPackageManager.ComInterop 1.9.25180 (confirmed in generated
            // CsWinRT projections as overload "GetCatalogPackageMetadata2").
            var metadata = version.GetCatalogPackageMetadata("en-US");
            if (metadata is null) return null;

            // CatalogPackageMetadata.Icons is IReadOnlyList<Icon>; Icon.Url is string.
            var icons = metadata.Icons;
            if (icons is null || icons.Count == 0) return null;

            foreach (var icon in icons)
            {
                if (!string.IsNullOrEmpty(icon.Url))
                    return icon.Url;
            }
            return null;
        }, ct).ConfigureAwait(false);
    }
}

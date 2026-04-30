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
/// Layer 2b orchestration: resolves WinGet package icons by querying the local
/// WinGet COM catalog (fast, in-process, no network rate limit). When COM is
/// unavailable, throws, or its package metadata API can't surface an icon URL,
/// falls back to a small in-process override map (see
/// <see cref="WinGetIconUrlOverrides"/>) of vendor-canonical icon URLs. There
/// is no GitHub manifest fetcher path — that was removed because the GitHub
/// Contents API rate-limits unauthenticated callers and the fallback added more
/// failure modes than it resolved.
/// </summary>
public class WinGetIconSource : IWinGetIconSource
{
    private static readonly TimeSpan PerCallTimeout = TimeSpan.FromSeconds(8);

    private readonly IWinGetBootstrapper _bootstrapper;
    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;
    private readonly Func<string, CancellationToken, Task<string?>> _comIconUrlsAsync;
    private readonly Func<string, string?> _overrideLookup;

    // True after the first COM call observed a hard failure (typically E_NOINTERFACE
    // when the bundled WindowsPackageManager.ComInterop projection's metadata
    // overload isn't actually implemented by the installed winget COM server).
    // Once tripped, we skip COM for the rest of the session and go straight to the
    // override-map fallback — saves ~30+ identical warnings per app load.
    // volatile: read+written across thread-pool threads serving concurrent Layer 2
    // calls under the resolver's SemaphoreSlim(5). Write-once (false → true), so
    // volatile is sufficient — no Interlocked needed.
    private volatile bool _comMetadataUnavailable;

    /// <summary>Production constructor — wires the COM path to the real session-based catalog call
    /// and the override lookup to the static <see cref="WinGetIconUrlOverrides"/> map.</summary>
    public WinGetIconSource(
        IWinGetBootstrapper bootstrapper,
        HttpClient httpClient,
        ILogService logService,
        WinGetComSession comSession)
        : this(bootstrapper, httpClient, logService,
               comIconUrlsAsync: (id, ct) => GetIconUrlViaComAsync(id, ct, comSession, logService),
               overrideLookup: id => WinGetIconUrlOverrides.TryGet(id, out var u) ? u : null)
    {
    }

    /// <summary>Test constructor — accepts a fake COM callable and a fake override lookup for unit tests.</summary>
    internal WinGetIconSource(
        IWinGetBootstrapper bootstrapper,
        HttpClient httpClient,
        ILogService logService,
        Func<string, CancellationToken, Task<string?>> comIconUrlsAsync,
        Func<string, string?> overrideLookup)
    {
        _bootstrapper = bootstrapper;
        _httpClient = httpClient;
        _logService = logService;
        _comIconUrlsAsync = comIconUrlsAsync;
        _overrideLookup = overrideLookup;
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

            // Phase 1: COM (skipped if bootstrap reports unavailable, or if a prior
            // call this session already determined the metadata API is missing).
            if (_bootstrapper.IsSystemWinGetAvailable && !_comMetadataUnavailable)
            {
                try
                {
                    iconUrl = await _comIconUrlsAsync(winGetPackageId, linked.Token).ConfigureAwait(false);
                    if (iconUrl is null)
                        comCleanMiss = true; // COM ran, found the package, but no Icons in manifest
                }
                catch (Exception ex)
                {
                    // First failure: log loudly with the cause, then trip the session flag
                    // so subsequent entries skip COM silently. Net log volume drops from
                    // one-warning-per-entry to one-warning-per-session.
                    if (!_comMetadataUnavailable)
                    {
                        _comMetadataUnavailable = true;
                        _logService.LogWarning(
                            $"WinGetIconSource: COM metadata API unavailable ({ex.Message}). " +
                            $"Skipping COM for the remainder of the session; using override-map fallback only.");
                    }
                    // iconUrl stays null, comCleanMiss stays false → fall through to override below
                }
            }

            // Phase 2: override-map fallback.
            // Consult the override map when:
            //   - COM was skipped (WinGet unavailable / metadata API broken), OR
            //   - COM threw (exception path, not a clean null return)
            // Do NOT consult the override when COM returned null cleanly: the manifest
            // exists and authoritatively has no icon, so the override would be guessing.
            if (iconUrl is null && !comCleanMiss && !linked.Token.IsCancellationRequested)
            {
                iconUrl = _overrideLookup(winGetPackageId);
            }

            if (string.IsNullOrEmpty(iconUrl)) return null;

            // Phase 3: download. Use ResponseHeadersRead so we don't buffer the
            // full body before checking the status code — matches StoreIconSource.
            using var request = new HttpRequestMessage(HttpMethod.Get, iconUrl);
            using var resp = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            await using var srcStream = await resp.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            var collector = new MemoryStream();
            await srcStream.CopyToAsync(collector, linked.Token).ConfigureAwait(false);
            if (collector.Length == 0) return null;
            collector.Position = 0;
            return collector;
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
        // We also race the work task against a hard wall-clock timeout via Task.WhenAny
        // — passing the linked-CTS token through AsTask(ct) registers cancellation with
        // the WinRT operation, but if WinRT doesn't honour it we'd otherwise block this
        // thread until the WinRT runtime's internal timeout fires. WinGetDetectionService
        // uses the same pattern; see GetInstalledPackageIdsViaCom for precedent.
        var workTask = Task.Run(async () =>
        {
            ct.ThrowIfCancellationRequested();

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

            var findResult = await catalog.FindPackagesAsync(findOptions).AsTask(ct).ConfigureAwait(false);
            if (findResult.Matches.Count == 0) return (string?)null;

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
        }, ct);

        // Hard wall-clock abandonment via Task.WhenAny — same pattern as
        // WinGetDetectionService.GetInstalledPackageIdsViaCom (lines 133–142).
        // The outer linked-CTS already has CancelAfter(PerCallTimeout) applied,
        // so this Task.Delay completes when ct cancels at the 8s mark.
        var timeoutTask = Task.Delay(PerCallTimeout, ct);
        if (await Task.WhenAny(workTask, timeoutTask).ConfigureAwait(false) == timeoutTask)
        {
            logService.LogWarning($"WinGetIconSource: COM lookup hard timeout for '{winGetPackageId}' — abandoning thread, falling back");
            throw new TimeoutException($"WinGet COM lookup did not complete within {PerCallTimeout.TotalSeconds}s");
        }
        return await workTask.ConfigureAwait(false);
    }
}

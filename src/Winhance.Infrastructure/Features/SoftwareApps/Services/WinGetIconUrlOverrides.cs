using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Layer 2b's static-data fallback for <see cref="WinGetIconSource"/>: maps
/// <c>WinGetPackageId</c> to a vendor icon URL when the local WinGet COM
/// catalog can't surface one.
///
/// <para>
/// <b>Data source:</b> consumes the icon database maintained by the UniGetUI
/// project (originally by Martí Climent, now under the Devolutions
/// organization), MIT-licensed. The database is a community-curated mapping
/// of WinGet package IDs to icon URLs, which is necessary because winget
/// package manifests themselves rarely populate the <c>Icons:</c> schema
/// field — the vast majority of packages ship without an authoritative icon
/// URL in their manifest, so winget GUIs (UniGetUI, and Winhance via this
/// fallback) rely on a community database to fill the gap.
/// </para>
///
/// <para>
/// See <see href="https://github.com/Devolutions/UniGetUI"/> for the project,
/// <see href="https://github.com/Devolutions/UniGetUI/blob/main/WebBasedData/screenshot-database-v2.json"/>
/// for the database itself, and <c>THIRD-PARTY-NOTICES.txt</c> for the
/// upstream MIT license. Icon URLs in the database typically point to
/// <c>i.postimg.cc</c> (where UniGetUI's maintainers host the cropped icons)
/// — that's a transitive dependency you inherit by consuming this database.
/// </para>
///
/// <para>
/// The database is fetched once per session via
/// <c>raw.githubusercontent.com</c> (Fastly-fronted, no GitHub-API rate limit).
/// Failure is fail-soft: warn once, use an empty map for the rest of the
/// session, retry next launch.
/// </para>
///
/// <para>
/// JSON shape (subset we read):
/// <code>
/// {
///   "icons_and_screenshots": {
///     "mozilla.firefox": { "icon": "https://i.postimg.cc/...", "images": [...] }
///   }
/// }
/// </code>
/// We only extract the <c>icon</c> field. Keys are matched case-insensitively
/// (the upstream convention is lowercased package IDs, but we don't rely on
/// it).
/// </para>
/// </summary>
public sealed class WinGetIconUrlOverrides : IWinGetIconUrlOverrides
{
    // UniGetUI's icon database — see the class doc comment for context and
    // licensing. If you fork this URL to a Winhance-controlled mirror, make
    // sure the JSON shape (icons_and_screenshots[id].icon) is preserved.
    private const string DefaultIndexUrl =
        "https://github.com/Devolutions/UniGetUI/raw/refs/heads/main/WebBasedData/screenshot-database-v2.json";

    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(15);

    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;
    private readonly string _indexUrl;

    // Lazy<Task<...>> caches the load attempt so concurrent first-callers share
    // one HTTP fetch, and so a load failure (returning Empty) is sticky for the
    // rest of the session — no retry storms.
    private readonly Lazy<Task<IReadOnlyDictionary<string, string>>> _lazyMap;

    /// <summary>Production constructor — fetches from UniGetUI's canonical database URL.</summary>
    public WinGetIconUrlOverrides(HttpClient httpClient, ILogService logService)
        : this(httpClient, logService, DefaultIndexUrl) { }

    /// <summary>Test constructor — accepts a custom index URL so tests can point at a mocked endpoint.</summary>
    internal WinGetIconUrlOverrides(HttpClient httpClient, ILogService logService, string indexUrl)
    {
        _httpClient = httpClient;
        _logService = logService;
        _indexUrl = indexUrl;
        _lazyMap = new Lazy<Task<IReadOnlyDictionary<string, string>>>(
            LoadAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<string?> TryGetAsync(string winGetPackageId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(winGetPackageId)) return null;
        var map = await _lazyMap.Value.ConfigureAwait(false);

        // Mirror UniGetUI's own lookup order from BasePkgDetailsHelper.GetIcon():
        // 1. Manager-prefixed full ID: "Winget.Mozilla.Firefox"
        // 2. Bare full ID: "Mozilla.Firefox"
        // 3. Normalized icon ID: take everything after the first dot, normalize
        //    separators to hyphens (matches UniGetUI's Package.GenerateIconId()).
        // The vast majority of database entries are keyed under (3); (1) and (2) are
        // for the rare cases where upstream chose a more specific key.
        var managerKey = "Winget." + winGetPackageId;
        if (map.TryGetValue(managerKey, out var url)) return url;
        if (map.TryGetValue(winGetPackageId, out url)) return url;

        var normalized = NormalizeIconId(winGetPackageId);
        if (normalized is not null && map.TryGetValue(normalized, out url)) return url;

        // TEMPORARY: log what we tried so we can identify gaps in upstream's
        // coverage and decide where to add Winhance-local fallback URLs.
        // Drop this block (or downgrade to LogDebug) once gaps are characterized.
        _logService.LogInformation(
            $"WinGetIconUrlOverrides: miss for '{winGetPackageId}' — tried '{managerKey}', '{winGetPackageId}'"
            + (normalized is not null ? $", '{normalized}'" : string.Empty));

        return null;
    }

    /// <summary>
    /// Mirrors UniGetUI's <c>Package.GenerateIconId()</c>: drop the publisher
    /// (everything up to and including the first dot), then convert
    /// <c>_ . space / ,</c> to hyphens. Dictionary lookups are
    /// case-insensitive, so we don't lower-case here. Returns null when the
    /// input has no dot or nothing after the dot.
    /// </summary>
    private static string? NormalizeIconId(string winGetPackageId)
    {
        var dotIndex = winGetPackageId.IndexOf('.');
        if (dotIndex < 0 || dotIndex == winGetPackageId.Length - 1) return null;

        var afterPublisher = winGetPackageId[(dotIndex + 1)..];
        return afterPublisher
            .Replace('_', '-')
            .Replace('.', '-')
            .Replace(' ', '-')
            .Replace('/', '-')
            .Replace(',', '-');
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(FetchTimeout);
            using var resp = await _httpClient.GetAsync(_indexUrl, cts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logService.LogWarning(
                    $"WinGetIconUrlOverrides: UniGetUI icon-database fetch returned HTTP {(int)resp.StatusCode}; using empty fallback for the session.");
                return Empty;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);

            if (!doc.RootElement.TryGetProperty("icons_and_screenshots", out var entriesEl)
                || entriesEl.ValueKind != JsonValueKind.Object)
            {
                _logService.LogWarning(
                    "WinGetIconUrlOverrides: UniGetUI icon-database missing 'icons_and_screenshots' object; using empty fallback.");
                return Empty;
            }

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in entriesEl.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                if (!prop.Value.TryGetProperty("icon", out var iconEl)) continue;
                if (iconEl.ValueKind != JsonValueKind.String) continue;
                var url = iconEl.GetString();
                if (string.IsNullOrEmpty(url)) continue;
                dict[prop.Name] = url;
            }

            _logService.LogInformation($"WinGetIconUrlOverrides: loaded {dict.Count} entries from UniGetUI icon database.");
            return dict;
        }
        catch (Exception ex)
        {
            _logService.LogWarning(
                $"WinGetIconUrlOverrides: UniGetUI icon-database fetch failed ({ex.Message}); using empty fallback for the session.");
            return Empty;
        }
    }
}

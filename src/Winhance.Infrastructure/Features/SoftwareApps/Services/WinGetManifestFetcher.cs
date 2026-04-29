using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using YamlDotNet.RepresentationModel;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Implements the Layer 2b GitHub fallback: list versions via the GitHub Contents
/// API, fetch the raw locale (or singleton) YAML manifest, parse it with
/// YamlDotNet's RepresentationModel, return the first usable icon URL.
/// Memoizes the resolved latest version per package id for the session.
/// </summary>
public class WinGetManifestFetcher : IWinGetManifestFetcher
{
    private const string ContentsApiUrlFormat =
        "https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/{0}";
    private const string RawFileUrlFormat =
        "https://raw.githubusercontent.com/microsoft/winget-pkgs/master/manifests/{0}";

    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;

    // package id (case-insensitive) -> latest version directory name
    private readonly ConcurrentDictionary<string, string> _versionCache = new(StringComparer.OrdinalIgnoreCase);

    public WinGetManifestFetcher(HttpClient httpClient, ILogService logService)
    {
        _httpClient = httpClient;
        _logService = logService;

        // GitHub API requires a User-Agent. Re-use what StoreIconSource sends.
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Winhance", "1.0"));
        }
    }

    private static readonly TimeSpan PerCallTimeout = TimeSpan.FromSeconds(8);

    public async Task<string?> GetIconUrlAsync(string winGetPackageId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(winGetPackageId)) return null;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(PerCallTimeout);

            var dir = BuildManifestDir(winGetPackageId);

            // Step 1: resolve latest version (memoized).
            string? latest;
            if (!_versionCache.TryGetValue(winGetPackageId, out latest))
            {
                latest = await ResolveLatestVersionAsync(dir, linked.Token).ConfigureAwait(false);
                if (latest is not null)
                    _versionCache[winGetPackageId] = latest;
            }
            if (latest is null) return null;

            // Step 2: fetch the locale manifest first; fall through to singleton on 404.
            var localePath = $"{dir}/{latest}/{winGetPackageId}.locale.en-US.yaml";
            var yaml = await FetchRawAsync(localePath, linked.Token).ConfigureAwait(false);
            if (yaml is null)
            {
                var singletonPath = $"{dir}/{latest}/{winGetPackageId}.yaml";
                yaml = await FetchRawAsync(singletonPath, linked.Token).ConfigureAwait(false);
            }
            if (yaml is null) return null;

            return ExtractFirstIconUrl(yaml);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // External cancellation propagates; internal-timeout cancellation falls through
            // to the generic catch and is logged as a normal failure.
            throw;
        }
        catch (RateLimitExceededException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"WinGetManifestFetcher: lookup failed for '{winGetPackageId}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Maps "Microsoft.PowerToys" → "m/Microsoft/PowerToys", "A.B.C" → "a/A/B/C".</summary>
    internal static string BuildManifestDir(string packageId)
    {
        var firstChar = char.ToLowerInvariant(packageId[0]);
        var path = packageId.Replace('.', '/');
        return $"{firstChar}/{path}";
    }

    private async Task<string?> ResolveLatestVersionAsync(string dir, CancellationToken ct)
    {
        var url = string.Format(ContentsApiUrlFormat, dir);
        using var resp = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);

        if (resp.StatusCode == HttpStatusCode.Forbidden &&
            resp.Headers.TryGetValues("X-RateLimit-Remaining", out var values) &&
            values.FirstOrDefault() == "0")
        {
            throw new RateLimitExceededException(resp.Headers);
        }

        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        var entries = await resp.Content.ReadFromJsonAsync<ContentEntry[]>(cancellationToken: ct)
                          .ConfigureAwait(false);
        if (entries is null) return null;

        var versionDirs = entries.Where(e => string.Equals(e.Type, "dir", StringComparison.OrdinalIgnoreCase))
                                 .Select(e => e.Name)
                                 .Where(n => !string.IsNullOrEmpty(n))
                                 .ToList();
        if (versionDirs.Count == 0) return null;

        // Transitive sort: parseable versions sort to the end (so "latest" is the
        // last element after sorting). Non-parseable versions (e.g. "2.1-beta",
        // git SHAs) sort BEFORE all parseable ones, ordered lexically among
        // themselves. This means stable releases always beat prerelease-style
        // names — which matches winget-pkgs convention where prerelease entries
        // are stored as separate version directories alongside stable ones.
        versionDirs.Sort((a, b) =>
        {
            var aParsed = Version.TryParse(a, out var va);
            var bParsed = Version.TryParse(b, out var vb);
            if (aParsed && bParsed) return va!.CompareTo(vb);
            if (aParsed) return 1;   // a is parseable, b isn't → a is "later"
            if (bParsed) return -1;  // b is parseable, a isn't → b is "later"
            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        });
        return versionDirs[^1];
    }

    private async Task<string?> FetchRawAsync(string repoPath, CancellationToken ct)
    {
        var url = string.Format(RawFileUrlFormat, repoPath);
        using var resp = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private static string? ExtractFirstIconUrl(string yaml)
    {
        try
        {
            using var reader = new System.IO.StringReader(yaml);
            var stream = new YamlStream();
            stream.Load(reader);
            if (stream.Documents.Count == 0) return null;

            var root = stream.Documents[0].RootNode as YamlMappingNode;
            if (root is null) return null;

            if (!root.Children.TryGetValue(new YamlScalarNode("Icons"), out var iconsNode)
                || iconsNode is not YamlSequenceNode icons
                || icons.Children.Count == 0)
            {
                return null;
            }

            foreach (var entry in icons.Children.OfType<YamlMappingNode>())
            {
                if (entry.Children.TryGetValue(new YamlScalarNode("IconUrl"), out var urlNode)
                    && urlNode is YamlScalarNode urlScalar
                    && !string.IsNullOrWhiteSpace(urlScalar.Value))
                {
                    return urlScalar.Value;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ContentEntry
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
    }

    public sealed class RateLimitExceededException : Exception
    {
        public DateTimeOffset? ResetAt { get; }

        public RateLimitExceededException(HttpResponseHeaders headers)
            : base("GitHub Contents API rate limit exceeded for the current IP/auth state.")
        {
            if (headers.TryGetValues("X-RateLimit-Reset", out var values)
                && long.TryParse(values.FirstOrDefault(), out var unixSeconds))
            {
                ResetAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
        }
    }
}

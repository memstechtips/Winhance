using System.Text.Json;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class IconManifestService(HttpClient httpClient, ILogService logService) : IIconManifestService
{
    private const string ManifestUrl =
        "https://cdn.jsdelivr.net/gh/memstechtips/package-icons@main/manifest.json";
    private const string UserAgent = "Winhance/1.0 (+https://github.com/memstechtips/Winhance)";

    private Dictionary<string, string>? _shas; // path-relative-to-icons/ -> sha256

    private readonly object _loadGate = new();
    private Task<bool>? _loadTask;

    // Both the eager Windows-apps batch and the background External-apps batch call this; concurrent callers share
    // the single in-flight fetch, so manifest.json is fetched once. A failed load is NOT cached - the next call
    // retries (connectivity restored between batches). LoadCoreAsync swallows all exceptions, so the shared task never faults.
    public Task<bool> LoadAsync(CancellationToken ct = default)
    {
        lock (_loadGate)
        {
            var existing = _loadTask;
            if (existing is not null && (!existing.IsCompleted || existing.Result))
                return existing;
            return _loadTask = LoadCoreAsync(ct);
        }
    }

    private async Task<bool> LoadCoreAsync(CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ManifestUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            using var resp = await httpClient.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                logService.LogWarning($"IconManifest: HTTP {(int)resp.StatusCode}");
                return false;
            }
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("icons", out var icons) || icons.ValueKind != JsonValueKind.Object)
                return false;
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in icons.EnumerateObject())
                if (prop.Value.TryGetProperty("sha256", out var sha) && sha.ValueKind == JsonValueKind.String)
                    map[prop.Name] = sha.GetString()!;
            _shas = map;
            logService.LogInformation($"IconManifest: loaded {map.Count} entries");
            return true;
        }
        catch (Exception ex)
        {
            logService.LogWarning($"IconManifest: load failed: {ex.Message}");
            return false;
        }
    }

    public string? Sha256For(string repoPath)
    {
        if (_shas is null) return null;
        const string prefix = "icons/";
        var key = repoPath.StartsWith(prefix, StringComparison.Ordinal) ? repoPath[prefix.Length..] : repoPath;
        return _shas.TryGetValue(key, out var sha) ? sha : null;
    }
}

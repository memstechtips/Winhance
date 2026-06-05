using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class IconManifestService(HttpClient httpClient, ILogService logService) : IIconManifestService
{
    private const string ManifestUrl =
        "https://cdn.jsdelivr.net/gh/memstechtips/package-icons@main/manifest.json";
    private const string UserAgent = "Winhance/1.0 (+https://github.com/memstechtips/Winhance)";

    private Dictionary<string, string>? _shas; // path-relative-to-icons/ -> sha256

    public async Task<bool> LoadAsync(CancellationToken ct = default)
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

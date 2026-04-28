using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Fetches Microsoft Store app icons via the public displaycatalog API. Used
/// as a Layer-2 fallback when AppxIconSource cannot resolve an icon (the
/// package is neither installed nor provisioned on this machine).
/// </summary>
public class StoreIconSource(HttpClient httpClient, ILogService logService) : IStoreIconSource
{
    private const string DisplayCatalogUrlFormat =
        "https://displaycatalog.mp.microsoft.com/v7.0/products/{0}?market=US&languages=en-US";

    private const string UserAgent = "Winhance/1.0";

    public async Task<Stream?> GetIconStreamAsync(string msStoreId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(msStoreId)) return null;

        try
        {
            using var ctsLinked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ctsLinked.CancelAfter(TimeSpan.FromSeconds(10));

            var imageUrl = await FetchImageUrlAsync(msStoreId, ctsLinked.Token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(imageUrl)) return null;

            return await DownloadImageAsync(imageUrl, msStoreId, ctsLinked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logService.LogWarning($"StoreIconSource: fetch failed for {msStoreId}: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> FetchImageUrlAsync(string msStoreId, CancellationToken ct)
    {
        var apiUrl = string.Format(DisplayCatalogUrlFormat, Uri.EscapeDataString(msStoreId));
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logService.LogWarning($"StoreIconSource: catalog API for {msStoreId} returned {(int)response.StatusCode}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ExtractIconUrl(json);
    }

    private async Task<Stream?> DownloadImageAsync(string imageUrl, string msStoreId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logService.LogWarning($"StoreIconSource: image download for {msStoreId} returned {(int)response.StatusCode}");
            return null;
        }

        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms, ct).ConfigureAwait(false);
        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Picks the best icon URL from the displaycatalog response. Prefers Logo,
    /// then Tile/BoxArt; among candidates of the same priority, picks the
    /// largest square image. Returns null if no suitable image is found.
    /// </summary>
    private static string? ExtractIconUrl(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("Products", out var products) ||
            products.ValueKind != JsonValueKind.Array ||
            products.GetArrayLength() == 0)
            return null;

        var product = products[0];
        if (!product.TryGetProperty("LocalizedProperties", out var localizedArr) ||
            localizedArr.ValueKind != JsonValueKind.Array ||
            localizedArr.GetArrayLength() == 0)
            return null;

        var localized = localizedArr[0];
        if (!localized.TryGetProperty("Images", out var images) ||
            images.ValueKind != JsonValueKind.Array)
            return null;

        string? bestUrl = null;
        int bestPriority = -1;
        int bestSize = 0;

        foreach (var image in images.EnumerateArray())
        {
            if (!image.TryGetProperty("ImagePurpose", out var purposeProp)) continue;
            var priority = purposeProp.GetString() switch
            {
                "Logo" => 3,
                "Tile" => 2,
                "BoxArt" => 1,
                _ => 0,
            };
            if (priority == 0) continue;

            if (!image.TryGetProperty("Uri", out var urlProp) &&
                !image.TryGetProperty("Url", out urlProp))
                continue;

            var url = urlProp.GetString();
            if (string.IsNullOrEmpty(url)) continue;
            if (url.StartsWith("//", StringComparison.Ordinal)) url = "https:" + url;

            int width = image.TryGetProperty("Width", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : 0;
            int height = image.TryGetProperty("Height", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : 0;

            // Skip noticeably non-square assets (Hero banners, screenshots, etc.).
            if (width > 0 && height > 0 && Math.Abs(width - height) > Math.Max(width, height) / 4)
                continue;

            int size = Math.Min(width, height);
            if (priority > bestPriority || (priority == bestPriority && size > bestSize))
            {
                bestUrl = url;
                bestPriority = priority;
                bestSize = size;
            }
        }

        return bestUrl;
    }
}

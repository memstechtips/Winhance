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
/// Fetches Microsoft Store app icons via two public Store APIs:
///
/// 1. displaycatalog.mp.microsoft.com/v7.0/products/{id} — used by the Store
///    SDK for modern "9N..." product IDs. Returns 404 for older "XP..." IDs.
/// 2. storeedgefd.dsx.mp.microsoft.com/v9.0/pages/pdp?productId={id} — used
///    by the Microsoft Store website's product detail pages. Covers both
///    9N... and XP... ID formats.
///
/// Tried in order; the first to return a usable icon URL wins. All failures
/// are logged at Warning and treated as null returns — never throws.
/// </summary>
public class StoreIconSource(HttpClient httpClient, ILogService logService) : IStoreIconSource
{
    private const string DisplayCatalogUrlFormat =
        "https://displaycatalog.mp.microsoft.com/v7.0/products/{0}?market=US&languages=en-US";

    private const string StoreEdgeFdUrlFormat =
        "https://storeedgefd.dsx.mp.microsoft.com/v9.0/pages/pdp?productId={0}&market=US&locale=en-US";

    private const string UserAgent = "Winhance/1.0";

    public async Task<Stream?> GetIconStreamAsync(string msStoreId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(msStoreId)) return null;

        try
        {
            using var ctsLinked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ctsLinked.CancelAfter(TimeSpan.FromSeconds(10));

            // Layer 2a: displaycatalog (fast, well-structured response). Works
            // for 9N... product IDs; returns 404 for XP... IDs.
            var imageUrl = await FetchImageUrlFromDisplayCatalogAsync(msStoreId, ctsLinked.Token).ConfigureAwait(false);

            // Layer 2b: storeedgefd fallback. Used when displaycatalog returns
            // no image — covers XP-prefix legacy product IDs.
            if (string.IsNullOrEmpty(imageUrl))
            {
                imageUrl = await FetchImageUrlFromStoreEdgeFdAsync(msStoreId, ctsLinked.Token).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                logService.LogWarning($"StoreIconSource: no image URL for {msStoreId} from either endpoint");
                return null;
            }

            var stream = await DownloadImageAsync(imageUrl, msStoreId, ctsLinked.Token).ConfigureAwait(false);
            if (stream is not null)
            {
                logService.LogInformation($"StoreIconSource: resolved icon for {msStoreId}");
            }
            return stream;
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

    private async Task<string?> FetchImageUrlFromDisplayCatalogAsync(string msStoreId, CancellationToken ct)
    {
        var apiUrl = string.Format(DisplayCatalogUrlFormat, Uri.EscapeDataString(msStoreId));
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logService.LogWarning($"StoreIconSource: catalog API for {msStoreId} returned {(int)response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ExtractIconUrlFromDisplayCatalog(json);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"StoreIconSource: catalog API call for {msStoreId} threw: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> FetchImageUrlFromStoreEdgeFdAsync(string msStoreId, CancellationToken ct)
    {
        var apiUrl = string.Format(StoreEdgeFdUrlFormat, Uri.EscapeDataString(msStoreId));
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logService.LogWarning($"StoreIconSource: storeedgefd API for {msStoreId} returned {(int)response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ExtractIconUrlFromStoreEdgeFd(json);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"StoreIconSource: storeedgefd API call for {msStoreId} threw: {ex.Message}");
            return null;
        }
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
    /// Extracts an icon URL from the displaycatalog v7 response shape:
    /// <c>Products[].LocalizedProperties[].Images[].Uri</c> with an
    /// <c>ImagePurpose</c> of "Logo", "Tile", or "BoxArt".
    /// </summary>
    private static string? ExtractIconUrlFromDisplayCatalog(string json)
    {
        try
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

            return PickBestImageUrl(images, purposeFieldName: "ImagePurpose", urlFieldName: "Uri");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts an icon URL from the storeedgefd v9 PDP response shape.
    /// Tries the documented Payload.Images[] path first; falls back to a
    /// permissive walk that picks any Images array under any nested object.
    /// The endpoint's response shape has shifted historically, so the
    /// fallback insulates against minor schema drift.
    /// </summary>
    private static string? ExtractIconUrlFromStoreEdgeFd(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Documented path: { "Payload": { "Images": [...] } }
            if (root.TryGetProperty("Payload", out var payload) &&
                payload.TryGetProperty("Images", out var payloadImages) &&
                payloadImages.ValueKind == JsonValueKind.Array)
            {
                var picked = PickBestImageUrl(payloadImages, purposeFieldName: "ImageType", urlFieldName: "Url")
                          ?? PickBestImageUrl(payloadImages, purposeFieldName: "ImagePurpose", urlFieldName: "Url")
                          ?? PickBestImageUrl(payloadImages, purposeFieldName: "ImageType", urlFieldName: "Uri");
                if (!string.IsNullOrEmpty(picked)) return picked;
            }

            // Fallback: walk every Images array we find anywhere in the tree.
            return WalkForFirstImage(root);
        }
        catch
        {
            return null;
        }
    }

    private static string? WalkForFirstImage(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (prop.NameEquals("Images") && prop.Value.ValueKind == JsonValueKind.Array)
                {
                    var url = PickBestImageUrl(prop.Value, "ImageType", "Url")
                           ?? PickBestImageUrl(prop.Value, "ImagePurpose", "Url")
                           ?? PickBestImageUrl(prop.Value, "ImageType", "Uri")
                           ?? PickBestImageUrl(prop.Value, "ImagePurpose", "Uri");
                    if (!string.IsNullOrEmpty(url)) return url;
                }
                var nested = WalkForFirstImage(prop.Value);
                if (!string.IsNullOrEmpty(nested)) return nested;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                var nested = WalkForFirstImage(item);
                if (!string.IsNullOrEmpty(nested)) return nested;
            }
        }
        return null;
    }

    /// <summary>
    /// Selects the best icon URL from an Images array. Prefers Logo, then
    /// Tile/BoxArt; among same-priority candidates, prefers the largest
    /// roughly-square image.
    /// </summary>
    private static string? PickBestImageUrl(JsonElement images, string purposeFieldName, string urlFieldName)
    {
        if (images.ValueKind != JsonValueKind.Array) return null;

        string? bestUrl = null;
        int bestPriority = -1;
        int bestSize = 0;

        foreach (var image in images.EnumerateArray())
        {
            if (image.ValueKind != JsonValueKind.Object) continue;

            int priority = 0;
            if (image.TryGetProperty(purposeFieldName, out var purposeProp) && purposeProp.ValueKind == JsonValueKind.String)
            {
                priority = purposeProp.GetString() switch
                {
                    "Logo" => 3,
                    "AppIcon" => 3,
                    "Tile" => 2,
                    "TileMedium" => 2,
                    "BoxArt" => 1,
                    "Poster" => 1,
                    _ => 0,
                };
            }
            if (priority == 0) continue;

            if (!image.TryGetProperty(urlFieldName, out var urlProp) || urlProp.ValueKind != JsonValueKind.String)
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

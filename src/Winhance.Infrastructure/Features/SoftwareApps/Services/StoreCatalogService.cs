using System.Runtime.InteropServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

// winget's msstore source reports every package as Version Unknown, so the Store's own catalog is the only place a
// Store-only app's newest version can be read. One compressed request covers every app; the endpoint is anonymous but
// undocumented, so any failure means no answers rather than a guess.
internal sealed class StoreCatalogService(HttpClient httpClient, IWindowsRegistryService registry, ILogService logService) : IStoreCatalogService
{
    private const string Endpoint = "https://displaycatalog.mp.microsoft.com/v7.0/products";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyDictionary<string, StoreCatalogUpdate>> GetUpdatesAsync(IReadOnlyList<StoreCatalogApp> apps, CancellationToken cancellationToken = default)
    {
        var updates = new Dictionary<string, StoreCatalogUpdate>(StringComparer.OrdinalIgnoreCase);
        if (apps.Count == 0)
            return updates;

        try
        {
            var installed = InstalledStoreVersions.Read(registry);
            var ids = string.Join(',', apps.Select(a => a.StoreId).Distinct(StringComparer.OrdinalIgnoreCase));
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(RequestTimeout);

            using var response = await httpClient.GetAsync($"{Endpoint}?bigIds={ids}&market=US&languages=en-us", limit.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logService.LogWarning($"Store catalog answered HTTP {(int)response.StatusCode}");
                return updates;
            }

            await using var body = await response.Content.ReadAsStreamAsync(limit.Token).ConfigureAwait(false);
            var available = StoreCatalogPackages.PickVersionByFamily(body, OsArchitecture, PackedVersion.FromVersion(Environment.OSVersion.Version));
            foreach (var app in apps)
            {
                if (available.TryGetValue(app.FamilyName, out var pick) && installed.TryGetValue(app.FamilyName, out var current) && pick > current)
                    updates[app.FamilyName] = new StoreCatalogUpdate(PackedVersion.ToDotted(current), PackedVersion.ToDotted(pick));
            }

            return updates;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Store catalog check failed: {ex.Message}");
            return new Dictionary<string, StoreCatalogUpdate>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string OsArchitecture => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
}

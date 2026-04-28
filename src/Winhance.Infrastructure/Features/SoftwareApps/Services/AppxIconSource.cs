using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Management.Deployment;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppxIconSource(ILogService logService) : IAppxIconSource
{
    public async Task<IReadOnlyDictionary<string, string>> GetInstalledPackageMapAsync(
        CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(15));

            await Task.Run(() =>
            {
                var packageManager = new PackageManager();
                foreach (var package in packageManager.FindPackagesForUser(""))
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    try
                    {
                        // Last writer wins for duplicate names (architecture-specific duplicates are rare)
                        map[package.Id.Name] = package.Id.FullName;
                    }
                    catch (Exception ex)
                    {
                        logService.LogWarning($"Skipped package during icon-source enumeration: {ex.Message}");
                    }
                }
            }, linkedCts.Token).ConfigureAwait(false);

            logService.LogInformation($"AppxIconSource: enumerated {map.Count} installed packages");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logService.LogWarning("AppxIconSource enumeration timed out after 15s — returning what was collected");
        }
        catch (Exception ex)
        {
            logService.LogWarning($"AppxIconSource enumeration failed: {ex.Message}");
        }

        return map;
    }

    public async Task<Stream?> GetLogoStreamAsync(
        string packageFullName,
        Size size,
        CancellationToken ct = default)
    {
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(5));

            return await Task.Run(async () =>
            {
                var packageManager = new PackageManager();
                Windows.ApplicationModel.Package? pkg = null;
                foreach (var p in packageManager.FindPackagesForUser(""))
                {
                    if (string.Equals(p.Id.FullName, packageFullName, StringComparison.OrdinalIgnoreCase))
                    {
                        pkg = p;
                        break;
                    }
                }
                if (pkg is null) return null;

                var entries = await pkg.GetAppListEntriesAsync();
                if (entries is null || entries.Count == 0) return null;

                var streamRef = entries[0].DisplayInfo.GetLogo(size);
                if (streamRef is null) return null;

                using var randomStream = await streamRef.OpenReadAsync();
                var ms = new MemoryStream();
                using (var input = randomStream.AsStreamForRead())
                {
                    await input.CopyToAsync(ms, linkedCts.Token).ConfigureAwait(false);
                }
                ms.Position = 0;
                return (Stream?)ms;
            }, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logService.LogWarning($"AppxIconSource.GetLogoStreamAsync failed for {packageFullName}: {ex.Message}");
            return null;
        }
    }
}

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

// Note: this file does not `using Windows.ApplicationModel;` because that namespace's
// `Package` type would clash with NuGet/MSBuild concepts elsewhere in the project. The
// `Package` references below are fully qualified for that reason.

public class AppxIconSource(ILogService logService) : IAppxIconSource
{
    // Snapshot of installed packages keyed by FullName. Populated by
    // GetInstalledPackageMapAsync; consumed by GetLogoStreamAsync to avoid
    // re-enumerating PackageManager.FindPackagesForUser per icon (~60+ icons
    // per cold-cache run). Replaced atomically on each enumeration call so
    // the cache is fresh per batch.
    private IReadOnlyDictionary<string, Windows.ApplicationModel.Package> _packagesByFullName =
        new Dictionary<string, Windows.ApplicationModel.Package>(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyDictionary<string, string>> GetInstalledPackageMapAsync(
        CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var packageDict = new Dictionary<string, Windows.ApplicationModel.Package>(StringComparer.OrdinalIgnoreCase);

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
                        packageDict[package.Id.FullName] = package;
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

        // Atomic snapshot replacement — readers in GetLogoStreamAsync see either
        // the old dict or the new one, never a partially-populated state.
        _packagesByFullName = packageDict;
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
                var snapshot = _packagesByFullName;
                Windows.ApplicationModel.Package? pkg = null;

                if (snapshot.TryGetValue(packageFullName, out var cachedPkg))
                {
                    pkg = cachedPkg;
                }
                else
                {
                    // Cache miss — fall back to a fresh enumeration. Happens when
                    // GetLogoStreamAsync is called without a prior GetInstalledPackageMapAsync,
                    // or when the requested package isn't in the most recent snapshot.
                    var packageManager = new PackageManager();
                    foreach (var p in packageManager.FindPackagesForUser(""))
                    {
                        if (string.Equals(p.Id.FullName, packageFullName, StringComparison.OrdinalIgnoreCase))
                        {
                            pkg = p;
                            break;
                        }
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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
    // Snapshot of packages keyed by FullName. Populated by GetInstalledPackageMapAsync;
    // consumed by GetLogoStreamAsync to avoid re-enumerating PackageManager per icon
    // and to retain references to provisioned-only packages (which carry an
    // InstalledPath we can read manifest XML from even when no user has them registered).
    private IReadOnlyDictionary<string, Windows.ApplicationModel.Package> _packagesByFullName =
        new Dictionary<string, Windows.ApplicationModel.Package>(StringComparer.OrdinalIgnoreCase);

    private static readonly XNamespace UapNs = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

    public async Task<IReadOnlyDictionary<string, string>> GetInstalledPackageMapAsync(
        CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var packageDict = new Dictionary<string, Windows.ApplicationModel.Package>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(20));

            await Task.Run(() =>
            {
                var packageManager = new PackageManager();

                // Layer 1: current user's registered packages. Cheapest, most common.
                EnumerateInto(packageManager.FindPackagesForUser(""), map, packageDict, linkedCts.Token);

                // Layer 2: all users on the machine. Catches packages another user
                // has registered, including system-apps that the current user has
                // uninstalled but that remain registered system-wide.
                try
                {
                    EnumerateInto(packageManager.FindPackages(), map, packageDict, linkedCts.Token);
                }
                catch (Exception ex)
                {
                    logService.LogWarning($"AppxIconSource: FindPackages (all users) unavailable, skipping: {ex.Message}");
                }

                // Layer 3: provisioned templates. Catches Microsoft system apps that
                // ship with Windows but no user has registered (e.g., the user removed
                // Calculator on a single-user machine — its provisioned template
                // remains, files still on disk, manifest still readable).
                try
                {
                    EnumerateInto(packageManager.FindProvisionedPackages(), map, packageDict, linkedCts.Token);
                }
                catch (Exception ex)
                {
                    logService.LogWarning($"AppxIconSource: FindProvisionedPackages unavailable (likely not elevated), skipping: {ex.Message}");
                }
            }, linkedCts.Token).ConfigureAwait(false);

            logService.LogInformation($"AppxIconSource: enumerated {map.Count} packages across user/all-users/provisioned scopes");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logService.LogWarning("AppxIconSource enumeration timed out after 20s — returning what was collected");
        }
        catch (Exception ex)
        {
            logService.LogWarning($"AppxIconSource enumeration failed: {ex.Message}");
        }

        _packagesByFullName = packageDict;
        return map;
    }

    private void EnumerateInto(
        IEnumerable<Windows.ApplicationModel.Package> packages,
        Dictionary<string, string> map,
        Dictionary<string, Windows.ApplicationModel.Package> packageDict,
        CancellationToken ct)
    {
        foreach (var package in packages)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Don't overwrite a package we already have — earlier layers (current
                // user) win because their AppListEntries reliably work; provisioned-only
                // packages come last and only fill in gaps.
                if (!packageDict.ContainsKey(package.Id.FullName))
                {
                    packageDict[package.Id.FullName] = package;
                }
                if (!map.ContainsKey(package.Id.Name))
                {
                    map[package.Id.Name] = package.Id.FullName;
                }
            }
            catch (Exception ex)
            {
                logService.LogWarning($"Skipped package during icon-source enumeration: {ex.Message}");
            }
        }
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
                var pkg = ResolvePackage(packageFullName);
                if (pkg is null) return null;

                // Path 1: AppListEntry + DisplayInfo.GetLogo. Works for any package
                // with at least one registered user. Returns the locale-appropriate
                // logo at the requested size.
                var entries = await pkg.GetAppListEntriesAsync();
                if (entries is { Count: > 0 })
                {
                    var streamRef = entries[0].DisplayInfo.GetLogo(size);
                    if (streamRef is not null)
                    {
                        return await CopyStreamRefToMemory(streamRef, linkedCts.Token).ConfigureAwait(false);
                    }
                }

                // Path 2: manifest fallback. For provisioned-only packages,
                // GetAppListEntriesAsync returns empty because no user has the
                // package registered. Read the AppxManifest.xml directly to find
                // the Square44x44Logo asset and open it as a file stream.
                return ReadLogoFromManifest(pkg);
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

    private Windows.ApplicationModel.Package? ResolvePackage(string packageFullName)
    {
        var snapshot = _packagesByFullName;
        if (snapshot.TryGetValue(packageFullName, out var cachedPkg))
        {
            return cachedPkg;
        }

        // Cold-path fallback when GetLogoStreamAsync is called without prior enumeration.
        try
        {
            var packageManager = new PackageManager();
            foreach (var p in packageManager.FindPackagesForUser(""))
            {
                if (string.Equals(p.Id.FullName, packageFullName, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
        }
        catch { /* best-effort */ }

        return null;
    }

    private static async Task<Stream?> CopyStreamRefToMemory(
        Windows.Storage.Streams.RandomAccessStreamReference streamRef,
        CancellationToken ct)
    {
        using var randomStream = await streamRef.OpenReadAsync();
        var ms = new MemoryStream();
        using (var input = randomStream.AsStreamForRead())
        {
            await input.CopyToAsync(ms, ct).ConfigureAwait(false);
        }
        ms.Position = 0;
        return ms;
    }

    private Stream? ReadLogoFromManifest(Windows.ApplicationModel.Package pkg)
    {
        try
        {
            var installedPath = pkg.InstalledPath;
            if (string.IsNullOrEmpty(installedPath) || !Directory.Exists(installedPath))
                return null;

            var manifestPath = Path.Combine(installedPath, "AppxManifest.xml");
            if (!File.Exists(manifestPath))
                return null;

            var doc = XDocument.Load(manifestPath);

            // Look for the first <uap:VisualElements> inside any <Application>.
            // Square44x44Logo is the app-list logo (smallest, least decorative padding);
            // fall back to Square150x150Logo, then Properties/Logo.
            var visual = doc.Descendants(UapNs + "VisualElements").FirstOrDefault();
            string? relativeLogo =
                (string?)visual?.Attribute("Square44x44Logo") ??
                (string?)visual?.Attribute("Square150x150Logo");

            if (string.IsNullOrEmpty(relativeLogo))
            {
                relativeLogo = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "Logo")?.Value;
            }

            if (string.IsNullOrEmpty(relativeLogo))
                return null;

            var resolvedPath = ResolveScaledLogo(installedPath, relativeLogo);
            if (resolvedPath is null)
                return null;

            return new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"AppxIconSource: manifest fallback failed for {pkg.Id.FullName}: {ex.Message}");
            return null;
        }
    }

    private static string? ResolveScaledLogo(string installedPath, string relativeLogo)
    {
        // The manifest lists a base name (e.g. "Assets\Square44x44Logo.png"). On disk
        // the actual files have scale or targetsize qualifiers in their names. Pick
        // the highest-quality variant available.
        var dir = Path.GetDirectoryName(Path.Combine(installedPath, relativeLogo));
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

        var baseName = Path.GetFileNameWithoutExtension(relativeLogo);
        var ext = Path.GetExtension(relativeLogo);

        var candidates = new[]
        {
            $"{baseName}.scale-400{ext}",
            $"{baseName}.scale-200{ext}",
            $"{baseName}.scale-150{ext}",
            $"{baseName}.scale-125{ext}",
            $"{baseName}.scale-100{ext}",
            $"{baseName}.targetsize-256{ext}",
            $"{baseName}.targetsize-96{ext}",
            $"{baseName}.targetsize-48{ext}",
            $"{baseName}{ext}",
        };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(dir, candidate);
            if (File.Exists(path)) return path;
        }
        return null;
    }
}

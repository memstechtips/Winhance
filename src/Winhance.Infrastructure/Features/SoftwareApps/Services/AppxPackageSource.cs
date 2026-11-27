using Windows.Management.Deployment;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class AppxPackageSource(
    ILogService logService,
    IPowerShellRunner powerShellRunner,
    IWmiApi wmiApi,
    IInteractiveUserService interactiveUser) : IAppxPackageSource
{
    // Store apps are registered per user. Elevated with another admin's password (OTS), Winhance runs as that admin,
    // but the cards are for the person signed in, whose apps can differ by dozens.
    private const int ElementNotFoundHResult = unchecked((int)0x80070490);

    private string PackageUser => interactiveUser.IsOtsElevation ? interactiveUser.InteractiveUserSid ?? "" : "";

    public async Task<HashSet<string>> GetInstalledPackageNamesAsync(CancellationToken cancellationToken = default)
    {
        var packageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(15));
            await Task.Run(() =>
            {
                var packageManager = new PackageManager();
                foreach (var package in packageManager.FindPackagesForUser(PackageUser))
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    try
                    {
                        packageNames.Add(package.Id.Name);
                    }
                    catch (Exception ex)
                    {
                        logService.LogWarning($"PackageManager enumeration skipped an entry: {ex.Message}");
                    }
                }
            }, linkedCts.Token).ConfigureAwait(false);

            logService.LogInformation($"AppX detection via PackageManager: found {packageNames.Count} packages");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logService.LogWarning("PackageManager enumeration timed out after 15s, falling back to WMI");

            return await FallBackAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"PackageManager failed ({ex.Message}), falling back to WMI");

            return await FallBackAsync().ConfigureAwait(false);
        }

        return packageNames;
    }

    public async Task<IReadOnlyList<StorePackage>> GetStorePackagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                var packages = new List<StorePackage>();
                foreach (var package in new PackageManager().FindPackagesForUser(PackageUser))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        packages.Add(Describe(package));
                    }
                    catch (Exception ex)
                    {
                        logService.LogWarning($"PackageManager enumeration skipped an entry: {ex.Message}");
                    }
                }
                return packages;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logService.LogWarning($"Store package enumeration failed: {ex.Message}");
            return [];
        }
    }

    // DisplayName comes back empty, or as the raw ms-resource string, when the package's resources cannot be loaded.
    private static StorePackage Describe(Windows.ApplicationModel.Package package)
    {
        var id = package.Id;
        var version = $"{id.Version.Major}.{id.Version.Minor}.{id.Version.Build}.{id.Version.Revision}";
        var displayName = package.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName) || displayName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
            displayName = id.Name;

        return new StorePackage(id.Name, id.FamilyName, displayName, version)
        {
            Publisher = package.PublisherDisplayName,
            InstalledOn = InstalledOn(package),
            IsFramework = package.IsFramework,
            IsResource = package.IsResourcePackage || package.IsBundle,
            IsSystem = package.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.System,
        };
    }

    // InstalledDate throws ERROR_NOT_FOUND for a package the calling account does not have itself, which under OTS
    // is about half of the signed-in user's list. The package is still theirs; only the date is unknown.
    private static DateTime? InstalledOn(Windows.ApplicationModel.Package package)
    {
        try
        {
            var installed = package.InstalledDate;
            return installed.Year > 1601 ? installed.LocalDateTime : null;
        }
        catch (Exception ex) when (ex.HResult == ElementNotFoundHResult)
        {
            return null;
        }
    }

    // WMI cannot be asked for another user's packages, so under OTS only Get-AppxPackage can stand in.
    private async Task<HashSet<string>> FallBackAsync()
    {
        if (!interactiveUser.IsOtsElevation)
        {
            var wmiResult = await GetInstalledAppxPackageNamesViaWmiAsync().ConfigureAwait(false);
            if (wmiResult.Count > 0)
                return wmiResult;

            logService.LogWarning("WMI also returned 0 results, trying Get-AppxPackage");
        }
        return await GetInstalledAppxPackageNamesViaPowerShellAsync().ConfigureAwait(false);
    }

    internal static string GetAppxPackageScript(string packageUser) =>
        packageUser.Length > 0
            ? $"Get-AppxPackage -User '{packageUser}' | Select-Object -ExpandProperty Name"
            : "Get-AppxPackage | Select-Object -ExpandProperty Name";

    internal async Task<HashSet<string>> GetInstalledAppxPackageNamesViaWmiAsync()
    {
        var packageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await Task.Run(() =>
            {
                var programs = wmiApi.Query(WmiScope.Cimv2, "Win32_InstalledStoreProgram", null);
                foreach (var obj in programs)
                {
                    using (obj)
                    {
                        try
                        {
                            var name = obj.Get("Name")?.ToString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                // Name is the package family name (e.g., "Microsoft.BingSearch_8wekyb3d8bbwe")
                                // Extract the package name before the publisher hash to match PackageManager.Id.Name
                                var underscoreIndex = name.IndexOf('_');
                                packageNames.Add(underscoreIndex > 0 ? name[..underscoreIndex] : name);
                            }
                        }
                        catch (Exception ex) { logService.LogDebug($"Failed to read WMI InstalledStoreProgram entry: {ex.Message}"); }
                    }
                }
            }).ConfigureAwait(false);

            logService.LogInformation($"WMI InstalledStoreProgram: found {packageNames.Count} packages");
        }
        catch (Exception ex)
        {
            logService.LogError($"WMI InstalledStoreProgram query also failed: {ex.Message}");
        }

        return packageNames;
    }

    private async Task<HashSet<string>> GetInstalledAppxPackageNamesViaPowerShellAsync()
    {
        var packageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            logService.LogInformation("Fetching installed AppX packages via Get-AppxPackage...");

            var output = await powerShellRunner.RunScriptAsync(GetAppxPackageScript(PackageUser)).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = line.Trim();
                    if (!string.IsNullOrEmpty(name))
                        packageNames.Add(name);
                }
            }

            logService.LogInformation($"Get-AppxPackage: found {packageNames.Count} packages");
        }
        catch (Exception ex)
        {
            logService.LogError($"Get-AppxPackage also failed: {ex.Message}");
        }

        return packageNames;
    }
}

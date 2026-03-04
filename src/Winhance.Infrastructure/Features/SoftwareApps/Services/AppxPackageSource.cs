using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppxPackageSource(
    ILogService logService,
    IPowerShellRunner powerShellRunner) : IAppxPackageSource
{
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
                foreach (var package in packageManager.FindPackagesForUser(""))
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
            // External cancellation — propagate immediately
            throw;
        }
        catch (OperationCanceledException)
        {
            logService.LogWarning("PackageManager enumeration timed out after 15s, falling back to WMI");

            // Tier 2: WMI (Win32_InstalledStoreProgram)
            var wmiResult = await GetInstalledAppxPackageNamesViaWmiAsync().ConfigureAwait(false);
            if (wmiResult.Count > 0)
                return wmiResult;

            // Tier 3: Get-AppxPackage via PowerShell (last resort)
            logService.LogWarning("WMI also returned 0 results, trying Get-AppxPackage");
            return await GetInstalledAppxPackageNamesViaPowerShellAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"PackageManager failed ({ex.Message}), falling back to WMI");

            // Tier 2: WMI (Win32_InstalledStoreProgram)
            var wmiResult = await GetInstalledAppxPackageNamesViaWmiAsync().ConfigureAwait(false);
            if (wmiResult.Count > 0)
                return wmiResult;

            // Tier 3: Get-AppxPackage via PowerShell (last resort)
            logService.LogWarning("WMI also returned 0 results, trying Get-AppxPackage");
            return await GetInstalledAppxPackageNamesViaPowerShellAsync().ConfigureAwait(false);
        }

        return packageNames;
    }

    private async Task<HashSet<string>> GetInstalledAppxPackageNamesViaWmiAsync()
    {
        var packageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_InstalledStoreProgram");
                foreach (var obj in searcher.Get())
                {
                    using (obj)
                    {
                        try
                        {
                            var name = obj["Name"]?.ToString();
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

            var output = await powerShellRunner.RunScriptAsync(
                "Get-AppxPackage | Select-Object -ExpandProperty Name").ConfigureAwait(false);

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

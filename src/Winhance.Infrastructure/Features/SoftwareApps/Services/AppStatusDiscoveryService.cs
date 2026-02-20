using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Management.Deployment;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppStatusDiscoveryService(
    ILogService logService,
    IWinGetService winGetService,
    IChocolateyService chocolateyService,
    IInteractiveUserService interactiveUserService) : IAppStatusDiscoveryService
{
    private HashSet<string>? _cachedWinGetPackageIds;

    public void InvalidateWinGetCache()
    {
        _cachedWinGetPackageIds = null;
    }

    public async Task<Dictionary<string, bool>> GetInstallationStatusBatchAsync(IEnumerable<ItemDefinition> definitions)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var definitionList = definitions.ToList();

        if (!definitionList.Any()) return result;

        try
        {
            var apps = definitionList.Where(d => !string.IsNullOrEmpty(d.AppxPackageName)).ToList();
            var capabilities = definitionList.Where(d => !string.IsNullOrEmpty(d.CapabilityName)).ToList();
            var features = definitionList.Where(d => !string.IsNullOrEmpty(d.OptionalFeatureName)).ToList();

            int capCount = 0, featCount = 0, appxCount = 0, wingetCount = 0;

            if (capabilities.Any())
            {
                var capabilityNames = capabilities.Select(c => c.CapabilityName).ToList();
                var capabilityResults = await CheckCapabilitiesAsync(capabilityNames);
                foreach (var capability in capabilities)
                {
                    if (capabilityResults.TryGetValue(capability.CapabilityName, out bool isInstalled))
                    {
                        result[capability.Id] = isInstalled;
                        if (isInstalled)
                        {
                            capCount++;
                            logService.LogInformation($"Installed (Capability): {capability.Name} ({capability.CapabilityName})");
                        }
                    }
                }
            }

            if (features.Any())
            {
                var featureNames = features.Select(f => f.OptionalFeatureName).ToList();
                var featureResults = await CheckFeaturesAsync(featureNames);
                foreach (var feature in features)
                {
                    if (featureResults.TryGetValue(feature.OptionalFeatureName, out bool isInstalled))
                    {
                        result[feature.Id] = isInstalled;
                        if (isInstalled)
                        {
                            featCount++;
                            logService.LogInformation($"Installed (Feature): {feature.Name} ({feature.OptionalFeatureName})");
                        }
                    }
                }
            }

            if (apps.Any())
            {
                var installedPackageNames = await GetInstalledAppxPackageNamesAsync();
                foreach (var app in apps)
                {
                    if (installedPackageNames.Contains(app.AppxPackageName))
                    {
                        result[app.Id] = true;
                        appxCount++;
                        logService.LogInformation($"Installed (AppX): {app.Name} ({app.AppxPackageName})");
                    }
                }

                // WinGet fallback for apps not found by PackageManager
                var undetectedApps = apps
                    .Where(a => !result.ContainsKey(a.Id) || !result[a.Id])
                    .Where(a => (a.WinGetPackageId != null && a.WinGetPackageId.Any()) || !string.IsNullOrEmpty(a.MsStoreId))
                    .ToList();

                if (undetectedApps.Any())
                {
                    var winGetIds = await GetOrFetchWinGetPackageIdsAsync();
                    if (winGetIds != null)
                    {
                        foreach (var app in undetectedApps)
                        {
                            var matchedById = app.WinGetPackageId?.Any(pkgId => winGetIds.Contains(pkgId)) == true;
                            var matchedByStoreId = !string.IsNullOrEmpty(app.MsStoreId) && winGetIds.Contains(app.MsStoreId);
                            if (matchedById || matchedByStoreId)
                            {
                                result[app.Id] = true;
                                wingetCount++;
                                var matchedId = matchedByStoreId
                                    ? app.MsStoreId!
                                    : app.WinGetPackageId!.First(pkgId => winGetIds.Contains(pkgId));
                                logService.LogInformation($"Installed (WinGet): {app.Name} ({matchedId})");
                            }
                        }
                    }
                }

                // Mark remaining unfound apps as not installed
                foreach (var app in apps)
                {
                    if (!result.ContainsKey(app.Id))
                        result[app.Id] = false;
                }
            }

            var notFoundCount = result.Count(kvp => !kvp.Value);
            logService.LogInformation(
                $"Detection complete: {capCount} via Capability, {featCount} via Feature, {appxCount} via AppX, {wingetCount} via WinGet, {notFoundCount} not found");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking batch installation status", ex);
            return definitionList.ToDictionary(d => d.Id, d => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<Dictionary<string, bool>> GetInstallationStatusByIdAsync(IEnumerable<string> appIds)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var appIdList = appIds.ToList();

        if (!appIdList.Any()) return result;

        try
        {
            var installedPackageNames = await GetInstalledAppxPackageNamesAsync();
            foreach (var appId in appIdList)
            {
                if (installedPackageNames.Contains(appId))
                {
                    result[appId] = true;
                    logService.LogInformation($"Installed (AppX): {appId}");
                }
                else
                {
                    result[appId] = false;
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking installation status by ID", ex);
            return appIdList.ToDictionary(id => id, id => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<Dictionary<string, bool>> CheckCapabilitiesAsync(List<string> capabilities)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            logService.LogInformation($"[DISM-Detect] CheckCapabilitiesAsync: checking {capabilities.Count} capabilities: [{string.Join(", ", capabilities)}]");

            var installedCapabilities = await DismSessionManager.ExecuteAsync<HashSet<string>>(session =>
            {
                DismApi.ThrowIfFailed(
                    DismApi.DismGetCapabilities(session, out IntPtr capPtr, out uint count),
                    "GetCapabilities");
                try
                {
                    var allCaps = DismApi.MarshalArray<DismApi.DISM_CAPABILITY>(capPtr, count);

                    var installed = new HashSet<string>(
                        allCaps.Where(c => c.State == DismApi.DismStateInstalled)
                               .Select(c => Marshal.PtrToStringUni(c.Name)!)
                               .Where(n => n != null),
                        StringComparer.OrdinalIgnoreCase);

                    logService.LogInformation($"[DISM-Detect] DismGetCapabilities: {installed.Count} installed out of {count} total");
                    return installed;
                }
                finally
                {
                    DismApi.DismDelete(capPtr);
                }
            });

            foreach (var capability in capabilities)
            {
                var match = installedCapabilities.Any(c =>
                    c.StartsWith(capability, StringComparison.OrdinalIgnoreCase));
                result[capability] = match;
                logService.LogInformation($"[DISM-Detect] Capability match: '{capability}' => {match}");
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"[DISM-Detect] Error checking capabilities status: {ex.GetType().Name}: {ex.Message}", ex);
            foreach (var capability in capabilities)
                result[capability] = false;
        }

        return result;
    }

    private async Task<Dictionary<string, bool>> CheckFeaturesAsync(List<string> features)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            logService.LogInformation($"[DISM-Detect] CheckFeaturesAsync: checking {features.Count} features: [{string.Join(", ", features)}]");

            var enabledFeatures = await DismSessionManager.ExecuteAsync<HashSet<string>>(session =>
            {
                DismApi.ThrowIfFailed(
                    DismApi.DismGetFeatures(session, null, 0, out IntPtr featPtr, out uint count),
                    "GetFeatures");
                try
                {
                    var allFeatures = DismApi.MarshalArray<DismApi.DISM_FEATURE>(featPtr, count);

                    var enabled = new HashSet<string>(
                        allFeatures.Where(f => f.State == DismApi.DismStateInstalled)
                                   .Select(f => Marshal.PtrToStringUni(f.FeatureName)!)
                                   .Where(n => n != null),
                        StringComparer.OrdinalIgnoreCase);

                    logService.LogInformation($"[DISM-Detect] DismGetFeatures: {enabled.Count} enabled out of {count} total");
                    return enabled;
                }
                finally
                {
                    DismApi.DismDelete(featPtr);
                }
            });

            foreach (var feature in features)
            {
                var match = enabledFeatures.Contains(feature);
                result[feature] = match;
                logService.LogInformation($"[DISM-Detect] Feature match: '{feature}' => {match}");
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"[DISM-Detect] Error checking features status: {ex.GetType().Name}: {ex.Message}", ex);
            foreach (var feature in features)
                result[feature] = false;
        }

        return result;
    }

    private async Task<HashSet<string>> GetInstalledAppxPackageNamesAsync()
    {
        var packageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await Task.Run(() =>
            {
                var packageManager = new PackageManager();
                foreach (var package in packageManager.FindPackagesForUser(""))
                {
                    cts.Token.ThrowIfCancellationRequested();
                    try
                    {
                        packageNames.Add(package.Id.Name);
                    }
                    catch (Exception ex)
                    {
                        logService.LogWarning($"PackageManager enumeration skipped an entry: {ex.Message}");
                    }
                }
            }, cts.Token);

            logService.LogInformation($"AppX detection via PackageManager: found {packageNames.Count} packages");
        }
        catch (OperationCanceledException)
        {
            logService.LogWarning("PackageManager enumeration timed out after 15s, falling back to WMI");

            // Tier 2: WMI (Win32_InstalledStoreProgram)
            var wmiResult = await GetInstalledAppxPackageNamesViaWmiAsync();
            if (wmiResult.Count > 0)
                return wmiResult;

            // Tier 3: Get-AppxPackage via PowerShell (last resort)
            logService.LogWarning("WMI also returned 0 results, trying Get-AppxPackage");
            return await GetInstalledAppxPackageNamesViaPowerShellAsync();
        }
        catch (Exception ex)
        {
            logService.LogWarning($"PackageManager failed ({ex.Message}), falling back to WMI");

            // Tier 2: WMI (Win32_InstalledStoreProgram)
            var wmiResult = await GetInstalledAppxPackageNamesViaWmiAsync();
            if (wmiResult.Count > 0)
                return wmiResult;

            // Tier 3: Get-AppxPackage via PowerShell (last resort)
            logService.LogWarning("WMI also returned 0 results, trying Get-AppxPackage");
            return await GetInstalledAppxPackageNamesViaPowerShellAsync();
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
                    catch { }
                }
            });

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

            var output = await PowerShellRunner.RunScriptAsync(
                "Get-AppxPackage | Select-Object -ExpandProperty Name");

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

    private async Task<HashSet<string>?> GetOrFetchWinGetPackageIdsAsync()
    {
        if (_cachedWinGetPackageIds != null)
            return _cachedWinGetPackageIds;

        try
        {
            bool winGetReady = await winGetService.EnsureWinGetReadyAsync();
            if (!winGetReady)
            {
                logService.LogWarning("WinGet unavailable - skipping WinGet detection");
                return null;
            }

            _cachedWinGetPackageIds = await winGetService.GetInstalledPackageIdsAsync();
            logService.LogInformation($"WinGet: Fetched {_cachedWinGetPackageIds.Count} installed package IDs");
            return _cachedWinGetPackageIds;
        }
        catch (Exception ex)
        {
            logService.LogWarning($"WinGet detection failed: {ex.Message}");
            return null;
        }
    }

    #region External Apps Detection

    public async Task<Dictionary<string, bool>> GetExternalAppsInstallationStatusAsync(IEnumerable<ItemDefinition> definitions)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var definitionList = definitions.ToList();

        if (!definitionList.Any())
            return result;

        try
        {
            int wingetCount = 0, chocoCount = 0, registryCount = 0;

            // Phase 1: WinGet detection for apps with WinGetPackageId or MsStoreId
            var appsWithWinGetId = definitionList
                .Where(d => (d.WinGetPackageId != null && d.WinGetPackageId.Any()) || !string.IsNullOrEmpty(d.MsStoreId))
                .ToList();

            if (appsWithWinGetId.Any())
            {
                var winGetIds = await GetOrFetchWinGetPackageIdsAsync();

                if (winGetIds != null)
                {
                    foreach (var def in appsWithWinGetId)
                    {
                        var matchedById = def.WinGetPackageId?.Any(pkgId => winGetIds.Contains(pkgId)) == true;
                        var matchedByStoreId = !string.IsNullOrEmpty(def.MsStoreId) && winGetIds.Contains(def.MsStoreId);
                        bool isInstalled = matchedById || matchedByStoreId;
                        result[def.Id] = isInstalled;

                        if (isInstalled)
                        {
                            wingetCount++;
                            var matchedPackageId = matchedByStoreId
                                ? def.MsStoreId!
                                : def.WinGetPackageId!.First(pkgId => winGetIds.Contains(pkgId));
                            logService.LogInformation($"Installed (WinGet): {def.Name} ({matchedPackageId})");
                        }
                    }

                    logService.LogInformation($"WinGet: Checked {appsWithWinGetId.Count} apps");
                }
                else
                {
                    logService.LogWarning("WinGet unavailable - cannot check installation status for apps with WinGetPackageId");
                }
            }

            // Phase 2: Chocolatey detection for apps not found by WinGet
            var appsForChocoCheck = definitionList
                .Where(d => !string.IsNullOrEmpty(d.ChocoPackageId)
                    && (!result.ContainsKey(d.Id) || !result[d.Id]))
                .ToList();

            if (appsForChocoCheck.Any())
            {
                try
                {
                    var chocoPackageIds = await chocolateyService.GetInstalledPackageIdsAsync();

                    if (chocoPackageIds.Count > 0)
                    {
                        foreach (var def in appsForChocoCheck)
                        {
                            if (chocoPackageIds.Contains(def.ChocoPackageId!))
                            {
                                result[def.Id] = true;
                                chocoCount++;
                                logService.LogInformation($"Installed (Chocolatey): {def.Name} ({def.ChocoPackageId})");
                            }
                        }

                        logService.LogInformation($"Chocolatey: Checked {appsForChocoCheck.Count} apps");
                    }
                    else
                    {
                        logService.LogInformation("Chocolatey not installed or no packages found - skipping Chocolatey detection");
                    }
                }
                catch (Exception ex)
                {
                    logService.LogWarning($"Chocolatey detection failed: {ex.Message}");
                }
            }

            // Phase 3: Registry fallback for apps not detected via WinGet or Chocolatey
            var appsForRegistryCheck = definitionList
                .Where(d => !result.ContainsKey(d.Id) || !result[d.Id])
                .ToList();

            if (appsForRegistryCheck.Any())
            {
                var (registryKeyNames, registryDisplayNames) = await GetRegistryUninstallInfoAsync();

                // First pass: match by registry key name (more reliable, e.g. "7-Zip")
                foreach (var def in appsForRegistryCheck)
                {
                    if (registryKeyNames.Contains(def.Name))
                    {
                        result[def.Id] = true;
                        registryCount++;
                        logService.LogInformation($"Installed (Registry): {def.Name}");
                    }
                }

                // Second pass: for remaining undetected apps, fall back to DisplayName match
                // (handles MSIs that use GUIDs as key names, e.g. Sniffnet)
                var stillUndetected = appsForRegistryCheck
                    .Where(d => !result.ContainsKey(d.Id) || !result[d.Id])
                    .ToList();

                foreach (var def in stillUndetected)
                {
                    if (registryDisplayNames.Contains(def.Name))
                    {
                        result[def.Id] = true;
                        registryCount++;
                        logService.LogInformation($"Installed (Registry DisplayName): {def.Name}");
                    }
                    else if (!result.ContainsKey(def.Id))
                    {
                        result[def.Id] = false;
                    }
                }
            }

            var totalFound = result.Count(kvp => kvp.Value);
            logService.LogInformation(
                $"Status check complete: {totalFound}/{definitionList.Count} apps installed ({wingetCount} via WinGet, {chocoCount} via Chocolatey, {registryCount} via Registry)");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking installation status: {ex.Message}", ex);
            return definitionList.ToDictionary(d => d.Id, d => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<(HashSet<string> KeyNames, HashSet<string> DisplayNames)> GetRegistryUninstallInfoAsync()
    {
        var keyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var displayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            // OTS: redirect HKCU to HKU\{interactive user SID} so we read
            // the standard user's uninstall keys, not the admin's.
            var hkcuUninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            RegistryKey hkcuHive;
            string hkcuPath;

            if (interactiveUserService.IsOtsElevation && interactiveUserService.InteractiveUserSid != null)
            {
                hkcuHive = Registry.Users;
                hkcuPath = $@"{interactiveUserService.InteractiveUserSid}\{hkcuUninstallPath}";
            }
            else
            {
                hkcuHive = Registry.CurrentUser;
                hkcuPath = hkcuUninstallPath;
            }

            var registryPaths = new[]
            {
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (hkcuHive, hkcuPath)
            };

            foreach (var (hive, path) in registryPaths)
            {
                try
                {
                    using var key = hive.OpenSubKey(path);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var subKey = key.OpenSubKey(subKeyName);
                            if (subKey == null) continue;

                            var systemComponent = subKey.GetValue("SystemComponent");
                            if (systemComponent is int value && value == 1)
                                continue;

                            keyNames.Add(subKeyName);

                            var displayName = subKey.GetValue("DisplayName") as string;
                            if (!string.IsNullOrEmpty(displayName))
                                displayNames.Add(displayName);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        });

        return (keyNames, displayNames);
    }

    #endregion
}

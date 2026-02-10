using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Dism;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppStatusDiscoveryService(
    ILogService logService,
    IWinGetService winGetService) : IAppStatusDiscoveryService
{

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
                            logService.LogInformation($"Installed (Feature): {feature.Name} ({feature.OptionalFeatureName})");
                        }
                    }
                }
            }

            if (apps.Any())
            {
                var installedApps = await GetInstalledStoreAppsAsync();
                foreach (var app in apps)
                {
                    if (installedApps.TryGetValue(app.AppxPackageName, out var detectionMethod))
                    {
                        result[app.Id] = true;
                        logService.LogInformation($"Installed ({detectionMethod}): {app.Name} ({app.AppxPackageName})");
                    }
                    else
                    {
                        result[app.Id] = false;
                    }
                }
            }

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
            var installedApps = await GetInstalledStoreAppsAsync();
            foreach (var appId in appIdList)
            {
                if (installedApps.TryGetValue(appId, out var detectionMethod))
                {
                    result[appId] = true;
                    logService.LogInformation($"Installed ({detectionMethod}): {appId}");
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
            var installedCapabilities = await DismSessionManager.ExecuteAsync<HashSet<string>>(session =>
            {
                var allCaps = DismApi.GetCapabilities(session);
                return new HashSet<string>(
                    allCaps.Where(c => c.State == DismPackageFeatureState.Installed)
                           .Select(c => c.Name),
                    StringComparer.OrdinalIgnoreCase);
            });

            foreach (var capability in capabilities)
            {
                result[capability] = installedCapabilities.Any(c =>
                    c.StartsWith(capability, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking capabilities status", ex);
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
            var enabledFeatures = await DismSessionManager.ExecuteAsync<HashSet<string>>(session =>
            {
                var allFeatures = DismApi.GetFeatures(session);
                return new HashSet<string>(
                    allFeatures.Where(f => f.State == DismPackageFeatureState.Installed)
                               .Select(f => f.FeatureName),
                    StringComparer.OrdinalIgnoreCase);
            });

            foreach (var feature in features)
            {
                result[feature] = enabledFeatures.Contains(feature);
            }
        }
        catch (Exception ex)
        {
            logService.LogError("Error checking features status", ex);
            foreach (var feature in features)
                result[feature] = false;
        }

        return result;
    }

    private async Task<Dictionary<string, string>> GetInstalledStoreAppsAsync()
    {
        var installedApps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_InstalledStoreProgram");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var name = obj["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        installedApps[name] = "WMI";
                    }
                }
            });

            try
            {
                var registryKeys = new[]
                {
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                    Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
                };

                foreach (var uninstallKey in registryKeys)
                {
                    if (uninstallKey == null) continue;

                    using (uninstallKey)
                    {
                        var subKeyNames = uninstallKey.GetSubKeyNames();

                        // OneNote (UWP version) doesn't appear in Win32_InstalledStoreProgram WMI query
                        // so we check registry as a fallback
                        if (subKeyNames.Any(name => name.Contains("OneNote", StringComparison.OrdinalIgnoreCase)))
                        {
                            installedApps["Microsoft.Office.OneNote"] = "Registry";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logService.LogError("Error checking registry for apps", ex);
            }
        }
        catch (Exception ex)
        {
            logService.LogError("Error querying installed apps via WMI", ex);
        }

        return installedApps;
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
            // Phase 1: WinGet detection for apps with WinGetPackageId
            var appsWithWinGetId = definitionList
                .Where(d => d.WinGetPackageId != null && d.WinGetPackageId.Any())
                .ToList();

            if (appsWithWinGetId.Any())
            {
                bool winGetReady = false;
                try
                {
                    winGetReady = await winGetService.EnsureWinGetReadyAsync();
                }
                catch (Exception ex)
                {
                    logService.LogWarning($"WinGet readiness check failed: {ex.Message}");
                }

                if (winGetReady)
                {
                    var wingetPackageIds = await winGetService.GetInstalledPackageIdsAsync();

                    foreach (var def in appsWithWinGetId)
                    {
                        bool isInstalled = def.WinGetPackageId!.Any(pkgId => wingetPackageIds.Contains(pkgId));
                        result[def.Id] = isInstalled;

                        if (isInstalled)
                        {
                            var matchedPackageId = def.WinGetPackageId!.First(pkgId => wingetPackageIds.Contains(pkgId));
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

            // Phase 2: Registry fallback for apps not detected via WinGet
            // Uses registry key names (e.g., "Mozilla Firefox") not DisplayName values
            var appsForRegistryCheck = definitionList
                .Where(d => !result.ContainsKey(d.Id) || !result[d.Id])
                .ToList();

            if (appsForRegistryCheck.Any())
            {
                var registryKeyNames = await GetRegistryUninstallKeyNamesAsync();

                foreach (var def in appsForRegistryCheck)
                {
                    bool isInstalled = registryKeyNames.Contains(def.Name);

                    if (isInstalled)
                    {
                        result[def.Id] = true;
                        logService.LogInformation($"Installed (Registry): {def.Name}");
                    }
                    else if (!result.ContainsKey(def.Id))
                    {
                        result[def.Id] = false;
                    }
                }
            }

            var totalFound = result.Count(kvp => kvp.Value);
            logService.LogInformation($"Status check complete: {totalFound}/{definitionList.Count} apps installed");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking installation status: {ex.Message}", ex);
            return definitionList.ToDictionary(d => d.Id, d => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<HashSet<string>> GetRegistryUninstallKeyNamesAsync()
    {
        var keyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            var registryPaths = new[]
            {
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
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
                        }
                        catch { }
                    }
                }
                catch { }
            }
        });

        return keyNames;
    }

    #endregion
}

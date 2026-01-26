using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppStatusDiscoveryService(
    ILogService logService,
    IPowerShellExecutionService powerShellExecutionService,
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
            var script = "Get-WindowsCapability -Online | Where-Object State -eq 'Installed' | Select-Object -ExpandProperty Name";
            var scriptOutput = await powerShellExecutionService.ExecuteScriptAsync(script);

            if (!string.IsNullOrEmpty(scriptOutput))
            {
                var installedCapabilities = scriptOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var capability in capabilities)
                {
                    result[capability] = installedCapabilities.Any(c =>
                        c.StartsWith(capability, StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                foreach (var capability in capabilities)
                    result[capability] = false;
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
            var script = "Get-WindowsOptionalFeature -Online | Where-Object State -eq 'Enabled' | Select-Object -ExpandProperty FeatureName";
            var scriptOutput = await powerShellExecutionService.ExecuteScriptAsync(script);

            if (!string.IsNullOrEmpty(scriptOutput))
            {
                var enabledFeatures = scriptOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var feature in features)
                {
                    result[feature] = enabledFeatures.Contains(feature);
                }
            }
            else
            {
                foreach (var feature in features)
                    result[feature] = false;
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

                        if (subKeyNames.Any(name => name.Contains("OneNote", StringComparison.OrdinalIgnoreCase)))
                        {
                            installedApps["Microsoft.Office.OneNote"] = "Registry";
                        }

                        if (subKeyNames.Any(name => name.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)))
                        {
                            installedApps["Microsoft.OneDriveSync"] = "Registry";
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
            var appsWithWinGetId = definitionList
                .Where(d => d.WinGetPackageId != null && d.WinGetPackageId.Any())
                .ToList();

            var appsWithoutWinGetId = definitionList
                .Where(d => d.WinGetPackageId == null || !d.WinGetPackageId.Any())
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
                    var wingetPackageIds = await GetInstalledWinGetPackageIdsAsync();

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
                    foreach (var def in appsWithWinGetId)
                    {
                        result[def.Id] = false;
                    }
                }
            }

            if (appsWithoutWinGetId.Any())
            {
                var registryPrograms = await GetInstalledProgramsFromRegistryAsync();

                foreach (var def in appsWithoutWinGetId)
                {
                    bool isInstalled = registryPrograms.Any(p =>
                        p.DisplayName.Equals(def.Name, StringComparison.OrdinalIgnoreCase));

                    result[def.Id] = isInstalled;

                    if (isInstalled)
                    {
                        logService.LogInformation($"Installed (Registry): {def.Name}");
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

    private async Task<HashSet<string>> GetInstalledWinGetPackageIdsAsync()
    {
        var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const int maxRetries = 3;
        const int timeoutMs = 10000; // 10 seconds

        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Winhance", "Cache");
        Directory.CreateDirectory(cacheDir);

        var exportPath = Path.Combine(cacheDir, "winget-packages.json");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(exportPath))
                    File.Delete(exportPath);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"export -o \"{exportPath}\" --accept-source-agreements --nowarn --disable-interactivity",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var completed = await Task.Run(() => process.WaitForExit(timeoutMs));
                if (!completed)
                {
                    try { process.Kill(true); } catch { }
                    logService.LogWarning($"WinGet export timed out after {timeoutMs / 1000} seconds (Attempt {attempt}/{maxRetries})");
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(2000);
                        continue;
                    }
                    return installedPackageIds;
                }

                if (process.ExitCode != 0 || !File.Exists(exportPath))
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    logService.LogWarning($"WinGet export failed with exit code {process.ExitCode} (Attempt {attempt}/{maxRetries}). Error: {error}");
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(2000);
                        continue;
                    }
                    return installedPackageIds;
                }

                var json = await File.ReadAllTextAsync(exportPath);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("Sources", out var sources))
                {
                    foreach (var source in sources.EnumerateArray())
                    {
                        if (source.TryGetProperty("Packages", out var packages))
                        {
                            foreach (var package in packages.EnumerateArray())
                            {
                                if (package.TryGetProperty("PackageIdentifier", out var id))
                                {
                                    var packageId = id.GetString();
                                    if (!string.IsNullOrEmpty(packageId))
                                        installedPackageIds.Add(packageId);
                                }
                            }
                        }
                    }
                }

                logService.LogInformation($"WinGet export: Found {installedPackageIds.Count} installed packages");
                return installedPackageIds; // Success, return results
            }
            catch (Exception ex)
            {
                logService.LogError($"Error running WinGet export (Attempt {attempt}/{maxRetries}): {ex.Message}", ex);
                if (attempt < maxRetries)
                {
                    await Task.Delay(2000);
                    continue;
                }
            }
        }

        return installedPackageIds;
    }


    private async Task<HashSet<(string DisplayName, string Publisher)>> GetInstalledProgramsFromRegistryAsync()
    {
        var installedPrograms = new HashSet<(string, string)>();

        try
        {
            await Task.Run(() => QueryRegistryForInstalledPrograms(installedPrograms));
        }
        catch (Exception ex)
        {
            logService.LogError($"Error querying registry for installed programs: {ex.Message}", ex);
        }

        return installedPrograms;
    }

    private void QueryRegistryForInstalledPrograms(HashSet<(string, string)> installedPrograms)
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
                        if (systemComponent is int systemComponentValue && systemComponentValue == 1)
                            continue;

                        var displayName = subKey.GetValue("DisplayName")?.ToString();
                        var publisher = subKey.GetValue("Publisher")?.ToString();

                        if (!string.IsNullOrEmpty(displayName))
                            installedPrograms.Add((displayName, publisher ?? ""));
                    }
                    catch { }
                }
            }
            catch { }
        }
    }



    #endregion
}

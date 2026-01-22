using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        var definitionList = definitions
            .Where(d => !string.IsNullOrWhiteSpace(d.WinGetPackageId))
            .ToList();

        if (!definitionList.Any())
            return result;

        try
        {
            var remainingToCheck = new List<ItemDefinition>(definitionList);
            var foundByWinGet = 0;

            bool winGetReady = false;
            try
            {
                winGetReady = await winGetService.EnsureWinGetReadyAsync();

                if (!winGetReady)
                {
                    logService.LogInformation("WinGet is not available - skipping WinGet detection, using WMI/Registry only");
                }
            }
            catch (Exception ex)
            {
                logService.LogWarning($"WinGet readiness check failed: {ex.Message}");
                winGetReady = false;
            }

            if (winGetReady)
            {
                var wingetPackageIds = await GetInstalledWinGetPackageIdsAsync();

                foreach (var def in definitionList.ToList())
                {
                    if (wingetPackageIds.Contains(def.WinGetPackageId))
                    {
                        result[def.Id] = true;
                        remainingToCheck.Remove(def);
                        foundByWinGet++;
                        logService.LogInformation($"Installed (WinGet): {def.Name} ({def.WinGetPackageId})");
                    }
                }

                logService.LogInformation($"WinGet: Found {foundByWinGet}/{definitionList.Count} apps installed");
            }

            if (remainingToCheck.Any())
            {
                var wmiTask = GetInstalledProgramsFromWmiOnlyAsync();
                var registryTask = GetInstalledProgramsFromRegistryAsync();

                await Task.WhenAll(wmiTask, registryTask);

                var wmiPrograms = wmiTask.Result;
                var registryPrograms = registryTask.Result;

                var foundByFallback = 0;

                foreach (var def in remainingToCheck.ToList())
                {
                    var wmiMatch = FuzzyMatchProgram(def.WinGetPackageId, wmiPrograms);
                    var registryMatch = FuzzyMatchProgram(def.WinGetPackageId, registryPrograms);

                    if (wmiMatch || registryMatch)
                    {
                        result[def.Id] = true;
                        remainingToCheck.Remove(def);
                        foundByFallback++;
                        var method = wmiMatch && registryMatch ? "WMI+Registry" : (wmiMatch ? "WMI" : "Registry");
                        logService.LogInformation($"Installed ({method}): {def.Name}");
                    }
                }

                logService.LogInformation($"Fallback detection: Found {foundByFallback} via WMI/Registry");
            }

            foreach (var def in remainingToCheck)
            {
                result[def.Id] = false;
            }

            var totalFound = result.Count(kvp => kvp.Value);
            logService.LogInformation($"Total: {totalFound}/{definitionList.Count} apps installed");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking batch installed apps: {ex.Message}", ex);
            return definitionList.ToDictionary(d => d.Id, d => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<HashSet<string>> GetInstalledWinGetPackageIdsAsync()
    {
        var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "Cache");
            Directory.CreateDirectory(cacheDir);

            var exportPath = Path.Combine(cacheDir, "winget-packages.json");

            if (File.Exists(exportPath))
                File.Delete(exportPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"export -o \"{exportPath}\" --nowarn --disable-interactivity",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var completed = await Task.Run(() => process.WaitForExit(30000));
            if (!completed)
            {
                try { process.Kill(true); } catch { }
                logService.LogWarning("WinGet export timed out after 30 seconds");
                return installedPackageIds;
            }

            if (process.ExitCode != 0 || !File.Exists(exportPath))
            {
                logService.LogWarning($"WinGet export failed with exit code {process.ExitCode}");
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
        }
        catch (Exception ex)
        {
            logService.LogError($"Error running WinGet export: {ex.Message}", ex);
        }

        return installedPackageIds;
    }

    private async Task<HashSet<(string DisplayName, string Publisher)>> GetInstalledProgramsFromWmiOnlyAsync()
    {
        var installedPrograms = new HashSet<(string, string)>();

        try
        {
            await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, Vendor FROM Win32_InstalledWin32Program");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var name = obj["Name"]?.ToString();
                    var vendor = obj["Vendor"]?.ToString();

                    if (!string.IsNullOrEmpty(name))
                        installedPrograms.Add((name, vendor ?? ""));
                }
            });
        }
        catch (Exception ex)
        {
            logService.LogError($"Error querying WMI for installed programs: {ex.Message}", ex);
        }

        return installedPrograms;
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

    private bool FuzzyMatchProgram(string winGetPackageId, HashSet<(string DisplayName, string Publisher)> installedPrograms)
    {
        var parts = winGetPackageId.Split('.');

        if (parts.Length < 2)
        {
            var normalized = NormalizeString(winGetPackageId);
            return installedPrograms.Any(p =>
                NormalizeString(p.DisplayName).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }

        var publisher = NormalizeString(parts[0]);
        var productName = NormalizeString(string.Join(" ", parts.Skip(1)));

        foreach (var (displayName, vendor) in installedPrograms)
        {
            var normDisplayName = NormalizeString(displayName);
            var normVendor = NormalizeString(vendor);

            if (normDisplayName.Equals(productName, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(vendor) || normVendor.Contains(publisher))
                {
                    logService.LogInformation($"Exact match: '{winGetPackageId}' to '{displayName}'");
                    return true;
                }
            }
        }

        foreach (var (displayName, vendor) in installedPrograms)
        {
            var normDisplayName = NormalizeString(displayName);
            var normVendor = NormalizeString(vendor);

            if (normDisplayName.StartsWith(productName + " ", StringComparison.OrdinalIgnoreCase) ||
                normDisplayName.StartsWith(productName + "-", StringComparison.OrdinalIgnoreCase))
            {
                if (IsMainApplication(normDisplayName))
                {
                    if (string.IsNullOrEmpty(vendor) || normVendor.Contains(publisher))
                    {
                        logService.LogInformation($"Prefix match: '{winGetPackageId}' to '{displayName}' (Publisher: {vendor})");
                        return true;
                    }
                }
            }
        }

        foreach (var (displayName, vendor) in installedPrograms)
        {
            var normDisplayName = NormalizeString(displayName);
            var normVendor = NormalizeString(vendor);

            if (ContainsAsWord(normDisplayName, productName))
            {
                if (IsMainApplication(normDisplayName))
                {
                    if (string.IsNullOrEmpty(vendor) || normVendor.Contains(publisher))
                    {
                        logService.LogInformation($"Word match: '{winGetPackageId}' to '{displayName}' (Publisher: {vendor})");
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool IsMainApplication(string displayName)
    {
        string[] utilityKeywords = { "helper", "updater", "installer", "uninstall",
                                      "add-in", "plugin", "addon", "extension" };

        return !utilityKeywords.Any(keyword => displayName.Contains(keyword));
    }

    private bool ContainsAsWord(string text, string word)
    {
        var pattern = $@"\b{Regex.Escape(word)}\b";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }

    private string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var normalized = input.ToLowerInvariant();

        normalized = normalized
            .Replace("á", "a").Replace("à", "a").Replace("ä", "a").Replace("â", "a")
            .Replace("é", "e").Replace("è", "e").Replace("ë", "e").Replace("ê", "e")
            .Replace("í", "i").Replace("ì", "i").Replace("ï", "i").Replace("î", "i")
            .Replace("ó", "o").Replace("ò", "o").Replace("ö", "o").Replace("ô", "o")
            .Replace("ú", "u").Replace("ù", "u").Replace("ü", "u").Replace("û", "u")
            .Replace("ñ", "n").Replace("ç", "c");

        return normalized;
    }

    public async Task<Dictionary<string, bool>> CheckInstalledByDisplayNameAsync(IEnumerable<string> displayNames)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var nameList = displayNames.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

        if (!nameList.Any())
            return result;

        try
        {
            var registryPrograms = await GetInstalledProgramsFromRegistryAsync();

            foreach (var displayName in nameList)
            {
                var isInstalled = FuzzyMatchProgram(displayName, registryPrograms);
                result[displayName] = isInstalled;
                if (isInstalled)
                {
                    logService.LogInformation($"Installed (DisplayName): {displayName}");
                }
            }

            var totalFound = result.Count(kvp => kvp.Value);
            logService.LogInformation($"Display name detection: Found {totalFound}/{nameList.Count} apps installed");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking apps by display name: {ex.Message}", ex);
            return nameList.ToDictionary(name => name, name => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    #endregion
}

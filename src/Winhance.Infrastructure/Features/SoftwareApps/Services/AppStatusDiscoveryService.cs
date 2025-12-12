using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
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
                        result[capability.Id] = isInstalled;
                }
            }

            if (features.Any())
            {
                var featureNames = features.Select(f => f.OptionalFeatureName).ToList();
                var featureResults = await CheckFeaturesAsync(featureNames);
                foreach (var feature in features)
                {
                    if (featureResults.TryGetValue(feature.OptionalFeatureName, out bool isInstalled))
                        result[feature.Id] = isInstalled;
                }
            }

            if (apps.Any())
            {
                var installedApps = await GetInstalledStoreAppsAsync();
                foreach (var app in apps)
                {
                    result[app.Id] = installedApps.Contains(app.AppxPackageName);
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
                result[appId] = installedApps.Contains(appId);
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

    private async Task<HashSet<string>> GetInstalledStoreAppsAsync()
    {
        var installedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                        installedApps.Add(name);
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
                            installedApps.Add("Microsoft.Office.OneNote");
                        }

                        if (subKeyNames.Any(name => name.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)))
                        {
                            installedApps.Add("Microsoft.OneDriveSync");
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

    public async Task<Dictionary<string, bool>> GetExternalAppsInstallationStatusAsync(IEnumerable<string> winGetPackageIds)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var packageIdList = winGetPackageIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();

        if (!packageIdList.Any())
            return result;

        try
        {
            var remainingToCheck = new List<string>(packageIdList);
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
                var wingetPackages = await GetAllInstalledWinGetPackagesAsync();

                foreach (var packageId in packageIdList)
                {
                    if (wingetPackages.Contains(packageId))
                    {
                        result[packageId] = true;
                        remainingToCheck.Remove(packageId);
                        foundByWinGet++;
                    }
                }

                logService.LogInformation($"WinGet detection: Found {foundByWinGet}/{packageIdList.Count} apps");
            }

            if (remainingToCheck.Any())
            {
                var wmiTask = GetInstalledProgramsFromWmiOnlyAsync();
                var registryTask = GetInstalledProgramsFromRegistryAsync();

                await Task.WhenAll(wmiTask, registryTask);

                var wmiPrograms = wmiTask.Result;
                var registryPrograms = registryTask.Result;

                var foundByWmi = 0;
                var foundByRegistry = 0;

                foreach (var packageId in remainingToCheck.ToList())
                {
                    var wmiMatch = FuzzyMatchProgram(packageId, wmiPrograms);
                    var registryMatch = FuzzyMatchProgram(packageId, registryPrograms);

                    if (wmiMatch || registryMatch)
                    {
                        result[packageId] = true;
                        remainingToCheck.Remove(packageId);
                        if (wmiMatch) foundByWmi++;
                        if (registryMatch) foundByRegistry++;
                    }
                }

                logService.LogInformation($"Parallel detection: Found {foundByWmi} via WMI, {foundByRegistry} via Registry");
            }

            foreach (var packageId in remainingToCheck)
            {
                result[packageId] = false;
            }

            var totalFound = result.Count(kvp => kvp.Value);
            logService.LogInformation($"Total detection: Found {totalFound}/{packageIdList.Count} apps installed");

            return result;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking batch installed apps: {ex.Message}", ex);
            return packageIdList.ToDictionary(id => id, id => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<HashSet<string>> GetAllInstalledWinGetPackagesAsync()
    {
        var installedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await Task.Run(async () =>
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "list --accept-source-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = new System.Diagnostics.Process { StartInfo = startInfo };
                var output = new System.Text.StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        output.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();

                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var processTask = process.WaitForExitAsync();
                var completedTask = await Task.WhenAny(processTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch { }
                    
                    logService.LogWarning("WinGet list command timed out after 30 seconds");
                    return;
                }

                ParseWinGetListOutput(output.ToString(), installedPackages);
            });
        }
        catch (Exception ex)
        {
            logService.LogError($"Error getting installed WinGet packages: {ex.Message}", ex);
        }

        return installedPackages;
    }

    private void ParseWinGetListOutput(string output, HashSet<string> installedPackages)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        bool headerPassed = false;

        foreach (var line in lines)
        {
            if (line.Contains("---"))
            {
                headerPassed = true;
                continue;
            }

            if (!headerPassed || string.IsNullOrWhiteSpace(line))
                continue;

            var trimmedLine = line.Trim();
            var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
            {
                var appName = parts[0];
                installedPackages.Add(appName);

                foreach (var part in parts.Skip(1))
                {
                    if (part.Contains('.') && !part.All(char.IsDigit) && !part.Contains('/'))
                    {
                        installedPackages.Add(part);
                    }
                    else if (part.Length > 10 && part.All(c => char.IsLetterOrDigit(c)))
                    {
                        installedPackages.Add(part);
                    }
                }
            }
        }
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
            var normalizedPackageId = NormalizeString(winGetPackageId);
            foreach (var (displayName, _) in installedPrograms)
            {
                if (NormalizeString(displayName).Contains(normalizedPackageId))
                    return true;
            }
            return false;
        }

        var publisher = parts[0];
        var productName = string.Join(".", parts.Skip(1));

        var normalizedPublisher = NormalizeString(publisher);
        var normalizedProduct = NormalizeString(productName);
        var publisherWords = normalizedPublisher.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var (displayName, vendor) in installedPrograms)
        {
            var normalizedDisplayName = NormalizeString(displayName);
            var normalizedVendor = NormalizeString(vendor);

            if (normalizedDisplayName.Contains(normalizedProduct))
            {
                if (string.IsNullOrEmpty(vendor) ||
                    normalizedVendor.Contains(normalizedPublisher))
                {
                    return true;
                }
            }

            if (normalizedDisplayName.Contains(normalizedProduct))
            {
                var vendorWords = normalizedVendor.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (publisherWords.Any(pw => vendorWords.Any(vw => vw.Contains(pw) || pw.Contains(vw))))
                {
                    return true;
                }
            }

            if (normalizedProduct.Length >= 5)
            {
                var productWords = normalizedProduct.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in productWords)
                {
                    if (word.Length >= 4 && normalizedDisplayName.Contains(word))
                    {
                        if (string.IsNullOrEmpty(vendor))
                            return true;

                        if (normalizedVendor.Contains(normalizedPublisher) ||
                            publisherWords.Any(pw => normalizedVendor.Contains(pw)))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        foreach (var (displayName, _) in installedPrograms)
        {
            var normalizedDisplayName = NormalizeString(displayName);

            if (normalizedProduct.Length >= 6 && normalizedDisplayName.Contains(normalizedProduct))
            {
                return true;
            }

            var productWords = SplitIntoWords(productName)
                .Select(w => NormalizeString(w))
                .Where(w => w.Length >= 4)
                .ToList();

            if (productWords.Count >= 2)
            {
                var allWordsMatch = productWords.All(word => normalizedDisplayName.Contains(word));

                if (allWordsMatch)
                {
                    var combinedWordLength = productWords.Sum(w => w.Length);
                    if (combinedWordLength >= 10)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private List<string> SplitIntoWords(string input)
    {
        if (string.IsNullOrEmpty(input))
            return new List<string>();

        var words = new List<string>();
        var currentWord = new StringBuilder();

        foreach (var ch in input)
        {
            if (char.IsUpper(ch) && currentWord.Length > 0)
            {
                words.Add(currentWord.ToString());
                currentWord.Clear();
            }

            if (char.IsLetterOrDigit(ch))
            {
                currentWord.Append(ch);
            }
            else if (currentWord.Length > 0)
            {
                words.Add(currentWord.ToString());
                currentWord.Clear();
            }
        }

        if (currentWord.Length > 0)
            words.Add(currentWord.ToString());

        return words;
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

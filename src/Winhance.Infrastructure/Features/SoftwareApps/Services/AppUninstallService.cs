using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppUninstallService(
    IWinGetPackageInstaller winGetPackageInstaller,
    IChocolateyService chocolateyService,
    ILogService logService,
    IInteractiveUserService interactiveUserService,
    ITaskProgressService taskProgressService,
    IProcessExecutor processExecutor) : IAppUninstallService
{
    public async Task<OperationResult<bool>> UninstallAsync(
        ItemDefinition item,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var method = await DetermineUninstallMethodAsync(item).ConfigureAwait(false);

        return method switch
        {
            UninstallMethod.WinGet => await UninstallViaWinGetAsync(item, cancellationToken).ConfigureAwait(false),
            UninstallMethod.Chocolatey => await UninstallViaChocolateyAsync(item, cancellationToken).ConfigureAwait(false),
            UninstallMethod.Registry => await UninstallViaRegistryAsync(item, cancellationToken).ConfigureAwait(false),
            _ => OperationResult<bool>.Failed($"No uninstall method available for {item.Name}")
        };
    }

    private async Task<UninstallMethod> DetermineUninstallMethodAsync(ItemDefinition item)
    {
        // Use the detection source to pick the most appropriate uninstall method.
        // If the app was detected via Chocolatey, prefer Chocolatey uninstall
        // (WinGet won't know about Chocolatey-installed packages).
        switch (item.DetectedVia)
        {
            case DetectionSource.Chocolatey when !string.IsNullOrEmpty(item.ChocoPackageId):
                return UninstallMethod.Chocolatey;

            case DetectionSource.Registry:
                var (regFound, _) = await GetUninstallStringAsync(item.Name).ConfigureAwait(false);
                if (regFound)
                    return UninstallMethod.Registry;
                break;
        }

        // Default: prefer WinGet if available, then Registry
        if (!string.IsNullOrEmpty(item.MsStoreId) || (item.WinGetPackageId != null && item.WinGetPackageId.Any()))
            return UninstallMethod.WinGet;

        if (!string.IsNullOrEmpty(item.ChocoPackageId))
        {
            var chocoIds = await chocolateyService.GetInstalledPackageIdsAsync().ConfigureAwait(false);
            if (chocoIds.Contains(item.ChocoPackageId))
                return UninstallMethod.Chocolatey;
        }

        var (found, _2) = await GetUninstallStringAsync(item.Name).ConfigureAwait(false);
        if (found)
            return UninstallMethod.Registry;

        return UninstallMethod.None;
    }

    private async Task<OperationResult<bool>> UninstallViaWinGetAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        try
        {
            string? packageId;
            string? source;

            if (!string.IsNullOrEmpty(item.MsStoreId))
            {
                packageId = item.MsStoreId;
                source = "msstore";
            }
            else
            {
                packageId = item.WinGetPackageId?[0];
                source = "winget";
            }

            var success = await winGetPackageInstaller.UninstallPackageAsync(packageId!, source, item.Name, cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                // Fallback: try Chocolatey if available, then registry
                if (!string.IsNullOrEmpty(item.ChocoPackageId))
                {
                    logService.LogWarning($"WinGet uninstall failed for {item.Name}, attempting Chocolatey fallback");
                    var chocoResult = await UninstallViaChocolateyAsync(item, cancellationToken).ConfigureAwait(false);
                    if (chocoResult.Success)
                        return chocoResult;
                }

                logService.LogWarning($"WinGet uninstall failed for {item.Name}, attempting registry fallback");
                return await UninstallViaRegistryAsync(item, cancellationToken).ConfigureAwait(false);
            }

            return OperationResult<bool>.Succeeded(true);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<bool>.Cancelled("Uninstall cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"WinGet uninstall error for {item.Name}: {ex.Message}", ex);
            return await UninstallViaRegistryAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<OperationResult<bool>> UninstallViaChocolateyAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(item.ChocoPackageId))
                return OperationResult<bool>.Failed($"No Chocolatey package ID for {item.Name}");

            if (!await chocolateyService.IsChocolateyInstalledAsync(cancellationToken).ConfigureAwait(false))
            {
                logService.LogWarning($"Chocolatey not available for uninstalling {item.Name}");
                return OperationResult<bool>.Failed($"Chocolatey is not installed");
            }

            var success = await chocolateyService.UninstallPackageAsync(item.ChocoPackageId, item.Name, cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                logService.LogWarning($"Chocolatey uninstall failed for {item.Name}, attempting registry fallback");
                return await UninstallViaRegistryAsync(item, cancellationToken).ConfigureAwait(false);
            }

            return OperationResult<bool>.Succeeded(true);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<bool>.Cancelled("Uninstall cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Chocolatey uninstall error for {item.Name}: {ex.Message}", ex);
            return await UninstallViaRegistryAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<OperationResult<bool>> UninstallViaRegistryAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        try
        {
            var (found, uninstallString) = await GetUninstallStringAsync(item.Name).ConfigureAwait(false);

            if (!found || string.IsNullOrWhiteSpace(uninstallString))
            {
                logService.LogError($"No uninstall string found for {item.Name}");
                return OperationResult<bool>.Failed($"Cannot uninstall {item.Name}: No uninstall method found");
            }

            logService.LogInformation($"Uninstalling {item.Name} via registry: {uninstallString}");

            var (fileName, arguments) = ParseUninstallString(uninstallString);

            await processExecutor.ShellExecuteAsync(fileName, arguments, waitForExit: true, cancellationToken).ConfigureAwait(false);

            logService.LogInformation($"Uninstall process for {item.Name} completed successfully");
            taskProgressService.UpdateProgress(100, $"Uninstall process for {item.Name} completed successfully");

            return OperationResult<bool>.Succeeded(true);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<bool>.Cancelled("Uninstall cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Registry uninstall error for {item.Name}: {ex.Message}", ex);
            return OperationResult<bool>.Failed($"Uninstall failed: {ex.Message}");
        }
    }

    private async Task<(bool Found, string UninstallString)> GetUninstallStringAsync(string displayName)
    {
        return await Task.Run(() =>
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

                            var regDisplayName = subKey.GetValue("DisplayName")?.ToString();
                            if (string.IsNullOrEmpty(regDisplayName)) continue;

                            if (IsFuzzyMatch(displayName, regDisplayName))
                            {
                                var uninstallString = subKey.GetValue("UninstallString")?.ToString();
                                if (!string.IsNullOrEmpty(uninstallString))
                                {
                                    logService.LogInformation($"Found uninstall string for {displayName}: {uninstallString}");
                                    return (true, uninstallString);
                                }
                            }
                        }
                        catch (Exception ex) { logService.LogDebug($"Failed to read registry subkey '{subKeyName}': {ex.Message}"); }
                    }
                }
                catch (Exception ex) { logService.LogDebug($"Failed to open registry key for uninstall lookup: {ex.Message}"); }
            }

            return (false, string.Empty);
        }).ConfigureAwait(false);
    }

    private bool IsFuzzyMatch(string searchName, string registryName)
    {
        var normalized1 = NormalizeString(searchName);
        var normalized2 = NormalizeString(registryName);

        if (normalized1 == normalized2)
            return true;

        if (normalized2.Contains(normalized1) || normalized1.Contains(normalized2))
            return true;

        var words1 = normalized1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = normalized2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var matchCount = words1.Count(w => words2.Contains(w));
        return matchCount >= Math.Min(words1.Length, 2);
    }

    private string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var normalized = Regex.Replace(input, @"[^\w\s]", "").ToLowerInvariant().Trim();
        return Regex.Replace(normalized, @"\s+", " ");
    }

    private (string FileName, string Arguments) ParseUninstallString(string uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString))
            return (string.Empty, string.Empty);

        uninstallString = uninstallString.Trim();

        if (uninstallString.StartsWith("\""))
        {
            var endQuoteIndex = uninstallString.IndexOf("\"", 1);
            if (endQuoteIndex > 0)
            {
                var fileName = uninstallString.Substring(1, endQuoteIndex - 1);
                var arguments = uninstallString.Length > endQuoteIndex + 1
                    ? uninstallString.Substring(endQuoteIndex + 1).Trim()
                    : string.Empty;

                arguments = AppendSilentFlags(fileName, arguments);
                logService.LogInformation($"Parsed command - FileName: {fileName}, Arguments: {arguments}");
                return (fileName, arguments);
            }
        }

        var spaceIndex = uninstallString.IndexOf(' ');
        if (spaceIndex > 0)
        {
            var fileName = uninstallString.Substring(0, spaceIndex);
            var arguments = uninstallString.Substring(spaceIndex + 1).Trim();
            arguments = AppendSilentFlags(fileName, arguments);
            logService.LogInformation($"Parsed command - FileName: {fileName}, Arguments: {arguments}");
            return (fileName, arguments);
        }

        var finalArgs = AppendSilentFlags(uninstallString, string.Empty);
        logService.LogInformation($"Parsed command - FileName: {uninstallString}, Arguments: {finalArgs}");
        return (uninstallString, finalArgs);
    }

    private string AppendSilentFlags(string fileName, string existingArgs)
    {
        var lower = fileName.ToLowerInvariant();

        if (lower.Contains("msiexec"))
        {
            existingArgs = Regex.Replace(
                existingArgs,
                @"/I(\{[A-F0-9-]+\})",
                "/X$1",
                RegexOptions.IgnoreCase
            );

            if (!existingArgs.Contains("/quiet") && !existingArgs.Contains("/qn"))
                return $"{existingArgs} /quiet /norestart".Trim();

            return existingArgs;
        }
        else if (lower.Contains("unins") || lower.Contains("setup"))
        {
            if (!existingArgs.Contains("/SILENT") && !existingArgs.Contains("/VERYSILENT"))
                return $"{existingArgs} /VERYSILENT /NORESTART".Trim();
        }

        return existingArgs;
    }
}

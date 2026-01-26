using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class AppUninstallService(
    IWinGetService winGetService,
    ILogService logService,
    IPowerShellExecutionService powerShellService) : IAppUninstallService
{
    public async Task<OperationResult<bool>> UninstallAsync(
        ItemDefinition item,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var method = await DetermineUninstallMethodAsync(item);

        return method switch
        {
            UninstallMethod.WinGet => await UninstallViaWinGetAsync(item, cancellationToken),
            UninstallMethod.Registry => await UninstallViaRegistryAsync(item, cancellationToken),
            _ => OperationResult<bool>.Failed($"No uninstall method available for {item.Name}")
        };
    }

    public async Task<UninstallMethod> DetermineUninstallMethodAsync(ItemDefinition item)
    {
        if (item.WinGetPackageId != null && item.WinGetPackageId.Any())
            return UninstallMethod.WinGet;

        var (found, _) = await GetUninstallStringAsync(item.Name);
        if (found)
            return UninstallMethod.Registry;

        return UninstallMethod.None;
    }

    private async Task<OperationResult<bool>> UninstallViaWinGetAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        try
        {
            var primaryPackageId = item.WinGetPackageId?[0];
            var success = await winGetService.UninstallPackageAsync(primaryPackageId!, item.Name, cancellationToken);

            if (!success)
            {
                logService.LogWarning($"WinGet uninstall failed for {item.Name}, attempting registry fallback");
                return await UninstallViaRegistryAsync(item, cancellationToken);
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
            return await UninstallViaRegistryAsync(item, cancellationToken);
        }
    }

    private async Task<OperationResult<bool>> UninstallViaRegistryAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        try
        {
            var (found, uninstallString) = await GetUninstallStringAsync(item.Name);

            if (!found || string.IsNullOrWhiteSpace(uninstallString))
            {
                logService.LogError($"No uninstall string found for {item.Name}");
                return OperationResult<bool>.Failed($"Cannot uninstall {item.Name}: No uninstall method found");
            }

            logService.LogInformation($"Uninstalling {item.Name} via registry: {uninstallString}");

            var (fileName, arguments) = ParseUninstallString(uninstallString);

            var escapedFileName = fileName.Replace("'", "''");
            var escapedArguments = arguments.Replace("'", "''");

            var script = $@"
$process = Start-Process -FilePath '{escapedFileName}' -ArgumentList '{escapedArguments}' -PassThru -Wait
exit $process.ExitCode
";

            await powerShellService.ExecuteScriptAsync(script, null, cancellationToken);

            logService.LogInformation($"Uninstall process for {item.Name} completed successfully");

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
                        catch { }
                    }
                }
                catch { }
            }

            return (false, string.Empty);
        });
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

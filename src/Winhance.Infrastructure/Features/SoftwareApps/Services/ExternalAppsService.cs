using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class ExternalAppsService(
    ILogService logService,
    IWinGetService winGetService,
    IAppStatusDiscoveryService appStatusDiscoveryService,
    IAppUninstallService appUninstallService,
    IDirectDownloadService directDownloadService,
    ITaskProgressService taskProgressService) : IExternalAppsService
{
    public string DomainName => FeatureIds.ExternalApps;

    private CancellationToken GetCurrentCancellationToken()
    {
        return taskProgressService?.CurrentTaskCancellationSource?.Token ?? CancellationToken.None;
    }

    public async Task<IEnumerable<ItemDefinition>> GetAppsAsync()
    {
        return ExternalAppDefinitions.GetExternalApps().Items;
    }

    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        var cancellationToken = GetCurrentCancellationToken();

        try
        {
            if (item.CustomProperties.TryGetValue("RequiresDirectDownload", out var requiresDownload)
                && requiresDownload is bool isDirect && isDirect)
            {
                logService.LogInformation($"Installing {item.Name} via direct download");
                var success = await directDownloadService.DownloadAndInstallAsync(item, progress, cancellationToken);
                return success
                    ? OperationResult<bool>.Succeeded(true)
                    : OperationResult<bool>.Failed("Direct download installation failed");
            }

            if (item.WinGetPackageId == null || !item.WinGetPackageId.Any())
                return OperationResult<bool>.Failed("No WinGet package ID or download URL specified");

            var primaryPackageId = item.WinGetPackageId[0];
            var installerType = await winGetService.GetInstallerTypeAsync(primaryPackageId, cancellationToken);
            var isPortable = IsPortableInstallerType(installerType);

            var wingetSuccess = await winGetService.InstallPackageAsync(primaryPackageId, item.Name, cancellationToken);

            if (wingetSuccess && isPortable)
            {
                await CreateStartMenuShortcutForPortableAppAsync(item);
            }

            return wingetSuccess ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("Installation failed");
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation($"Installation of {item.Name} was cancelled");
            return OperationResult<bool>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    private static bool IsPortableInstallerType(string? installerType)
    {
        if (string.IsNullOrEmpty(installerType))
            return false;

        var lower = installerType.ToLowerInvariant();
        return lower.Contains("portable") || lower == "zip";
    }

    private async Task CreateStartMenuShortcutForPortableAppAsync(ItemDefinition item)
    {
        try
        {
            var exePath = FindPortableAppExecutable(item);
            if (string.IsNullOrEmpty(exePath))
            {
                logService.LogWarning($"Could not find executable for portable app {item.Name}");
                return;
            }

            var startMenuPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                $"{item.Name}.lnk");

            var script = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{startMenuPath.Replace("'", "''")}')
$Shortcut.TargetPath = '{exePath.Replace("'", "''")}'
$Shortcut.WorkingDirectory = '{Path.GetDirectoryName(exePath)?.Replace("'", "''")}'
$Shortcut.Description = '{item.Name.Replace("'", "''")}'
$Shortcut.Save()
";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                logService.LogInformation($"Created Start Menu shortcut for portable app: {item.Name} -> {exePath}");
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                logService.LogWarning($"Failed to create shortcut for {item.Name}: {error}");
            }
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Error creating Start Menu shortcut for {item.Name}: {ex.Message}");
        }
    }

    private string? FindPortableAppExecutable(ItemDefinition item)
    {
        var searchPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WinGet", "Packages"),
            @"C:\Program Files\WinGet\Packages",
            @"C:\Program Files (x86)\WinGet\Packages"
        };

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            var matchingDirs = item.WinGetPackageId!
                .SelectMany(pkgId => Directory.GetDirectories(basePath, $"{pkgId}*"))
                .Distinct()
                .ToList();

            foreach (var dir in matchingDirs)
            {
                var exeFiles = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!exeFiles.Any())
                    continue;

                // Try to find an exe that matches the app name
                var appNamePart = item.Name.Split(' ')[0];
                var bestMatch = exeFiles.FirstOrDefault(e =>
                    Path.GetFileNameWithoutExtension(e).Contains(appNamePart, StringComparison.OrdinalIgnoreCase));

                return bestMatch ?? exeFiles.First();
            }
        }

        return null;
    }

    public async Task<OperationResult<bool>> UninstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            var result = await appUninstallService.UninstallAsync(item, progress, CancellationToken.None);

            if (result.Success)
            {
                RemoveStartMenuShortcutIfExists(item.Name);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation($"Uninstall of {item.Name} was cancelled by user");
            return OperationResult<bool>.Cancelled("Uninstall cancelled by user");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to uninstall {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    private void RemoveStartMenuShortcutIfExists(string appName)
    {
        try
        {
            var shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                $"{appName}.lnk");

            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
                logService.LogInformation($"Removed Start Menu shortcut for {appName}");
            }
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Could not remove Start Menu shortcut for {appName}: {ex.Message}");
        }
    }

    public async Task<bool> CheckIfInstalledAsync(string winGetPackageId)
    {
        if (string.IsNullOrWhiteSpace(winGetPackageId))
            return false;

        try
        {
            var tempDef = new ItemDefinition
            {
                Id = winGetPackageId,
                Name = winGetPackageId,
                Description = "",
                WinGetPackageId = [winGetPackageId]
            };
            var batch = await CheckBatchInstalledAsync(new[] { tempDef });
            return batch.GetValueOrDefault(winGetPackageId, false);
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking if {winGetPackageId} is installed: {ex.Message}");
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> CheckBatchInstalledAsync(IEnumerable<ItemDefinition> definitions)
    {
        return await appStatusDiscoveryService.GetExternalAppsInstallationStatusAsync(definitions);
    }
}
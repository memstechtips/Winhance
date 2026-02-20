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
    ITaskProgressService taskProgressService,
    IChocolateyService chocolateyService,
    IChocolateyConsentService chocolateyConsentService,
    IInteractiveUserService interactiveUserService) : IExternalAppsService
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

            // Determine package ID and source
            string? packageId = null;
            string? source = null;

            if (!string.IsNullOrEmpty(item.MsStoreId))
            {
                packageId = item.MsStoreId;
                source = "msstore";
            }
            else if (item.WinGetPackageId != null && item.WinGetPackageId.Any())
            {
                packageId = item.WinGetPackageId[0];
                source = "winget";
            }
            else
            {
                return OperationResult<bool>.Failed("No WinGet package ID or Store ID specified");
            }

            var installerType = await winGetService.GetInstallerTypeAsync(packageId, cancellationToken);
            var isPortable = IsPortableInstallerType(installerType);

            var wingetResult = await winGetService.InstallPackageAsync(packageId, source, item.Name, cancellationToken);

            if (wingetResult.Success)
            {
                if (isPortable)
                    await CreateStartMenuShortcutForPortableAppAsync(item);

                return OperationResult<bool>.Succeeded(true);
            }

            // Chocolatey fallback: only for eligible failures when a ChocoPackageId is defined
            if (wingetResult.IsChocolateyFallbackCandidate && !string.IsNullOrEmpty(item.ChocoPackageId))
            {
                logService.LogInformation($"WinGet install failed for '{item.Name}' ({wingetResult.FailureReason}), attempting Chocolatey fallback with '{item.ChocoPackageId}'");

                var consented = await chocolateyConsentService.RequestConsentAsync();
                if (consented)
                {
                    if (!await chocolateyService.IsChocolateyInstalledAsync(cancellationToken))
                    {
                        if (!await chocolateyService.InstallChocolateyAsync(cancellationToken))
                        {
                            logService.LogError("Failed to install Chocolatey, cannot proceed with fallback");
                            return OperationResult<bool>.Failed(wingetResult.ErrorMessage ?? "Installation failed");
                        }
                    }

                    var chocoSuccess = await chocolateyService.InstallPackageAsync(item.ChocoPackageId, item.Name, cancellationToken);
                    if (chocoSuccess)
                    {
                        if (IsChocoPortablePackage(item.ChocoPackageId))
                            await CreateStartMenuShortcutForChocoPortableAppAsync(item);

                        return OperationResult<bool>.Succeeded(true);
                    }

                    logService.LogWarning($"Chocolatey fallback also failed for '{item.Name}'");
                }
            }

            return OperationResult<bool>.Failed(wingetResult.ErrorMessage ?? "Installation failed");
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
            var installDir = FindPortableAppDirectory(item);
            if (string.IsNullOrEmpty(installDir))
            {
                logService.LogWarning($"Could not find installation directory for {item.Name}");
                return;
            }

            var exeFiles = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories).ToList();
            if (!exeFiles.Any())
            {
                logService.LogWarning($"No executables found for {item.Name}");
                return;
            }

            var startMenuFolder = Path.Combine(
                interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs),
                item.Name);

            Directory.CreateDirectory(startMenuFolder);

            foreach (var exePath in exeFiles)
            {
                var exeName = Path.GetFileNameWithoutExtension(exePath);
                var shortcutPath = Path.Combine(startMenuFolder, $"{exeName}.lnk");

                await CreateShortcutAsync(shortcutPath, exePath, Path.GetDirectoryName(exePath), item.Name);
            }

            logService.LogInformation($"Created Start Menu folder with {exeFiles.Count} shortcuts for {item.Name}");
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Error creating Start Menu shortcuts for {item.Name}: {ex.Message}");
        }
    }

    private static bool IsChocoPortablePackage(string chocoPackageId)
    {
        return chocoPackageId.EndsWith(".portable", StringComparison.OrdinalIgnoreCase)
            || chocoPackageId.Contains(".portable.", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateStartMenuShortcutForChocoPortableAppAsync(ItemDefinition item)
    {
        try
        {
            var installDir = FindChocoPackageDirectory(item.ChocoPackageId!);
            if (string.IsNullOrEmpty(installDir))
            {
                logService.LogWarning($"Could not find Chocolatey install directory for {item.Name}");
                return;
            }

            var exeFiles = Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).Equals("ChocolateyInstall.ps1", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!exeFiles.Any())
            {
                logService.LogWarning($"No executables found in Chocolatey package for {item.Name}");
                return;
            }

            var startMenuFolder = Path.Combine(
                interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs),
                item.Name);

            Directory.CreateDirectory(startMenuFolder);

            foreach (var exePath in exeFiles)
            {
                var exeName = Path.GetFileNameWithoutExtension(exePath);
                var shortcutPath = Path.Combine(startMenuFolder, $"{exeName}.lnk");
                await CreateShortcutAsync(shortcutPath, exePath, Path.GetDirectoryName(exePath)!, item.Name);
            }

            logService.LogInformation($"Created Start Menu folder with {exeFiles.Count} shortcuts for {item.Name} (Chocolatey portable)");
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Error creating Start Menu shortcuts for Chocolatey package {item.Name}: {ex.Message}");
        }
    }

    private string? FindChocoPackageDirectory(string chocoPackageId)
    {
        var searchPaths = new[]
        {
            @"C:\ProgramData\chocolatey\lib",
            Path.Combine(interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UniGetUI", "Chocolatey", "lib")
        };

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            var packageDir = Path.Combine(basePath, chocoPackageId, "tools");
            if (Directory.Exists(packageDir))
                return packageDir;

            // Also check without "tools" subfolder
            packageDir = Path.Combine(basePath, chocoPackageId);
            if (Directory.Exists(packageDir))
                return packageDir;
        }

        return null;
    }

    private string? FindPortableAppDirectory(ItemDefinition item)
    {
        var searchPaths = new List<string>
        {
            Path.Combine(interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WinGet", "Packages"),
            @"C:\Program Files\WinGet\Packages",
            @"C:\Program Files (x86)\WinGet\Packages"
        };

        // Under OTS, also search the process user's (admin) AppData since WinGet runs as admin
        if (interactiveUserService.IsOtsElevation)
        {
            searchPaths.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WinGet", "Packages"));
        }

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            var matchingDir = item.WinGetPackageId!
                .SelectMany(pkgId => Directory.GetDirectories(basePath, $"{pkgId}*"))
                .Distinct()
                .FirstOrDefault();

            if (matchingDir != null)
                return matchingDir;
        }

        return null;
    }

    private async Task CreateShortcutAsync(string shortcutPath, string targetPath, string workingDir, string description)
    {
        var script = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath.Replace("'", "''")}')
$Shortcut.TargetPath = '{targetPath.Replace("'", "''")}'
$Shortcut.WorkingDirectory = '{workingDir?.Replace("'", "''")}'
$Shortcut.Description = '{description.Replace("'", "''")}'
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

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            logService.LogWarning($"Failed to create shortcut at {shortcutPath}: {error}");
        }
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
            var startMenuFolder = Path.Combine(
                interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs),
                appName);

            if (Directory.Exists(startMenuFolder))
            {
                Directory.Delete(startMenuFolder, true);
                logService.LogInformation($"Removed Start Menu folder for {appName}");
            }
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Could not remove Start Menu folder for {appName}: {ex.Message}");
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
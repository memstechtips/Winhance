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
    IWinGetPackageInstaller winGetPackageInstaller,
    IWinGetDetectionService winGetDetectionService,
    IWinGetBootstrapper winGetBootstrapper,
    IAppStatusDiscoveryService appStatusDiscoveryService,
    IAppUninstallService appUninstallService,
    IDirectDownloadService directDownloadService,
    ITaskProgressService taskProgressService,
    IChocolateyService chocolateyService,
    IChocolateyConsentService chocolateyConsentService,
    IInteractiveUserService interactiveUserService,
    IFileSystemService fileSystemService) : IExternalAppsService
{
    public string DomainName => FeatureIds.ExternalApps;

    public event EventHandler? WinGetReady
    {
        add => winGetBootstrapper.WinGetInstalled += value;
        remove => winGetBootstrapper.WinGetInstalled -= value;
    }

    public void InvalidateStatusCache() => appStatusDiscoveryService.InvalidateCache();

    private CancellationToken GetCurrentCancellationToken()
    {
        return taskProgressService?.CurrentTaskCancellationSource?.Token ?? CancellationToken.None;
    }

    public Task<IEnumerable<ItemDefinition>> GetAppsAsync()
    {
        return Task.FromResult<IEnumerable<ItemDefinition>>(ExternalAppDefinitions.GetExternalApps().Items);
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
                var success = await directDownloadService.DownloadAndInstallAsync(item, progress, cancellationToken).ConfigureAwait(false);
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

            var installerType = await winGetDetectionService.GetInstallerTypeAsync(packageId, cancellationToken).ConfigureAwait(false);
            var isPortable = IsPortableInstallerType(installerType);

            var wingetResult = await winGetPackageInstaller.InstallPackageAsync(packageId, source, item.Name, cancellationToken).ConfigureAwait(false);

            if (wingetResult.Success)
            {
                if (isPortable)
                    await CreateStartMenuShortcutForPortableAppAsync(item).ConfigureAwait(false);

                return OperationResult<bool>.Succeeded(true);
            }

            // Chocolatey fallback: only for eligible failures when a ChocoPackageId is defined
            if (wingetResult.IsChocolateyFallbackCandidate && !string.IsNullOrEmpty(item.ChocoPackageId))
            {
                logService.LogInformation($"WinGet install failed for '{item.Name}' ({wingetResult.FailureReason}), attempting Chocolatey fallback with '{item.ChocoPackageId}'");

                var consented = await chocolateyConsentService.RequestConsentAsync().ConfigureAwait(false);
                if (consented)
                {
                    if (!await chocolateyService.IsChocolateyInstalledAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (!await chocolateyService.InstallChocolateyAsync(cancellationToken).ConfigureAwait(false))
                        {
                            logService.LogError("Failed to install Chocolatey, cannot proceed with fallback");
                            return OperationResult<bool>.Failed(wingetResult.ErrorMessage ?? "Installation failed");
                        }
                    }

                    var chocoSuccess = await chocolateyService.InstallPackageAsync(item.ChocoPackageId, item.Name, cancellationToken).ConfigureAwait(false);
                    if (chocoSuccess)
                    {
                        if (IsChocoPortablePackage(item.ChocoPackageId))
                            await CreateStartMenuShortcutForChocoPortableAppAsync(item).ConfigureAwait(false);

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

            var exeFiles = fileSystemService.GetFiles(installDir, "*.exe", SearchOption.AllDirectories).ToList();
            if (!exeFiles.Any())
            {
                logService.LogWarning($"No executables found for {item.Name}");
                return;
            }

            var startMenuFolder = fileSystemService.CombinePath(
                interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs),
                item.Name);

            fileSystemService.CreateDirectory(startMenuFolder);

            foreach (var exePath in exeFiles)
            {
                var exeName = fileSystemService.GetFileNameWithoutExtension(exePath);
                var shortcutPath = fileSystemService.CombinePath(startMenuFolder, $"{exeName}.lnk");

                await CreateShortcutAsync(shortcutPath, exePath, fileSystemService.GetDirectoryName(exePath)!, item.Name).ConfigureAwait(false);
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

            var exeFiles = fileSystemService.GetFiles(installDir, "*.exe", SearchOption.AllDirectories)
                .Where(f => !fileSystemService.GetFileName(f).Equals("ChocolateyInstall.ps1", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!exeFiles.Any())
            {
                logService.LogWarning($"No executables found in Chocolatey package for {item.Name}");
                return;
            }

            var startMenuFolder = fileSystemService.CombinePath(
                interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs),
                item.Name);

            fileSystemService.CreateDirectory(startMenuFolder);

            foreach (var exePath in exeFiles)
            {
                var exeName = fileSystemService.GetFileNameWithoutExtension(exePath);
                var shortcutPath = fileSystemService.CombinePath(startMenuFolder, $"{exeName}.lnk");
                await CreateShortcutAsync(shortcutPath, exePath, fileSystemService.GetDirectoryName(exePath)!, item.Name).ConfigureAwait(false);
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
            fileSystemService.CombinePath(interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UniGetUI", "Chocolatey", "lib")
        };

        foreach (var basePath in searchPaths)
        {
            if (!fileSystemService.DirectoryExists(basePath))
                continue;

            var packageDir = fileSystemService.CombinePath(basePath, chocoPackageId, "tools");
            if (fileSystemService.DirectoryExists(packageDir))
                return packageDir;

            // Also check without "tools" subfolder
            packageDir = fileSystemService.CombinePath(basePath, chocoPackageId);
            if (fileSystemService.DirectoryExists(packageDir))
                return packageDir;
        }

        return null;
    }

    private string? FindPortableAppDirectory(ItemDefinition item)
    {
        var searchPaths = new List<string>
        {
            fileSystemService.CombinePath(interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WinGet", "Packages"),
            @"C:\Program Files\WinGet\Packages",
            @"C:\Program Files (x86)\WinGet\Packages"
        };

        // Under OTS, also search the process user's (admin) AppData since WinGet runs as admin
        if (interactiveUserService.IsOtsElevation)
        {
            searchPaths.Add(fileSystemService.CombinePath(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WinGet", "Packages"));
        }

        foreach (var basePath in searchPaths)
        {
            if (!fileSystemService.DirectoryExists(basePath))
                continue;

            var matchingDir = item.WinGetPackageId!
                .SelectMany(pkgId => fileSystemService.GetDirectories(basePath, $"{pkgId}*"))
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
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            logService.LogWarning($"Failed to create shortcut at {shortcutPath}: {error}");
        }
    }

    public async Task<OperationResult<bool>> UninstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            var result = await appUninstallService.UninstallAsync(item, progress, CancellationToken.None).ConfigureAwait(false);

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
            var startMenuFolder = fileSystemService.CombinePath(
                interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs),
                appName);

            if (fileSystemService.DirectoryExists(startMenuFolder))
            {
                fileSystemService.DeleteDirectory(startMenuFolder, true);
                logService.LogInformation($"Removed Start Menu folder for {appName}");
            }
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Could not remove Start Menu folder for {appName}: {ex.Message}");
        }
    }

    public async Task<Dictionary<string, bool>> CheckBatchInstalledAsync(IEnumerable<ItemDefinition> definitions)
    {
        return await appStatusDiscoveryService.GetExternalAppsInstallationStatusAsync(definitions).ConfigureAwait(false);
    }
}
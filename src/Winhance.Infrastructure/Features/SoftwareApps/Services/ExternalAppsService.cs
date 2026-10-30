using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Autounattend.Helpers;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class ExternalAppsService(
    ILogService logService,
    IWinGetPackageInstaller winGetPackageInstaller,
    IWinGetDetectionService winGetDetectionService,
    IWinGetBootstrapper winGetBootstrapper,
    IAppStatusDiscoveryService appStatusDiscoveryService,
    IExternalAppUninstallService externalAppUninstallService,
    IDirectDownloadService directDownloadService,
    ITaskProgressService taskProgressService,
    IChocolateyService chocolateyService,
    IInteractiveUserService interactiveUserService,
    IFileSystemService fileSystemService,
    IProcessExecutor processExecutor,
    IChangeHistoryService changeHistory,
    IInstallOptionsService installOptions) : IExternalAppsService
{
    public string DomainName => FeatureIds.ExternalApps;

    public event EventHandler? WinGetReady
    {
        add => winGetBootstrapper.WinGetInstalled += value;
        remove => winGetBootstrapper.WinGetInstalled -= value;
    }

    public void InvalidateStatusCache() => appStatusDiscoveryService.InvalidateCache();

    public Task<IEnumerable<ItemDefinition>> GetAppsAsync()
    {
        return Task.FromResult<IEnumerable<ItemDefinition>>(ExternalAppDefinitions.GetExternalApps().Items);
    }

    // winget first; Chocolatey adds only what neither winget nor the catalog has. The two name the same app differently
    // (VideoLAN.VLC / vlc, Notepad++ / notepadplusplus), so a Chocolatey id is compared with the catalog's Chocolatey
    // ids and with each winget result's name and last id segment, all squashed to letters and digits.
    public async Task<PackageSearchResult> SearchPackagesAsync(string query, CancellationToken cancellationToken = default)
    {
        var catalog = ExternalAppDefinitions.GetExternalApps().Items;
        var catalogPackageIds = catalog.SelectMany(item => item.WinGetPackageId ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var searchChocolatey = await chocolateyService.IsChocolateyInstalledAsync(cancellationToken).ConfigureAwait(false);
        var winGetSearch = winGetDetectionService.SearchPackagesAsync(query, cancellationToken);
        var chocoSearch = searchChocolatey ? chocolateyService.SearchPackagesAsync(query, cancellationToken) : Task.FromResult<IReadOnlyList<string>>([]);
        await Task.WhenAll(winGetSearch, chocoSearch).ConfigureAwait(false);

        var winGetResults = winGetSearch.Result.Where(match => !catalogPackageIds.Contains(match.Id)).ToList();
        var known = catalog.Select(item => item.ChocoPackageId).OfType<string>()
            .Concat(catalog.Select(item => item.Name))
            .Concat(winGetSearch.Result.Select(match => match.Name))
            .Concat(winGetSearch.Result.Select(match => match.Id[(match.Id.LastIndexOf('.') + 1)..]))
            .Select(Squash)
            .ToHashSet(StringComparer.Ordinal);

        // A pick is saved by its id, so a result whose id a saved file could not carry is not offered.
        var items = winGetResults.Select(match => WinGetPackageItem.Create(match.Id, match.Name))
            .Concat(chocoSearch.Result.Where(id => !known.Contains(Squash(id))).Select(ChocoPackageItem.Create))
            .Where(item => PackageSearchItem.FromId(item.Id) is not null)
            .ToList();
        return new PackageSearchResult(items, searchChocolatey);
    }

    internal static string Squash(string name) =>
        new string(name.Replace("+", "plus", StringComparison.Ordinal).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    public async Task<IReadOnlyCollection<string>> GetUnavailableAppIdsAsync(IEnumerable<ItemDefinition> items, CancellationToken cancellationToken = default)
    {
        var wingetOnly = items
            .Where(item => item.WinGetPackageId is { Length: > 0 }
                && string.IsNullOrEmpty(item.MsStoreId)
                && string.IsNullOrEmpty(item.ChocoPackageId)
                && item.ExternalApp?.RequiresDirectDownload != true
                && !WinGetPackageItem.Is(item.Id))
            .ToList();
        if (wingetOnly.Count == 0)
            return [];

        var packageIds = wingetOnly.SelectMany(item => item.WinGetPackageId!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missing = (await winGetDetectionService.FindMissingPackageIdsAsync(packageIds, cancellationToken).ConfigureAwait(false))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unavailable = wingetOnly
            .Where(item => item.WinGetPackageId!.All(missing.Contains))
            .Select(item => item.Id)
            .ToList();
        foreach (var id in unavailable)
            logService.LogWarning($"'{id}' is hidden: winget no longer has its package and it has no other source");

        return unavailable;
    }

    public Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
        => InstallAsync(item, progress, installOptions.EffectiveFor(item.Id));

    // An answer file installs for everyone on the new PC unless its install options chose a scope.
    public Task<OperationResult<bool>> InstallAppOnNewPcAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        var options = installOptions.EffectiveFor(item.Id);
        return InstallAsync(item, progress, options.Scope == InstallScope.Default ? options with { Scope = InstallScope.Machine } : options);
    }

    private async Task<OperationResult<bool>> InstallAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress, InstallOptions options)
    {
        var cancellationToken = taskProgressService.GetCurrentCancellationToken();

        try
        {
            if (item.ExternalApp?.RequiresDirectDownload == true)
            {
                logService.LogInformation($"Installing {item.Name} via direct download");
                var success = await directDownloadService.DownloadAndInstallAsync(item, progress, cancellationToken).ConfigureAwait(false);
                return success
                    ? OperationResult<bool>.Succeeded(true)
                    : OperationResult<bool>.Failed("Direct download installation failed");
            }

            var sources = new List<(string packageId, string source)>();

            if (item.WinGetPackageId != null && item.WinGetPackageId.Length > 0)
                sources.Add((item.WinGetPackageId[0], WinGetSources.WinGet));
            if (!string.IsNullOrEmpty(item.MsStoreId))
                sources.Add((item.MsStoreId, WinGetSources.MsStore));

            PackageInstallResult? lastResult = null;

            foreach (var (pkgId, src) in sources)
            {
                try
                {
                    var installerType = await winGetDetectionService.GetInstallerTypeAsync(pkgId, cancellationToken).ConfigureAwait(false);
                    var isPortable = IsPortableInstallerType(installerType);

                    // The Store installs per user whatever winget is asked.
                    var sourceOptions = src == WinGetSources.MsStore ? InstallOptions.Default
                        : isPortable ? options with { Scope = InstallScope.Default }
                        : options;

                    lastResult = await winGetPackageInstaller.InstallPackageAsync(pkgId, src, item.Name, item.WinGetInstallerOverride, sourceOptions, cancellationToken).ConfigureAwait(false);

                    // Any failure with a chosen scope gets the default-scope attempt whatever the exit code, so an app
                    // that only ships an installer for the other scope still installs.
                    if (!lastResult.Success && sourceOptions.Scope != InstallScope.Default)
                    {
                        logService.LogWarning($"Install for scope {sourceOptions.Scope} failed for '{item.Name}' ({lastResult.FailureReason}), trying the default scope");
                        lastResult = await winGetPackageInstaller.InstallPackageAsync(pkgId, src, item.Name, item.WinGetInstallerOverride, sourceOptions with { Scope = InstallScope.Default }, cancellationToken).ConfigureAwait(false);
                    }

                    if (lastResult.Success)
                    {
                        if (isPortable)
                            await CreateStartMenuShortcutForPortableAppAsync(item).ConfigureAwait(false);

                        return OperationResult<bool>.Succeeded(true);
                    }

                    logService.LogWarning($"Install failed for '{item.Name}' via {src}/{pkgId}: {lastResult.FailureReason}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logService.LogError($"Exception installing '{item.Name}' via {src}/{pkgId}: {ex.Message}");
                    lastResult = PackageInstallResult.Failed(InstallFailureReason.Other, ex.Message);
                }
            }

            if (!string.IsNullOrEmpty(item.ChocoPackageId))
            {
                logService.LogInformation($"Attempting Chocolatey install for '{item.Name}' with '{item.ChocoPackageId}'");

                try
                {
                    var chocoReady = await chocolateyService.IsChocolateyInstalledAsync(cancellationToken).ConfigureAwait(false)
                        || await chocolateyService.InstallChocolateyAsync(cancellationToken).ConfigureAwait(false);

                    if (chocoReady)
                    {
                        var chocoSuccess = await chocolateyService.InstallPackageAsync(item.ChocoPackageId, item.Name, options, cancellationToken).ConfigureAwait(false);
                        if (chocoSuccess)
                        {
                            if (IsChocoPortablePackage(item.ChocoPackageId))
                                await CreateStartMenuShortcutForChocoPortableAppAsync(item).ConfigureAwait(false);

                            return OperationResult<bool>.Succeeded(true);
                        }

                        logService.LogWarning($"Chocolatey install failed for '{item.Name}'");
                    }
                    else
                    {
                        logService.LogError("Failed to install Chocolatey, cannot proceed with Chocolatey install");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logService.LogWarning($"Chocolatey install failed for '{item.Name}': {ex.Message}");
                }
            }

            if (item.ExternalApp?.DownloadUrl != null)
            {
                logService.LogInformation($"All package manager installs failed for '{item.Name}', attempting direct download fallback");

                try
                {
                    var success = await directDownloadService.DownloadAndInstallAsync(item, progress, cancellationToken).ConfigureAwait(false);
                    if (success)
                        return OperationResult<bool>.Succeeded(true);

                    logService.LogWarning($"Direct download fallback failed for '{item.Name}'");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logService.LogWarning($"Direct download fallback failed for '{item.Name}': {ex.Message}");
                }
            }

            return OperationResult<bool>.Failed(lastResult?.ErrorMessage ?? "Installation failed");
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
            if (exeFiles.Count == 0)
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

            if (exeFiles.Count == 0)
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
$Shortcut = $WshShell.CreateShortcut('{PowerShellScriptUtilities.EscapePowerShellString(shortcutPath)}')
$Shortcut.TargetPath = '{PowerShellScriptUtilities.EscapePowerShellString(targetPath)}'
$Shortcut.WorkingDirectory = '{PowerShellScriptUtilities.EscapePowerShellString(workingDir)}'
$Shortcut.Description = '{PowerShellScriptUtilities.EscapePowerShellString(description)}'
$Shortcut.Save()
";

        var args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"";
        var result = await processExecutor.ExecuteAsync("powershell", args, CancellationToken.None).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            logService.LogWarning($"Failed to create shortcut at {shortcutPath}: {result.StandardError}");
        }
    }

    public async Task<OperationResult<bool>> UninstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            var cancellationToken = taskProgressService.GetCurrentCancellationToken();
            var result = await externalAppUninstallService.UninstallAsync(item, progress, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                changeHistory.LogAppChange(item.Name, AppChangeKind.Removed);
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
using System;
using System.IO;
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

public class ExternalAppUninstallService(
    IWinGetPackageInstaller winGetPackageInstaller,
    IChocolateyService chocolateyService,
    ILogService logService,
    IInteractiveUserService interactiveUserService,
    ITaskProgressService taskProgressService,
    IProcessExecutor processExecutor) : IExternalAppUninstallService
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
            UninstallMethod.AppX => await UninstallViaAppxAsync(item, cancellationToken).ConfigureAwait(false),
            UninstallMethod.Registry => await UninstallViaRegistryAsync(item, cancellationToken).ConfigureAwait(false),
            UninstallMethod.FileSystem => await UninstallViaFileSystemAsync(item, cancellationToken).ConfigureAwait(false),
            _ => OperationResult<bool>.Failed($"No uninstall method available for {item.Name}")
        };
    }

    private async Task<UninstallMethod> DetermineUninstallMethodAsync(ItemDefinition item)
    {
        // Use the detection source to pick the most appropriate uninstall method.
        // Each source owns the path that knows how to undo what it detected — WinGet
        // can't see Chocolatey-installed packages, can't see Store-UI-installed AppX
        // packages, etc. Dispatching on DetectedVia keeps detection and uninstall
        // symmetric.
        switch (item.DetectedVia)
        {
            case DetectionSource.Chocolatey when !string.IsNullOrEmpty(item.ChocoPackageId):
                return UninstallMethod.Chocolatey;

            case DetectionSource.AppX when item.AppxPackageName?.Length > 0:
                return UninstallMethod.AppX;

            case DetectionSource.Registry:
                var (regFound, _) = await GetUninstallStringAsync(item).ConfigureAwait(false);
                if (regFound)
                    return UninstallMethod.Registry;
                break;

            case DetectionSource.FileSystem when item.DetectionPaths?.Length > 0:
                return UninstallMethod.FileSystem;
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

        var (found, _2) = await GetUninstallStringAsync(item).ConfigureAwait(false);
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

            if (item.WinGetPackageId != null && item.WinGetPackageId.Any())
            {
                packageId = item.WinGetPackageId[0];
                source = "winget";
            }
            else if (!string.IsNullOrEmpty(item.MsStoreId))
            {
                packageId = item.MsStoreId;
                source = "msstore";
            }
            else
            {
                packageId = null;
                source = null;
            }

            if (string.IsNullOrEmpty(packageId))
            {
                logService.LogWarning($"No WinGet package ID for {item.Name}, falling back to registry");
                taskProgressService.UpdateProgress(0, $"No WinGet package ID for {item.Name}, trying registry fallback...");
                return await UninstallViaRegistryAsync(item, cancellationToken).ConfigureAwait(false);
            }

            var success = await winGetPackageInstaller.UninstallPackageAsync(packageId, source, item.Name, cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                // Fallback: try Chocolatey if available, then AppX, then registry.
                // AppX defends against detection mis-classification: if the item carries
                // an AppxPackageName but DetectedVia routed to WinGet (e.g. a Store-UI
                // install that winget surfaces as MSIX\... and fails to uninstall by ID),
                // the AppX path can still find and remove the actual installed package.
                if (!string.IsNullOrEmpty(item.ChocoPackageId))
                {
                    logService.LogWarning($"WinGet uninstall failed for {item.Name}, attempting Chocolatey fallback");
                    taskProgressService.UpdateProgress(0, $"WinGet uninstall failed for {item.Name}, trying Chocolatey...");
                    var chocoResult = await UninstallViaChocolateyAsync(item, cancellationToken).ConfigureAwait(false);
                    if (chocoResult.Success)
                        return chocoResult;
                }

                if (item.AppxPackageName?.Length > 0)
                {
                    logService.LogWarning($"WinGet uninstall failed for {item.Name}, attempting AppX fallback");
                    taskProgressService.UpdateProgress(0, $"Trying AppX fallback for {item.Name}...");
                    var appxResult = await UninstallViaAppxAsync(item, cancellationToken).ConfigureAwait(false);
                    if (appxResult.Success)
                        return appxResult;
                }

                logService.LogWarning($"Uninstall failed for {item.Name}, attempting registry fallback");
                taskProgressService.UpdateProgress(0, $"Trying registry fallback for {item.Name}...");
                return await UninstallViaRegistryAsync(item, cancellationToken).ConfigureAwait(false);
            }

            await CleanupStaleChocoRecordAsync(item, cancellationToken).ConfigureAwait(false);
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
                taskProgressService.UpdateProgress(0, $"Chocolatey uninstall failed for {item.Name}, trying registry fallback...");
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
            var (found, uninstallString) = await GetUninstallStringAsync(item).ConfigureAwait(false);

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

            await CleanupStaleChocoRecordAsync(item, cancellationToken).ConfigureAwait(false);
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

    // Best-effort: when WinGet or Registry has just removed the actual app, clear any
    // Chocolatey package record left behind (Chocolatey doesn't notice out-of-band
    // uninstalls, so its lib folder keeps reporting the package and the next detection
    // pass surfaces a ghost). No-op when the item wasn't tracked by Choco or the record
    // is already gone. Never fails the parent uninstall — this is hygiene, not correctness.
    private async Task CleanupStaleChocoRecordAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(item.ChocoPackageId))
            return;

        try
        {
            if (!await chocolateyService.IsChocolateyInstalledAsync(cancellationToken).ConfigureAwait(false))
                return;

            await chocolateyService.CleanupStalePackageRecordAsync(item.ChocoPackageId, item.Name, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logService.LogWarning($"Chocolatey ghost-record cleanup for {item.Name} errored: {ex.Message}");
        }
    }

    // AppX uninstall for External Apps. Uses the WinRT PackageManager API directly
    // (in-process, not via a saved PowerShell script). Persistent-removal scripts
    // are intentionally a Windows-Apps-only concern (see IBloatRemovalService); for
    // External Apps the user runs an uninstall once and sees terminal progress,
    // matching the WinGet/Chocolatey/Registry paths above.
    //
    // Scope is per-user (FindPackagesForUser("")) — we never call RemovePackageAsync
    // with the AllUsers option for External Apps. Provisioned-package deprovisioning
    // (which bloatware needs to stop re-install on new user creation) is also
    // intentionally absent here; Store-installed external apps don't carry that
    // re-provisioning concern.
    private async Task<OperationResult<bool>> UninstallViaAppxAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.AppxPackageName == null || item.AppxPackageName.Length == 0)
                return OperationResult<bool>.Failed($"No AppxPackageName configured for {item.Name}");

            cancellationToken.ThrowIfCancellationRequested();

            var packageManager = new Windows.Management.Deployment.PackageManager();
            var packages = packageManager.FindPackagesForUser("")
                .Where(p => item.AppxPackageName!.Any(name =>
                    string.Equals(p.Id.Name, name, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (packages.Count == 0)
            {
                logService.LogWarning($"No installed AppX package found for {item.Name} (looked for: {string.Join(", ", item.AppxPackageName!)})");
                return OperationResult<bool>.Failed($"No installed AppX package found for {item.Name}");
            }

            logService.LogInformation($"AppX uninstall for {item.Name}: removing {packages.Count} package(s)");
            taskProgressService.UpdateProgress(10, $"Removing {item.Name}...");

            int removed = 0;
            foreach (var package in packages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullName = package.Id.FullName;
                logService.LogInformation($"AppX: removing {fullName}");

                var op = packageManager.RemovePackageAsync(fullName);
                op.Progress = (info, progress) =>
                {
                    // DeploymentProgress.percentage is 0–100 but the registry/winget
                    // paths reserve 100% for the completion log line, so cap reports
                    // at 95% to keep the terminal output visually consistent. Also
                    // skip the WinRT API's initial 0% callback — we already reported
                    // 10% before calling RemovePackageAsync, and the bar mustn't go
                    // backwards.
                    var pct = Math.Min(95, (int)progress.percentage);
                    if (pct <= 10) return;
                    taskProgressService.UpdateProgress(pct, $"Removing {item.Name}... {pct}%");
                };
                using var ctReg = cancellationToken.Register(() => op.Cancel());
                var result = await op.AsTask().ConfigureAwait(false);

                if (result.ExtendedErrorCode != null)
                {
                    var errMsg = !string.IsNullOrEmpty(result.ErrorText)
                        ? result.ErrorText
                        : result.ExtendedErrorCode.Message;
                    logService.LogError($"AppX removal of {fullName} failed: {errMsg}");
                    return OperationResult<bool>.Failed($"AppX uninstall failed: {errMsg}");
                }
                removed++;
            }

            logService.LogInformation($"AppX uninstall for {item.Name} completed ({removed} package(s) removed)");
            taskProgressService.UpdateProgress(100, $"Uninstall of {item.Name} completed successfully");

            await CleanupStaleChocoRecordAsync(item, cancellationToken).ConfigureAwait(false);
            return OperationResult<bool>.Succeeded(true);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<bool>.Cancelled("Uninstall cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"AppX uninstall error for {item.Name}: {ex.Message}", ex);
            return OperationResult<bool>.Failed($"AppX uninstall failed: {ex.Message}");
        }
    }

    private Task<OperationResult<bool>> UninstallViaFileSystemAsync(ItemDefinition item, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.DetectionPaths == null || item.DetectionPaths.Length == 0)
                return Task.FromResult(OperationResult<bool>.Failed($"No detection paths configured for {item.Name}"));

            int deletedCount = 0;
            foreach (var rawPath in item.DetectionPaths)
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(rawPath);

                if (Directory.Exists(expandedPath))
                {
                    logService.LogInformation($"Deleting directory for {item.Name}: {expandedPath}");
                    Directory.Delete(expandedPath, recursive: true);
                    deletedCount++;
                }
                else if (File.Exists(expandedPath))
                {
                    logService.LogInformation($"Deleting file for {item.Name}: {expandedPath}");
                    File.Delete(expandedPath);
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                logService.LogInformation($"FileSystem uninstall for {item.Name} completed ({deletedCount} path(s) removed)");
                taskProgressService.UpdateProgress(100, $"Uninstall of {item.Name} completed successfully");
                return Task.FromResult(OperationResult<bool>.Succeeded(true));
            }

            logService.LogWarning($"No detection paths found on disk for {item.Name}");
            return Task.FromResult(OperationResult<bool>.Failed($"No files found to remove for {item.Name}"));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(OperationResult<bool>.Cancelled("Uninstall cancelled"));
        }
        catch (Exception ex)
        {
            logService.LogError($"FileSystem uninstall error for {item.Name}: {ex.Message}", ex);
            return Task.FromResult(OperationResult<bool>.Failed($"Uninstall failed: {ex.Message}"));
        }
    }

    private async Task<(bool Found, string UninstallString)> GetUninstallStringAsync(ItemDefinition item)
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

                            // Prefer explicit definition patterns when provided (e.g. AutoHotkey v1's
                            // "AutoHotkey 1.{version}"), since item.Name ("AutoHotkey v1") won't
                            // fuzzy-match the actual registry DisplayName ("AutoHotkey 1.1.37.02").
                            bool matched = false;

                            if (!string.IsNullOrEmpty(item.RegistrySubKeyName)
                                && AppStatusDiscoveryService.MatchesPattern(subKeyName, item.RegistrySubKeyName!))
                            {
                                matched = true;
                            }

                            if (!matched
                                && !string.IsNullOrEmpty(item.RegistryDisplayName)
                                && !string.IsNullOrEmpty(regDisplayName)
                                && AppStatusDiscoveryService.MatchesPattern(regDisplayName!, item.RegistryDisplayName!))
                            {
                                matched = true;
                            }

                            // Only fall back to fuzzy name matching when the definition didn't
                            // pin down a precise registry pattern. Apps that set RegistryDisplayName
                            // or RegistrySubKeyName (e.g. XnView, Steam) do so to AVOID collateral
                            // hits like "XnView MP" — fuzzy fallback would re-introduce that bug.
                            if (!matched
                                && string.IsNullOrEmpty(item.RegistryDisplayName)
                                && string.IsNullOrEmpty(item.RegistrySubKeyName)
                                && !string.IsNullOrEmpty(regDisplayName)
                                && IsFuzzyMatch(item.Name, regDisplayName!))
                            {
                                matched = true;
                            }

                            if (matched)
                            {
                                var uninstallString = subKey.GetValue("UninstallString")?.ToString();
                                if (!string.IsNullOrEmpty(uninstallString))
                                {
                                    logService.LogInformation($"Found uninstall string for {item.Name}: {uninstallString}");
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

    private static readonly Regex NonWordOrSpaceRegex = new(@"[^\w\s]", RegexOptions.Compiled);
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex MsiInstallToUninstallRegex = new(@"/I(\{[A-F0-9-]+\})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var normalized = NonWordOrSpaceRegex.Replace(input, "").ToLowerInvariant().Trim();
        return MultipleSpacesRegex.Replace(normalized, " ");
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
            existingArgs = MsiInstallToUninstallRegex.Replace(existingArgs, "/X$1");

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

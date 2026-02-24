using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;

/// <summary>
/// Handles WinGet CLI-based package install and uninstall operations.
/// </summary>
public class WinGetPackageInstaller : IWinGetPackageInstaller
{
    private readonly WinGetComSession _comSession;
    private readonly ITaskProgressService _taskProgressService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localization;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IFileSystemService _fileSystemService;

    public WinGetPackageInstaller(
        WinGetComSession comSession,
        ITaskProgressService taskProgressService,
        ILogService logService,
        ILocalizationService localization,
        IInteractiveUserService interactiveUserService,
        IFileSystemService fileSystemService)
    {
        _comSession = comSession;
        _taskProgressService = taskProgressService;
        _logService = logService;
        _localization = localization;
        _interactiveUserService = interactiveUserService;
        _fileSystemService = fileSystemService;
    }

    public async Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default)
    {
        // Bundled CLI is always available if the app is correctly installed
        var exePath = WinGetCliRunner.GetWinGetExePath(_interactiveUserService);
        if (exePath != null && _fileSystemService.FileExists(exePath))
            return true;

        // Fallback: try COM init (covers edge cases)
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            return await Task.Run(() => _comSession.EnsureComInitialized(), linkedCts.Token).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    public async Task<PackageInstallResult> InstallPackageAsync(string packageId, string? source = null, string? displayName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

        displayName ??= packageId;

        _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_WinGet_CheckingPrerequisites", displayName));

        if (!await IsWinGetInstalledAsync(cancellationToken).ConfigureAwait(false))
        {
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_FailedInstallManager", displayName));
            return PackageInstallResult.Failed(InstallFailureReason.WinGetNotAvailable, "WinGet CLI not found");
        }

        try
        {
            _taskProgressService?.UpdateProgress(20, _localization.GetString("Progress_WinGet_StartingInstallation", displayName));

            var arguments = $"install --id {packageId} --silent --accept-package-agreements --accept-source-agreements --force --disable-interactivity";
            if (!string.IsNullOrEmpty(source))
                arguments += $" --source {source}";

            _logService?.LogInformation($"[winget] Running: winget {arguments}");

            // Emit metadata header for the task output dialog
            var wingetExe = WinGetCliRunner.GetWinGetExePath(_interactiveUserService) ?? "winget";
            var startTime = DateTime.Now;
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = $"Command: {wingetExe} {arguments}"
            });
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = $"Start Time: \"{startTime:yyyy/MM/dd HH:mm:ss}\""
            });
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = "---"
            });

            var lastProgressReport = DateTime.MinValue;

            var lastLoggedPhase = (WinGetProgressParser.WinGetPhase?)null;

            var result = await WinGetCliRunner.RunAsync(
                arguments,
                onOutputLine: line =>
                {
                    try
                    {
                        // Translate raw resource keys to human-readable text
                        var displayLine = WinGetProgressParser.TranslateLine(line);

                        var progress = WinGetProgressParser.ParseLine(line);
                        if (progress != null)
                        {
                            // Only log phase transitions (Found, Installing, Complete, Error) — skip noisy progress updates
                            if (progress.Phase != lastLoggedPhase
                                && progress.Phase is WinGetProgressParser.WinGetPhase.Found
                                    or WinGetProgressParser.WinGetPhase.Installing
                                    or WinGetProgressParser.WinGetPhase.Complete
                                    or WinGetProgressParser.WinGetPhase.Error)
                            {
                                lastLoggedPhase = progress.Phase;
                                if (displayLine != null)
                                    _logService?.LogInformation($"[winget] {displayLine}");
                            }

                            var progressPercent = progress.Phase switch
                            {
                                WinGetProgressParser.WinGetPhase.Found => 30,
                                WinGetProgressParser.WinGetPhase.Downloading => 30 + (int)((progress.Percent ?? 0) * 0.4),
                                WinGetProgressParser.WinGetPhase.Installing => 70 + (int)((progress.Percent ?? 0) * 0.25),
                                WinGetProgressParser.WinGetPhase.Complete => 100,
                                _ => 50
                            };

                            var statusText = progress.Phase switch
                            {
                                WinGetProgressParser.WinGetPhase.Found => _localization.GetString("Progress_WinGet_FoundPackage", displayName),
                                WinGetProgressParser.WinGetPhase.Downloading => _localization.GetString("Progress_Downloading", displayName),
                                WinGetProgressParser.WinGetPhase.Installing => _localization.GetString("Progress_Installing", displayName),
                                WinGetProgressParser.WinGetPhase.Complete => _localization.GetString("Progress_WinGet_InstalledSuccess", displayName),
                                _ => _localization.GetString("Progress_Processing", displayName)
                            };

                            // Throttle progress updates to avoid flooding the UI (allow Complete through unconditionally)
                            var now = DateTime.UtcNow;
                            if (progress.Phase == WinGetProgressParser.WinGetPhase.Complete
                                || (now - lastProgressReport).TotalMilliseconds >= 250)
                            {
                                lastProgressReport = now;
                                _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                                {
                                    Progress = progressPercent,
                                    StatusText = statusText,
                                    // Don't emit TerminalOutput for lines with a parsed percentage —
                                    // these are \r\n re-emissions of progress lines already handled
                                    // by the onProgressLine callback. But allow Complete phase through
                                    // so "Successfully installed" appears in the terminal output.
                                    TerminalOutput = progress.Percent.HasValue && progress.Phase != WinGetProgressParser.WinGetPhase.Complete
                                        ? null : (displayLine ?? line),
                                });
                            }
                        }
                        else if (displayLine != null)
                        {
                            // Non-parsed text lines: send to UI terminal only (skip log to avoid noise)
                            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                            {
                                TerminalOutput = displayLine
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogWarning($"Progress reporting error (ignored): {ex.Message}");
                    }
                },
                onErrorLine: line =>
                {
                    _logService?.LogWarning($"[winget-err] {line}");
                },
                cancellationToken: cancellationToken,
                interactiveUserService: _interactiveUserService,
                onProgressLine: line =>
                {
                    try
                    {
                        // Progress lines (\r-terminated) are transient — send to terminal output
                        // with IsProgressIndicator=true for real-time display and replacement
                        var displayLine = WinGetProgressParser.TranslateLine(line);
                        _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                        {
                            TerminalOutput = displayLine ?? line,
                            IsProgressIndicator = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogWarning($"Progress reporting error (ignored): {ex.Message}");
                    }
                }).ConfigureAwait(false);

            // Emit metadata footer for the task output dialog
            var endTime = DateTime.Now;
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = "---"
            });
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = $"End Time: \"{endTime:yyyy/MM/dd HH:mm:ss}\""
            });
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = $"Process return value: \"{result.ExitCode}\" (0x{result.ExitCode:X8})"
            });

            // If the user cancelled while the process was running, throw before
            // checking the exit code so the OperationCanceledException handler fires
            // and the Chocolatey fallback prompt is never reached.
            cancellationToken.ThrowIfCancellationRequested();

            if (WinGetExitCodes.IsSuccess(result.ExitCode))
            {
                _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_InstalledSuccess", displayName));
                return PackageInstallResult.Succeeded();
            }

            var failureReason = WinGetExitCodes.MapExitCode(result.ExitCode);
            var errorMessage = GetInstallErrorMessageCli(packageId, failureReason, result.ExitCode);
            _logService?.LogError($"Installation failed for {packageId}: {errorMessage} (exit code: {result.ExitCode})");
            _taskProgressService?.UpdateProgress(0, errorMessage);
            return PackageInstallResult.Failed(failureReason, errorMessage);
        }
        catch (OperationCanceledException)
        {
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_InstallationCancelled", displayName));
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error installing {packageId}: {ex.Message}");

            var errorMessage = _localization.GetString("Progress_WinGet_InstallationError", displayName, ex.Message);
            _taskProgressService?.UpdateProgress(0, errorMessage);
            return PackageInstallResult.Failed(InstallFailureReason.Other, errorMessage);
        }
    }

    public async Task<bool> UninstallPackageAsync(string packageId, string? source = null, string? displayName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

        displayName ??= packageId;

        _taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_WinGet_CheckingPrerequisitesUninstall", displayName));

        if (!await IsWinGetInstalledAsync(cancellationToken).ConfigureAwait(false))
        {
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_NotInstalled"));
            return false;
        }

        try
        {
            _taskProgressService?.UpdateProgress(20, _localization.GetString("Progress_WinGet_StartingUninstallation", displayName));

            var arguments = $"uninstall --id {packageId} --silent --accept-source-agreements --force --disable-interactivity";
            if (!string.IsNullOrEmpty(source))
                arguments += $" --source {source}";

            _logService?.LogInformation($"[winget] Running: winget {arguments}");

            // Emit metadata header for the task output dialog
            var wingetExe = WinGetCliRunner.GetWinGetExePath(_interactiveUserService) ?? "winget";
            var startTime = DateTime.Now;
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = $"Command: {wingetExe} {arguments}"
            });
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = $"Start Time: \"{startTime:yyyy/MM/dd HH:mm:ss}\""
            });
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = "---"
            });

            var lastProgressReport = DateTime.MinValue;

            var lastLoggedPhase = (WinGetProgressParser.WinGetPhase?)null;

            var result = await WinGetCliRunner.RunAsync(
                arguments,
                onOutputLine: line =>
                {
                    try
                    {
                        // Translate raw resource keys to human-readable text
                        var displayLine = WinGetProgressParser.TranslateLine(line);

                        var progress = WinGetProgressParser.ParseLine(line);
                        if (progress != null)
                        {
                            // Only log phase transitions (Found, Uninstalling, Complete, Error) — skip noisy progress updates
                            if (progress.Phase != lastLoggedPhase
                                && progress.Phase is WinGetProgressParser.WinGetPhase.Found
                                    or WinGetProgressParser.WinGetPhase.Uninstalling
                                    or WinGetProgressParser.WinGetPhase.Complete
                                    or WinGetProgressParser.WinGetPhase.Error)
                            {
                                lastLoggedPhase = progress.Phase;
                                if (displayLine != null)
                                    _logService?.LogInformation($"[winget] {displayLine}");
                            }

                            var progressPercent = progress.Phase switch
                            {
                                WinGetProgressParser.WinGetPhase.Found => 30,
                                WinGetProgressParser.WinGetPhase.Uninstalling => 60,
                                WinGetProgressParser.WinGetPhase.Complete => 100,
                                _ => 50
                            };

                            var statusText = progress.Phase switch
                            {
                                WinGetProgressParser.WinGetPhase.Found => _localization.GetString("Progress_WinGet_FoundPackage", displayName),
                                WinGetProgressParser.WinGetPhase.Uninstalling => _localization.GetString("Progress_Uninstalling", displayName),
                                WinGetProgressParser.WinGetPhase.Complete => _localization.GetString("Progress_WinGet_UninstalledSuccess", displayName),
                                _ => _localization.GetString("Progress_Processing", displayName)
                            };

                            // Throttle progress updates to avoid flooding the UI (allow Complete through unconditionally)
                            var now = DateTime.UtcNow;
                            if (progress.Phase == WinGetProgressParser.WinGetPhase.Complete
                                || (now - lastProgressReport).TotalMilliseconds >= 250)
                            {
                                lastProgressReport = now;
                                _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                                {
                                    Progress = progressPercent,
                                    StatusText = statusText,
                                    TerminalOutput = progress.Percent.HasValue && progress.Phase != WinGetProgressParser.WinGetPhase.Complete
                                        ? null : (displayLine ?? line),
                                });
                            }
                        }
                        else if (displayLine != null)
                        {
                            // Non-parsed text lines: send to UI terminal only (skip log to avoid noise)
                            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                            {
                                TerminalOutput = displayLine
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogWarning($"Progress reporting error (ignored): {ex.Message}");
                    }
                },
                onErrorLine: line =>
                {
                    _logService?.LogWarning($"[winget-err] {line}");
                },
                cancellationToken: cancellationToken,
                interactiveUserService: _interactiveUserService,
                onProgressLine: line =>
                {
                    try
                    {
                        var displayLine = WinGetProgressParser.TranslateLine(line);
                        _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                        {
                            TerminalOutput = displayLine ?? line,
                            IsProgressIndicator = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogWarning($"Progress reporting error (ignored): {ex.Message}");
                    }
                }).ConfigureAwait(false);

            // Emit metadata footer for the task output dialog
            var endTime = DateTime.Now;
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = "---"
            });
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = $"End Time: \"{endTime:yyyy/MM/dd HH:mm:ss}\""
            });
            _taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
            {
                TerminalOutput = $"Process return value: \"{result.ExitCode}\" (0x{result.ExitCode:X8})"
            });

            if (WinGetExitCodes.IsSuccess(result.ExitCode))
            {
                // Verify the package is actually gone — some uninstallers are interactive
                // and WinGet reports success as soon as it launches them.
                _taskProgressService?.UpdateProgress(95, _localization.GetString("Progress_WinGet_VerifyingUninstall", displayName));

                bool stillInstalled = await IsPackageStillInstalledAsync(packageId, source, cancellationToken).ConfigureAwait(false);

                if (stillInstalled)
                {
                    _logService?.LogInformation($"[winget] {packageId} still detected after uninstall — waiting for interactive uninstaller");
                    _taskProgressService?.UpdateProgress(95, _localization.GetString("Progress_WinGet_WaitingForUninstaller", displayName));

                    const int pollIntervalMs = 3000;
                    const int maxWaitMs = 60_000;
                    int elapsed = 0;

                    while (elapsed < maxWaitMs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
                        elapsed += pollIntervalMs;

                        if (!await IsPackageStillInstalledAsync(packageId, source, cancellationToken).ConfigureAwait(false))
                        {
                            stillInstalled = false;
                            _logService?.LogInformation($"[winget] {packageId} confirmed uninstalled after {elapsed / 1000}s");
                            break;
                        }
                    }
                }

                if (!stillInstalled)
                {
                    _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_UninstalledSuccess", displayName));
                    return true;
                }

                // Timed out — uninstaller may require user interaction
                _logService?.LogWarning($"[winget] {packageId} still detected after 60s wait — uninstaller may require user interaction");
                _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_UninstalledSuccess", displayName));
                return true; // WinGet did report success; don't block the UI
            }

            // WinGet wraps any non-zero exit code from the underlying uninstaller
            // into EXEC_UNINSTALL_COMMAND_FAILED (0x8A150030), even when the uninstall
            // actually succeeded (e.g. Chromium-based apps always return exit code 19).
            // Before declaring failure, verify whether the package is actually still installed.
            if (WinGetExitCodes.IsUninstallVerifiable(result.ExitCode))
            {
                _logService?.LogInformation($"[winget] {packageId} returned 0x{result.ExitCode:X8} — verifying whether package was actually removed");
                _taskProgressService?.UpdateProgress(95, _localization.GetString("Progress_WinGet_VerifyingUninstall", displayName));

                bool stillInstalled = await IsPackageStillInstalledAsync(packageId, source, cancellationToken).ConfigureAwait(false);

                if (stillInstalled)
                {
                    _logService?.LogInformation($"[winget] {packageId} still detected after failed uninstall — waiting for interactive uninstaller");
                    _taskProgressService?.UpdateProgress(95, _localization.GetString("Progress_WinGet_WaitingForUninstaller", displayName));

                    const int pollIntervalMs = 3000;
                    const int maxWaitMs = 60_000;
                    int elapsed = 0;

                    while (elapsed < maxWaitMs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
                        elapsed += pollIntervalMs;

                        if (!await IsPackageStillInstalledAsync(packageId, source, cancellationToken).ConfigureAwait(false))
                        {
                            stillInstalled = false;
                            _logService?.LogInformation($"[winget] {packageId} confirmed uninstalled after {elapsed / 1000}s (despite exit code 0x{result.ExitCode:X8})");
                            break;
                        }
                    }
                }

                if (!stillInstalled)
                {
                    _logService?.LogInformation($"[winget] {packageId} verified as uninstalled despite WinGet exit code 0x{result.ExitCode:X8}");
                    _taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_UninstalledSuccess", displayName));
                    return true;
                }

                // Package is genuinely still installed — fall through to failure reporting
                _logService?.LogWarning($"[winget] {packageId} is still installed after verification — uninstall truly failed");
            }

            var failureReason = WinGetExitCodes.MapExitCode(result.ExitCode);
            var errorMessage = GetUninstallErrorMessageCli(packageId, failureReason, result.ExitCode);
            _logService?.LogError($"Uninstallation failed for {packageId}: {errorMessage} (exit code: {result.ExitCode})");
            _taskProgressService?.UpdateProgress(0, errorMessage);
            return false;
        }
        catch (OperationCanceledException)
        {
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_UninstallationCancelled", displayName));
            throw;
        }
        catch (Exception ex)
        {
            _logService?.LogError($"Error uninstalling {packageId}: {ex.Message}");
            _taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_UninstallationError", displayName, ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Quick check: runs "winget list --exact --id {packageId}" to see if a package is still installed.
    /// Returns true if the package is still present.
    /// </summary>
    private async Task<bool> IsPackageStillInstalledAsync(string packageId, string? source, CancellationToken cancellationToken)
    {
        try
        {
            var arguments = $"list --id {packageId} --accept-source-agreements --disable-interactivity";
            if (!string.IsNullOrEmpty(source))
                arguments += $" --source {source}";

            var result = await WinGetCliRunner.RunAsync(
                arguments,
                cancellationToken: cancellationToken,
                timeoutMs: 10_000,
                interactiveUserService: _interactiveUserService).ConfigureAwait(false);

            // Exit code 0 = found (still installed), non-zero = not found
            return result.ExitCode == 0;
        }
        catch
        {
            // If the check fails, assume it's gone to avoid blocking
            return false;
        }
    }

    private string GetInstallErrorMessageCli(string packageId, InstallFailureReason reason, int exitCode)
    {
        return reason switch
        {
            InstallFailureReason.PackageNotFound => _localization.GetString("Progress_WinGet_PackageNotFound", packageId),
            InstallFailureReason.BlockedByPolicy => _localization.GetString("Progress_WinGet_Error_BlockedByPolicy", packageId),
            InstallFailureReason.DownloadError => _localization.GetString("Progress_WinGet_Error_DownloadError", packageId),
            InstallFailureReason.HashMismatchOrInstallError => _localization.GetString("Progress_WinGet_Error_InstallError", packageId, exitCode),
            InstallFailureReason.NoApplicableInstallers => _localization.GetString("Progress_WinGet_Error_NoApplicableInstallers", packageId),
            InstallFailureReason.AgreementsNotAccepted => _localization.GetString("Progress_WinGet_Error_AgreementsNotAccepted", packageId),
            InstallFailureReason.NetworkError => _localization.GetString("Progress_WinGet_NetworkError", packageId),
            _ => _localization.GetString("Progress_WinGet_Error_InstallFailed", packageId, exitCode)
        };
    }

    private string GetUninstallErrorMessageCli(string packageId, InstallFailureReason reason, int exitCode)
    {
        return reason switch
        {
            InstallFailureReason.PackageNotFound => _localization.GetString("Progress_WinGet_PackageNotInstalled", packageId),
            InstallFailureReason.BlockedByPolicy => _localization.GetString("Progress_WinGet_Error_UninstallBlockedByPolicy", packageId),
            _ => _localization.GetString("Progress_WinGet_Error_UninstallFailed", packageId, exitCode)
        };
    }
}

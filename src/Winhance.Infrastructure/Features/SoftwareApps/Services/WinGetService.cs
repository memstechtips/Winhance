using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Management.Deployment;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using WindowsPackageManager.Interop;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    public class WinGetService : IWinGetService
    {
        private readonly ITaskProgressService _taskProgressService;
        private readonly ILogService _logService;
        private readonly ILocalizationService _localization;
        private readonly IInteractiveUserService _interactiveUserService;

        private WindowsPackageManagerFactory? _winGetFactory;
        private PackageManager? _packageManager;
        private readonly object _factoryLock = new();
        private bool _isInitialized;
        private bool _comInitTimedOut;
        private bool _systemWinGetAvailable;

        private const int ComInitTimeoutSeconds = 5;
        private const int ComOperationTimeoutSeconds = 15;

        public event EventHandler? WinGetInstalled;

        public bool IsSystemWinGetAvailable => _systemWinGetAvailable;

        public WinGetService(
            ITaskProgressService taskProgressService,
            ILogService logService,
            ILocalizationService localization,
            IInteractiveUserService interactiveUserService)
        {
            _taskProgressService = taskProgressService;
            _logService = logService;
            _localization = localization;
            _interactiveUserService = interactiveUserService;
        }

        #region COM Initialization (for detection)

        private bool EnsureComInitialized()
        {
            if (_isInitialized && _packageManager != null)
                return true;

            if (_comInitTimedOut)
                return false;

            lock (_factoryLock)
            {
                if (_isInitialized && _packageManager != null)
                    return true;

                if (_comInitTimedOut)
                    return false;

                try
                {
                    // Winhance always runs as admin with self-contained AppSdk.
                    // StandardFactory + ALLOW_LOWER_TRUST_REGISTRATION is the only approach
                    // that works in this configuration.
                    // ElevatedFactory (winrtact.dll) hangs in self-contained mode:
                    // https://github.com/microsoft/winget-cli/issues/4377
                    _logService?.LogInformation("Initializing WinGet COM API via StandardFactory");
                    _winGetFactory = new WindowsPackageManagerStandardFactory(
                        ClsidContext.Prod,
                        allowLowerTrustRegistration: true);
                    _packageManager = _winGetFactory.CreatePackageManager();
                    _isInitialized = true;
                    _logService?.LogInformation("WinGet COM API initialized successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    _logService?.LogError($"Failed to initialize WinGet COM API: {ex.Message}");
                    _isInitialized = false;
                    _packageManager = null;
                    _winGetFactory = null;
                    return false;
                }
            }
        }

        private void ResetFactory()
        {
            lock (_factoryLock)
            {
                _isInitialized = false;
                _comInitTimedOut = false;
                _packageManager = null;
                _winGetFactory = null;
            }
        }

        #endregion

        #region CLI-based Install / Uninstall

        public async Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default)
        {
            // Bundled CLI is always available if the app is correctly installed
            var exePath = WinGetCliRunner.GetWinGetExePath(_interactiveUserService);
            if (exePath != null && File.Exists(exePath))
                return true;

            // Fallback: try COM init (covers edge cases)
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                return await Task.Run(() => EnsureComInitialized(), linkedCts.Token).ConfigureAwait(false);
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

        #endregion

        #region Simplified Bootstrapping

        public async Task<bool> InstallWinGetAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logService?.LogInformation("Starting AppInstaller installation...");

                var installer = new WinGetInstaller(_logService, _localization, _taskProgressService);
                var (success, message) = await installer.InstallAsync(cancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    _logService?.LogError($"AppInstaller installation failed: {message}");
                    return false;
                }

                _logService?.LogInformation("AppInstaller installed, waiting for COM API readiness...");

                // Retry COM init with loop (10 retries, 3s delay)
                for (int i = 0; i < 10; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(3000, cancellationToken).ConfigureAwait(false);

                    ResetFactory();
                    if (await Task.Run(() => EnsureComInitialized(), cancellationToken).ConfigureAwait(false))
                    {
                        _logService?.LogInformation($"COM API ready after {i + 1} attempt(s)");
                        _systemWinGetAvailable = true;
                        WinGetInstalled?.Invoke(this, EventArgs.Empty);
                        return true;
                    }

                    _logService?.LogInformation($"COM init attempt {i + 1}/10 failed, retrying...");
                }

                _logService?.LogWarning("COM API did not become ready after AppInstaller installation");
                // Still consider it a partial success — system winget may be available
                _systemWinGetAvailable = WinGetCliRunner.IsSystemWinGetAvailable(_interactiveUserService);
                if (_systemWinGetAvailable)
                {
                    WinGetInstalled?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                _logService?.LogWarning("AppInstaller installation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error installing AppInstaller: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logService?.LogInformation("Checking WinGet availability...");

                _systemWinGetAvailable = WinGetCliRunner.IsSystemWinGetAvailable(_interactiveUserService);
                _logService?.LogInformation($"System winget available: {_systemWinGetAvailable}");

                if (_systemWinGetAvailable)
                {
                    // OTS: COM activation uses the admin's MSIX registration but the
                    // desktop session belongs to the standard user — the out-of-process
                    // COM server cannot start in this mismatched state. Skip straight
                    // to CLI fallback.
                    if (_interactiveUserService.IsOtsElevation)
                    {
                        _logService?.LogInformation("OTS elevation detected — skipping COM init (CLI fallback will be used)");
                        _comInitTimedOut = true;
                    }
                    else
                    {
                        // System winget is present — try COM init (likely succeeds).
                        // Use Task.WhenAny to enforce a timeout without nesting Task.Run
                        // (nested Task.Run breaks COM activation context).
                        try
                        {
                            var initTask = Task.Run(() => EnsureComInitialized(), cancellationToken);
                            var completed = await Task.WhenAny(
                                initTask, Task.Delay(TimeSpan.FromSeconds(ComInitTimeoutSeconds), cancellationToken)).ConfigureAwait(false);

                            if (completed != initTask)
                            {
                                _logService?.LogWarning("COM init timed out in EnsureWinGetReadyAsync — using CLI fallback");
                                _comInitTimedOut = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService?.LogWarning($"COM init failed (detection may use CLI fallback): {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Only bundled winget available — skip COM attempt (will fail anyway)
                    _logService?.LogInformation("No system winget — bundled CLI will be used for detection");
                }

                // Return true as long as bundled or system winget exists
                var exePath = WinGetCliRunner.GetWinGetExePath(_interactiveUserService);
                if (exePath == null || !File.Exists(exePath))
                {
                    _logService?.LogWarning("WinGet CLI not found — install/uninstall will fail");
                    return false;
                }

                _logService?.LogInformation($"WinGet CLI found at: {exePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error checking WinGet availability: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpgradeAppInstallerAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var bundledPath = WinGetCliRunner.GetBundledWinGetExePath();
                if (bundledPath == null)
                {
                    _logService?.LogWarning("Cannot upgrade AppInstaller — bundled winget not found");
                    return false;
                }

                _logService?.LogInformation("Attempting to upgrade AppInstaller via bundled winget...");

                var arguments = "upgrade --id Microsoft.AppInstaller --exact --silent --accept-source-agreements --accept-package-agreements --force --disable-interactivity";

                var result = await WinGetCliRunner.RunAsync(
                    arguments,
                    onOutputLine: line =>
                    {
                        if (!IsWinGetOutputNoise(line))
                            _logService?.LogInformation($"[winget-upgrade] {line}");
                    },
                    onErrorLine: line => _logService?.LogWarning($"[winget-upgrade-err] {line}"),
                    cancellationToken: cancellationToken,
                    timeoutMs: 120_000,
                    exePathOverride: bundledPath).ConfigureAwait(false);

                if (WinGetExitCodes.IsSuccess(result.ExitCode))
                {
                    _logService?.LogInformation($"AppInstaller upgrade succeeded (exit code: 0x{result.ExitCode:X8})");
                    return true;
                }

                _logService?.LogWarning($"AppInstaller upgrade returned exit code: 0x{result.ExitCode:X8}");
                return false;
            }
            catch (Exception ex)
            {
                _logService?.LogWarning($"AppInstaller upgrade failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns true if the winget output line is visual noise (progress bars, spinners, blank lines)
        /// that should not be written to the log file.
        /// </summary>
        private static bool IsWinGetOutputNoise(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return true;

            var trimmed = line.Trim();

            // Progress bar characters
            if (trimmed.Contains('█') || trimmed.Contains('▒'))
                return true;

            // Spinner characters (single char lines like " - ", " \ ", " | ", " / ")
            if (trimmed.Length <= 2 && (trimmed == "-" || trimmed == "\\" || trimmed == "|" || trimmed == "/"))
                return true;

            return false;
        }

        #endregion

        #region Detection (COM with CLI fallback)

        public async Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default)
        {
            var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Only try COM if system winget is available (COM requires DesktopAppInstaller MSIX)
                if (_systemWinGetAvailable && EnsureComInitialized() && _packageManager != null && _winGetFactory != null)
                {
                    var comResult = await GetInstalledPackageIdsViaCom(cancellationToken).ConfigureAwait(false);
                    if (comResult != null)
                        return comResult;
                    _logService?.LogInformation("COM detection failed/timed out, falling back to CLI");
                }

                // CLI fallback (uses winget export → JSON)
                _logService?.LogInformation("COM not available, falling back to CLI for installed package detection");
                return await GetInstalledPackageIdsViaCli(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in GetInstalledPackageIdsAsync: {ex.Message}");
                return installedPackageIds;
            }
        }

        private async Task<HashSet<string>?> GetInstalledPackageIdsViaCom(CancellationToken cancellationToken)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(ComOperationTimeoutSeconds));

                return await Task.Run(() =>
                {
                    var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var catalogs = _packageManager!.GetPackageCatalogs().ToArray();
                    var wingetCatalog = catalogs.FirstOrDefault(c =>
                        c.Info.Name.Equals("winget", StringComparison.OrdinalIgnoreCase));

                    if (wingetCatalog == null && catalogs.Length > 0)
                    {
                        wingetCatalog = catalogs[0];
                        _logService?.LogInformation($"Using catalog: {wingetCatalog.Info.Name}");
                    }

                    if (wingetCatalog == null)
                    {
                        _logService?.LogWarning("No package catalogs available");
                        return installedPackageIds;
                    }

                    var compositeOptions = _winGetFactory!.CreateCreateCompositePackageCatalogOptions();
                    compositeOptions.Catalogs.Add(wingetCatalog);
                    compositeOptions.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;

                    var compositeCatalogRef = _packageManager.CreateCompositePackageCatalog(compositeOptions);
                    var connectResult = compositeCatalogRef.Connect();

                    if (connectResult.Status != ConnectResultStatus.Ok)
                    {
                        _logService?.LogError($"Failed to connect to composite catalog: {connectResult.Status}");
                        return installedPackageIds;
                    }

                    var findOptions = _winGetFactory.CreateFindPackagesOptions();
                    var filter = _winGetFactory.CreatePackageMatchFilter();
                    filter.Field = PackageMatchField.Id;
                    filter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
                    filter.Value = "";
                    findOptions.Filters.Add(filter);

                    var findResult = connectResult.PackageCatalog.FindPackages(findOptions);
                    var matches = findResult.Matches.ToArray();

                    foreach (var match in matches)
                    {
                        var packageId = match.CatalogPackage?.Id;
                        if (!string.IsNullOrEmpty(packageId))
                        {
                            installedPackageIds.Add(packageId);
                        }
                    }

                    _logService?.LogInformation($"WinGet COM API: Found {installedPackageIds.Count} installed packages");
                    return installedPackageIds;
                }, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logService?.LogWarning("COM package enumeration timed out");
                return null;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error getting installed packages via COM API: {ex.Message}");
                return null;
            }
        }

        private async Task<HashSet<string>> GetInstalledPackageIdsViaCli(CancellationToken cancellationToken)
        {
            var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var cacheDir = Path.Combine(
                _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance", "Cache");
            Directory.CreateDirectory(cacheDir);
            var exportPath = Path.Combine(cacheDir, "winget-packages.json");

            const int maxRetries = 3;
            const int retryDelayMs = 2000;
            const int timeoutMs = 10_000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Clean up any previous export file
                    if (File.Exists(exportPath))
                        File.Delete(exportPath);

                    var arguments = $"export -o \"{exportPath}\" --accept-source-agreements --nowarn --disable-interactivity";
                    _logService?.LogInformation($"[winget-bundled] Running: winget {arguments} (attempt {attempt}/{maxRetries})");

                    var result = await WinGetCliRunner.RunAsync(
                        arguments,
                        cancellationToken: cancellationToken,
                        timeoutMs: timeoutMs,
                        exePathOverride: WinGetCliRunner.GetBundledWinGetExePath(),
                        interactiveUserService: _interactiveUserService).ConfigureAwait(false);

                    if (result.ExitCode != 0)
                    {
                        _logService?.LogWarning($"winget export failed with exit code 0x{result.ExitCode:X8} (attempt {attempt}/{maxRetries})");

                        // FailedToOpenAllSources = sources not initialized (no internet / no AppInstaller)
                        // No point retrying — the sources won't appear on their own
                        if (result.ExitCode == WinGetExitCodes.FailedToOpenAllSources)
                        {
                            _logService?.LogWarning("Sources not available — skipping retries");
                            return installedPackageIds;
                        }

                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                        return installedPackageIds;
                    }

                    if (!File.Exists(exportPath))
                    {
                        _logService?.LogWarning($"winget export succeeded but file not found (attempt {attempt}/{maxRetries})");
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                        return installedPackageIds;
                    }

                    // Parse JSON: root.Sources[].Packages[].PackageIdentifier
                    var jsonBytes = await File.ReadAllBytesAsync(exportPath, cancellationToken).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(jsonBytes);

                    if (doc.RootElement.TryGetProperty("Sources", out var sourcesElement))
                    {
                        foreach (var source in sourcesElement.EnumerateArray())
                        {
                            if (source.TryGetProperty("Packages", out var packagesElement))
                            {
                                foreach (var package in packagesElement.EnumerateArray())
                                {
                                    if (package.TryGetProperty("PackageIdentifier", out var idElement))
                                    {
                                        var id = idElement.GetString();
                                        if (!string.IsNullOrEmpty(id))
                                            installedPackageIds.Add(id);
                                    }
                                }
                            }
                        }
                    }

                    _logService?.LogInformation($"WinGet CLI (export): Found {installedPackageIds.Count} installed packages");
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logService?.LogError($"Error getting installed packages via winget export (attempt {attempt}/{maxRetries}): {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // Clean up export file
            try
            {
                if (File.Exists(exportPath))
                    File.Delete(exportPath);
            }
            catch (Exception ex) { _logService?.LogDebug($"Best-effort export file cleanup failed: {ex.Message}"); }

            return installedPackageIds;
        }

        public async Task<string?> GetInstallerTypeAsync(string packageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return null;

            try
            {
                // Try COM first
                if (EnsureComInitialized())
                {
                    var package = await FindPackageAsync(packageId, cancellationToken).ConfigureAwait(false);
                    if (package?.DefaultInstallVersion != null)
                    {
                        var catalogInfo = package.DefaultInstallVersion.PackageCatalog?.Info;
                        if (catalogInfo != null)
                        {
                            _logService?.LogInformation($"Package {packageId} from catalog: {catalogInfo.Name}");
                        }
                    }
                }

                // CLI fallback: parse "winget show" output for Installer Type
                var result = await WinGetCliRunner.RunAsync(
                    $"show --id {packageId} --accept-source-agreements --disable-interactivity",
                    cancellationToken: cancellationToken,
                    timeoutMs: 60_000,
                    interactiveUserService: _interactiveUserService).ConfigureAwait(false);

                if (result.ExitCode == 0)
                {
                    foreach (var rawLine in result.StandardOutput.Split('\n'))
                    {
                        var line = rawLine.Trim();
                        if (line.StartsWith("Installer Type:", StringComparison.OrdinalIgnoreCase))
                        {
                            var installerType = line.Substring("Installer Type:".Length).Trim();
                            if (!string.IsNullOrEmpty(installerType))
                            {
                                _logService?.LogInformation($"Package {packageId} installer type: {installerType}");
                                return installerType;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logService?.LogWarning($"Could not determine installer type for {packageId}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region COM Detection Internals (unchanged)

        private async Task<CatalogPackage?> FindPackageAsync(string packageId, CancellationToken cancellationToken)
        {
            if (!EnsureComInitialized() || _packageManager == null || _winGetFactory == null)
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    var catalogs = _packageManager.GetPackageCatalogs().ToArray();

                    foreach (var catalogRef in catalogs)
                    {
                        var connectResult = catalogRef.Connect();
                        if (connectResult.Status != ConnectResultStatus.Ok)
                            continue;

                        var findOptions = _winGetFactory.CreateFindPackagesOptions();
                        var filter = _winGetFactory.CreatePackageMatchFilter();
                        filter.Field = PackageMatchField.Id;
                        filter.Option = PackageFieldMatchOption.EqualsCaseInsensitive;
                        filter.Value = packageId;
                        findOptions.Filters.Add(filter);

                        var findResult = connectResult.PackageCatalog.FindPackages(findOptions);

                        var match = findResult.Matches.ToArray().FirstOrDefault();
                        if (match != null)
                        {
                            return match.CatalogPackage;
                        }
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    _logService?.LogError($"Error finding package {packageId}: {ex.Message}");
                    return null;
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Error Message Helpers

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

        #endregion
    }
}

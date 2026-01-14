using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    public class WinGetService(
        ITaskProgressService taskProgressService,
        IPowerShellExecutionService powerShellExecutionService,
        ILogService logService,
        ILocalizationService localization) : IWinGetService
    {
        private readonly ILocalizationService _localization = localization;
        private string _wingetExePath = null;

        private async Task<string> GetWinGetFromAppXAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string script = @"
                    $package = Get-AppxPackage -Name Microsoft.DesktopAppInstaller
                    if ($package) {
                        $installLocation = $package.InstallLocation
                        $wingetPath = Join-Path $installLocation 'winget.exe'
                        if (Test-Path $wingetPath) {
                            Write-Output $wingetPath
                        }
                    }
                ";

                var output = await powerShellExecutionService.ExecuteScriptAsync(script, null, cancellationToken);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    string path = output.Trim();
                    if (File.Exists(path))
                        return path;
                }

                return null;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error getting WinGet path from AppX: {ex.Message}");
                return null;
            }
        }

        private async Task<string> ResolveWinGetPathAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await ExecuteProcessAsync("winget", "--version", "Checking WinGet", cancellationToken);
                if (result.ExitCode == 0)
                {
                    logService?.LogInformation("WinGet is available in PATH");
                    return "winget";
                }
            }
            catch
            {
            }

            logService?.LogWarning("WinGet not in PATH, searching AppX location...");

            string directPath = await GetWinGetFromAppXAsync(cancellationToken);
            if (!string.IsNullOrEmpty(directPath))
            {
                var testResult = await ExecuteProcessAsync(directPath, "--version", "Testing WinGet", cancellationToken);
                if (testResult.ExitCode == 0)
                {
                    logService?.LogInformation($"Found working WinGet at: {directPath}");
                    return directPath;
                }
            }

            logService?.LogError("Could not find working WinGet executable");
            return null;
        }

        private async Task<bool> IsWinGetAppXInstalledAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string script = "Get-AppxPackage -Name Microsoft.DesktopAppInstaller | Select-Object -ExpandProperty Status";

                var output = await powerShellExecutionService.ExecuteScriptAsync(script, null, cancellationToken);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    string status = output.Trim();
                    logService?.LogInformation($"WinGet AppX status: {status}");
                    return status.Equals("Ok", StringComparison.OrdinalIgnoreCase);
                }

                return false;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error checking WinGet AppX: {ex.Message}");
                return false;
            }
        }

        private async Task InitializeWinGetSourcesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string wingetPath = _wingetExePath ?? "winget";
                logService?.LogInformation("Triggering WinGet source initialization...");

                var result = await ExecuteProcessAsync(wingetPath, "source list", "Initializing WinGet", cancellationToken);

                if (result.ExitCode != 0)
                {
                    logService?.LogWarning($"WinGet source list returned exit code {result.ExitCode}, attempting reset");
                    await ExecuteProcessAsync(wingetPath, "source reset --force", "Resetting WinGet sources", cancellationToken);
                }
                else
                {
                    logService?.LogInformation("WinGet sources initialized successfully");
                }
            }
            catch (Exception ex)
            {
                logService?.LogWarning($"Failed to initialize WinGet sources: {ex.Message}");
            }
        }

        private async Task RegisterDesktopAppInstallerAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                logService?.LogInformation("Attempting to repair WinGet AppX registration...");

                string script = "Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe";

                await powerShellExecutionService.ExecuteScriptAsync(script, null, cancellationToken);

                logService?.LogInformation("WinGet AppX registration repair command executed.");
            }
            catch (Exception ex)
            {
                logService?.LogWarning($"WinGet AppX registration repair failed: {ex.Message}");
            }
        }

        public async Task<bool> InstallPackageAsync(string packageId, string displayName = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

            displayName ??= packageId;

            taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_WinGet_CheckingPrerequisites", displayName));
            if (!await IsWinGetInstalledAsync(cancellationToken))
            {
                taskProgressService?.UpdateProgress(20, _localization.GetString("Progress_WinGet_InstallingManager"));
                if (!await InstallWinGetAsync(cancellationToken))
                {
                    taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_FailedInstallManager", displayName));
                    return false;
                }
            }
            else
            {
                logService?.LogInformation("WinGet found, ensuring it's up to date...");
                taskProgressService?.UpdateProgress(15, "Checking WinGet version...");

                var updateProgress = taskProgressService?.CreateDetailedProgress();
                var isUpdated = await EnsureWinGetUpToDateAsync(updateProgress, cancellationToken);

                if (!isUpdated)
                {
                    logService?.LogWarning("Could not ensure WinGet is up to date, proceeding anyway...");
                }
            }

            try
            {
                taskProgressService?.UpdateProgress(30, _localization.GetString("Progress_WinGet_StartingInstallation", displayName));

                string wingetPath = _wingetExePath ?? "winget";
                var args = $"install --id {EscapeArgument(packageId)} --accept-package-agreements --accept-source-agreements --disable-interactivity --silent --force";
                var result = await ExecuteProcessAsync(wingetPath, args, displayName, cancellationToken, $"Installing {displayName}");

                if (result.ExitCode == 0)
                {
                    taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_InstalledSuccess", displayName));
                    return true;
                }

                if (result.ExitCode == -1978335230)
                {
                    logService?.LogWarning("WinGet sources corrupted, reinstalling WinGet...");
                    taskProgressService?.UpdateProgress(40, _localization.GetString("Progress_WinGet_RepairingInstallation"));

                    _wingetExePath = null;

                    var progress = new Progress<TaskProgressDetail>(p => taskProgressService?.UpdateDetailedProgress(p));
                    var installResult = await WinGetInstallationScript.InstallWinGetAsync(powerShellExecutionService, progress, logService, cancellationToken);

                    if (!installResult.Success)
                    {
                        taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_FailedRepair", displayName));
                        return false;
                    }

                    await Task.Delay(2000, cancellationToken);

                    _wingetExePath = await ResolveWinGetPathAsync(cancellationToken);

                    if (string.IsNullOrEmpty(_wingetExePath))
                    {
                        taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_FailedLocateAfterRepair", displayName));
                        return false;
                    }

                    await InitializeWinGetSourcesAsync(cancellationToken);

                    taskProgressService?.UpdateProgress(60, _localization.GetString("Progress_WinGet_RetryingInstallation", displayName));
                    wingetPath = _wingetExePath;
                    result = await ExecuteProcessAsync(wingetPath, args, displayName, cancellationToken, $"Installing {displayName}");

                    if (result.ExitCode == 0)
                    {
                        taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_InstalledSuccess", displayName));
                        return true;
                    }
                }

                bool shouldRetry = result.ExitCode == unchecked((int)0x80080005) || result.ExitCode != 0;

                if (shouldRetry)
                {
                    logService?.LogWarning($"Installation failed with code {result.ExitCode}. Attempting AppX registration repair and retry...");
                    taskProgressService?.UpdateProgress(40, _localization.GetString("Progress_WinGet_RepairingInstallation"));

                    await RegisterDesktopAppInstallerAsync(cancellationToken);
                    await Task.Delay(1000, cancellationToken);

                    taskProgressService?.UpdateProgress(60, _localization.GetString("Progress_WinGet_RetryingInstallation", displayName));
                    result = await ExecuteProcessAsync(wingetPath, args, displayName, cancellationToken, $"Retry Installing {displayName}");

                    if (result.ExitCode == 0)
                    {
                        taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_InstalledSuccess", displayName));
                        return true;
                    }
                }

                var errorMessage = GetErrorContextMessage(packageId, result.ExitCode, result.Output);
                taskProgressService?.UpdateProgress(0, errorMessage);
                return false;
            }
            catch (OperationCanceledException ex)
            {
                taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_InstallationCancelled", displayName));
                throw;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error installing {packageId}: {ex.Message}");

                var errorMessage = IsNetworkRelatedError(ex.Message)
                    ? _localization.GetString("Progress_WinGet_NetworkError", displayName)
                    : _localization.GetString("Progress_WinGet_InstallationError", displayName, ex.Message);

                taskProgressService?.UpdateProgress(0, errorMessage);
                return false;
            }
        }

        public async Task<bool> UninstallPackageAsync(string packageId, string displayName = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

            displayName ??= packageId;

            taskProgressService?.UpdateProgress(10, _localization.GetString("Progress_WinGet_CheckingPrerequisitesUninstall", displayName));
            if (!await IsWinGetInstalledAsync(cancellationToken))
            {
                taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_NotInstalled"));
                return false;
            }

            try
            {
                taskProgressService?.UpdateProgress(30, _localization.GetString("Progress_WinGet_StartingUninstallation", displayName));

                string wingetPath = _wingetExePath ?? "winget";
                var args = $"uninstall --id {EscapeArgument(packageId)} --silent --force";
                var result = await ExecuteProcessAsync(wingetPath, args, displayName, cancellationToken, $"Uninstalling {displayName}");

                if (result.ExitCode == 0)
                {
                    taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_UninstalledSuccess", displayName));
                    return true;
                }

                var errorMessage = GetUninstallErrorMessage(packageId, result.ExitCode, result.Output);
                taskProgressService?.UpdateProgress(0, errorMessage);
                return false;
            }
            catch (OperationCanceledException)
            {
                taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_UninstallationCancelled", displayName));
                throw;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error uninstalling {packageId}: {ex.Message}");
                taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_UninstallationError", displayName, ex.Message));
                return false;
            }
        }

        public async Task<bool> InstallWinGetAsync(CancellationToken cancellationToken = default)
        {
            if (await IsWinGetInstalledAsync(cancellationToken))
                return true;

            var progress = new Progress<TaskProgressDetail>(p => taskProgressService?.UpdateDetailedProgress(p));

            try
            {
                taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_Installing"));
                var result = await WinGetInstallationScript.InstallWinGetAsync(powerShellExecutionService, progress, logService, cancellationToken);

                if (!result.Success)
                {
                    taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_InstallFailed"));
                    return false;
                }

                _wingetExePath = null;

                await Task.Delay(2000, cancellationToken);

                taskProgressService?.UpdateProgress(50, _localization.GetString("Progress_WinGet_VerifyingInstallation"));

                if (!await IsWinGetAppXInstalledAsync(cancellationToken))
                {
                    logService?.LogError("WinGet AppX package not found after installation");
                    taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_VerificationFailed"));
                    return false;
                }

                _wingetExePath = await ResolveWinGetPathAsync(cancellationToken);

                if (string.IsNullOrEmpty(_wingetExePath))
                {
                    taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_InstalledNotAccessible"));
                    return false;
                }

                taskProgressService?.UpdateProgress(75, _localization.GetString("Progress_WinGet_InitializingSources"));
                await InitializeWinGetSourcesAsync(cancellationToken);

                await RegisterDesktopAppInstallerAsync(cancellationToken);

                taskProgressService?.UpdateProgress(100, _localization.GetString("Progress_WinGet_InstalledSuccessfully"));
                return true;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Failed to install WinGet: {ex.Message}");
                taskProgressService?.UpdateProgress(0, _localization.GetString("Progress_WinGet_InstallError", ex.Message));
                return false;
            }
        }


        public async Task<bool> IsPackageInstalledAsync(string packageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId) || !await IsWinGetInstalledAsync(cancellationToken))
                return false;

            try
            {
                string wingetPath = _wingetExePath ?? "winget";
                var result = await ExecuteProcessAsync(wingetPath, $"list --id {packageId} --exact", packageId, cancellationToken, $"Checking if {packageId} is installed");
                return result.ExitCode == 0;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error checking if package {packageId} is installed: {ex.Message}");
                return false;
            }
        }

        private async Task<(int ExitCode, string Output)> ExecuteProcessAsync(string fileName, string arguments, string displayName, CancellationToken cancellationToken, string operationContext = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            var outputParser = new WinGetOutputParser(displayName);

            var progress = new Progress<WinGetProgress>(p =>
            {
                taskProgressService?.UpdateDetailedProgress(new TaskProgressDetail
                {
                    StatusText = p.Status,
                    TerminalOutput = p.Details,
                    IsActive = p.IsActive,
                    IsIndeterminate = true
                });
            });

            var initialStatus = GetInitialStatusMessage(arguments, displayName, operationContext);
            ((IProgress<WinGetProgress>)progress).Report(new WinGetProgress { Status = initialStatus, IsActive = true });

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    var installProgress = outputParser.ParseOutputLine(e.Data);
                    if (installProgress != null)
                    {
                        ((IProgress<WinGetProgress>)progress).Report(new WinGetProgress
                        {
                            Status = installProgress.Status,
                            Details = installProgress.LastLine,
                            IsActive = installProgress.IsActive,
                            IsCancelled = installProgress.IsCancelled
                        });

                        if (installProgress.IsConnectivityIssue)
                        {
                            logService?.LogWarning($"Network connectivity issue detected during {displayName} operation: {installProgress.LastLine}");
                        }
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();

            await Task.Run(() =>
            {
                while (!process.WaitForExit(100))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        ((IProgress<WinGetProgress>)progress).Report(new WinGetProgress { Status = "Cancelling...", IsCancelled = true });
                        try { process.Kill(); } catch { }
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }, cancellationToken);

            if (process.ExitCode == 0)
            {
                ((IProgress<WinGetProgress>)progress).Report(new WinGetProgress { Status = "Completed", IsActive = false });
            }

            return (process.ExitCode, outputBuilder.ToString());
        }

        private string GetInitialStatusMessage(string arguments, string displayName, string operationContext)
        {
            if (!string.IsNullOrEmpty(operationContext))
                return operationContext;

            if (arguments.Contains("install"))
                return $"Preparing to install {displayName}...";
            if (arguments.Contains("uninstall"))
                return $"Preparing to uninstall {displayName}...";
            if (arguments.Contains("--version"))
                return displayName ?? "Checking version...";
            if (arguments.Contains("list"))
                return $"Checking installation status of {displayName}...";

            return $"Processing {displayName ?? "operation"}...";
        }

        private string GetErrorContextMessage(string packageId, int exitCode, string output = null)
        {
            if (!string.IsNullOrEmpty(output) && IsNetworkRelatedError(output))
            {
                return $"Network error while installing {packageId}. Please check your internet connection and try again.";
            }

            return exitCode switch
            {
                -1978335189 => $"Package '{packageId}' not found in repositories. Please verify the package ID is correct.",
                -1978335135 => $"Another installation is already in progress. Please wait for it to complete before installing {packageId}.",
                -1978335148 => $"Installation cancelled by user for package '{packageId}'.",
                -1978335153 => $"Package '{packageId}' requires administrator privileges. Please run as administrator.",
                -1978335154 => $"Insufficient disk space to install '{packageId}'. Please free up space and try again.",
                -1978335092 => $"Package '{packageId}' is already installed with the same or newer version.",
                -1978335212 => $"Installation source is not available for package '{packageId}'. The package may have been removed from the repository.",
                unchecked((int)0x80070005) => $"Access denied while installing '{packageId}'. Please run as administrator.",
                unchecked((int)0x80072EE2) => $"Network timeout while downloading '{packageId}'. Please check your internet connection and try again.",
                unchecked((int)0x80072EFD) => $"Could not connect to package repository while installing '{packageId}'. Please check your internet connection.",
                _ => $"Installation failed for '{packageId}' with exit code {exitCode}. Please check the logs for more details."
            };
        }

        private string GetUninstallErrorMessage(string packageId, int exitCode, string output = null)
        {
            return exitCode switch
            {
                -1978335189 => $"Package '{packageId}' not found or not installed.",
                -1978335148 => $"Uninstallation cancelled by user for package '{packageId}'.",
                -1978335153 => $"Package '{packageId}' requires administrator privileges to uninstall. Please run as administrator.",
                unchecked((int)0x80070005) => $"Access denied while uninstalling '{packageId}'. Please run as administrator.",
                _ => $"Uninstallation failed for '{packageId}' with exit code {exitCode}. The app may require manual uninstallation."
            };
        }

        private bool IsNetworkRelatedError(string output)
        {
            if (string.IsNullOrEmpty(output))
                return false;

            var lowerOutput = output.ToLowerInvariant();
            return lowerOutput.Contains("network") ||
                   lowerOutput.Contains("timeout") ||
                   lowerOutput.Contains("connection") ||
                   lowerOutput.Contains("dns") ||
                   lowerOutput.Contains("resolve") ||
                   lowerOutput.Contains("unreachable") ||
                   lowerOutput.Contains("offline") ||
                   lowerOutput.Contains("proxy") ||
                   lowerOutput.Contains("certificate") ||
                   lowerOutput.Contains("ssl") ||
                   lowerOutput.Contains("tls") ||
                   lowerOutput.Contains("download failed") ||
                   lowerOutput.Contains("no internet") ||
                   lowerOutput.Contains("connectivity");
        }

        private string EscapeArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";

            arg = arg.Replace("\"", "\\\"");
            if (arg.Contains(" "))
                arg = $"\"{arg}\"";

            return arg;
        }

        public async Task<bool> IsWinGetInstalledAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await ExecuteProcessAsync("winget", "--version", "Checking WinGet availability", cancellationToken);
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                logService?.LogInformation("Checking WinGet availability...");
                bool isInstalled = await IsWinGetInstalledAsync(cancellationToken);

                if (!isInstalled)
                {
                    logService?.LogInformation("WinGet is not installed - will use WMI/Registry for app detection");
                }

                return isInstalled;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error checking WinGet availability: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnsureWinGetUpToDateAsync(IProgress<TaskProgressDetail> progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                logService?.LogInformation("Ensuring WinGet is up to date...");
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 10,
                    StatusText = "Checking for WinGet updates..."
                });

                string wingetPath = _wingetExePath ?? "winget";
                var arguments = "install Microsoft.AppInstaller --source winget " +
                               "--accept-source-agreements --accept-package-agreements " +
                               "--disable-interactivity --silent";

                var result = await ExecuteProcessAsync(wingetPath, arguments, "WinGet Update", cancellationToken, "Updating WinGet");

                if (result.ExitCode == 0 ||
                    result.ExitCode == -2147467260 ||
                    result.ExitCode == -1978335189 ||
                    result.ExitCode == -1978335135)
                {
                    if (result.ExitCode == -2147467260)
                    {
                        logService?.LogInformation("WinGet update initiated via Store, verifying completion...");
                        progress?.Report(new TaskProgressDetail
                        {
                            Progress = 50,
                            StatusText = "Waiting for WinGet update to complete..."
                        });

                        bool isReady = false;
                        for (int i = 0; i < 10; i++)
                        {
                            await Task.Delay(1000, cancellationToken);

                            if (await IsWinGetInstalledAsync(cancellationToken))
                            {
                                _wingetExePath = await ResolveWinGetPathAsync(cancellationToken);
                                logService?.LogInformation($"WinGet update completed and verified (waited {i + 1}s)");
                                isReady = true;
                                break;
                            }
                        }

                        if (!isReady)
                        {
                            logService?.LogWarning("WinGet update did not complete in time, attempting reinstall...");
                            return await InstallWinGetAsync(cancellationToken);
                        }
                    }

                    var status = result.ExitCode switch
                    {
                        0 => "WinGet updated successfully",
                        -2147467260 => "WinGet update completed",
                        -1978335189 => "WinGet is already up to date",
                        -1978335135 => "WinGet is already installed at latest version",
                        _ => "WinGet is ready"
                    };

                    logService?.LogInformation($"{status} (exit code: {result.ExitCode})");
                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = 100,
                        StatusText = "WinGet is ready"
                    });

                    await RegisterDesktopAppInstallerAsync(cancellationToken);

                    return true;
                }

                logService?.LogWarning($"WinGet self-update returned unexpected code: {result.ExitCode}, attempting reinstall...");
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 40,
                    StatusText = "Reinstalling WinGet..."
                });

                return await InstallWinGetAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error updating WinGet: {ex.Message}");

                try
                {
                    return await InstallWinGetAsync(cancellationToken);
                }
                catch
                {
                    return false;
                }
            }
        }

    }
}
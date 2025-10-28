using System;
using System.Diagnostics;
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
        ILogService logService = null) : IWinGetService
    {

        public async Task<bool> InstallPackageAsync(string packageId, string displayName = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

            displayName ??= packageId;

            // Phase 1: Check WinGet availability (10%)
            taskProgressService?.UpdateProgress(10, $"Checking prerequisites for {displayName}...");
            if (!await IsWinGetInstalledAsync(cancellationToken))
            {
                taskProgressService?.UpdateProgress(20, $"Installing WinGet package manager...");
                if (!await InstallWinGetAsync(cancellationToken))
                {
                    taskProgressService?.UpdateProgress(0, $"Failed to install WinGet. Cannot install {displayName}.");
                    return false;
                }
            }

            try
            {
                // Phase 2: Start installation process (30%)
                taskProgressService?.UpdateProgress(30, $"Starting installation of {displayName}...");
                var args = $"install --id {EscapeArgument(packageId)} --accept-package-agreements --accept-source-agreements --disable-interactivity --silent --force";
                var result = await ExecuteProcessAsync("winget", args, displayName, cancellationToken, $"Installing {displayName}");

                if (result.ExitCode == 0)
                {
                    taskProgressService?.UpdateProgress(100, $"Successfully installed {displayName}");
                    return true;
                }

                var errorMessage = GetErrorContextMessage(packageId, result.ExitCode, result.Output);
                taskProgressService?.UpdateProgress(0, errorMessage);
                return false;
            }
            catch (OperationCanceledException ex)
            {
                taskProgressService?.UpdateProgress(0, $"Installation of {displayName} was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error installing {packageId}: {ex.Message}");

                // Check if the exception is network-related
                var errorMessage = IsNetworkRelatedError(ex.Message)
                    ? $"Network error while installing {displayName}. Please check your internet connection and try again."
                    : $"Error installing {displayName}: {ex.Message}";

                taskProgressService?.UpdateProgress(0, errorMessage);
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
                taskProgressService?.UpdateProgress(0, "Installing WinGet...");
                var result = await WinGetInstallationScript.InstallWinGetAsync(powerShellExecutionService, progress, logService, cancellationToken);
                
                if (result.Success)
                {
                    // Wait and verify installation
                    await Task.Delay(3000, cancellationToken);
                    
                    if (await IsWinGetInstalledAsync(cancellationToken))
                    {
                        taskProgressService?.UpdateProgress(100, "WinGet installed successfully");
                        return true;
                    }
                    
                    // One retry after additional delay
                    await Task.Delay(3000, cancellationToken);
                    if (await IsWinGetInstalledAsync(cancellationToken))
                    {
                        taskProgressService?.UpdateProgress(100, "WinGet installed successfully");
                        return true;
                    }
                }
                
                taskProgressService?.UpdateProgress(0, "Failed to install WinGet");
                return false;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Failed to install WinGet: {ex.Message}");
                taskProgressService?.UpdateProgress(0, $"Error installing WinGet: {ex.Message}");
                return false;
            }
        }


        public async Task<bool> IsPackageInstalledAsync(string packageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId) || !await IsWinGetInstalledAsync(cancellationToken))
                return false;

            try
            {
                var result = await ExecuteProcessAsync("winget", $"list --id {packageId} --exact", packageId, cancellationToken, $"Checking if {packageId} is installed");
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

                        // If we detect a network error during parsing, log it for better error reporting
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

            // Determine operation type from arguments
            if (arguments.Contains("install"))
                return $"Preparing to install {displayName}...";
            if (arguments.Contains("uninstall"))
                return $"Preparing to uninstall {displayName}...";
            if (arguments.Contains("--version"))
                return displayName ?? "Checking version...";
            if (arguments.Contains("list"))
                return $"Checking installation status of {displayName}...";

            // Fallback to generic message
            return $"Processing {displayName ?? "operation"}...";
        }

        private string GetErrorContextMessage(string packageId, int exitCode, string output = null)
        {
            // Check for network-related errors first
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


    }
}
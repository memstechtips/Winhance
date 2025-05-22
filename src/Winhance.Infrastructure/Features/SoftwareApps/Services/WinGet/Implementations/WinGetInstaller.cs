using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Verification;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Extensions;
using Winhance.Infrastructure.Features.Common.Utilities;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification.Methods;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Implementations
{
    /// <summary>
    /// Implements the <see cref="IWinGetInstaller"/> interface for managing packages with WinGet.
    /// </summary>
    public class WinGetInstaller : IWinGetInstaller
    {
        private const string WinGetExe = "winget.exe";
        private readonly IPowerShellExecutionService _powerShellService;
        private readonly ITaskProgressService _taskProgressService;
        private readonly IInstallationVerifier _installationVerifier;
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="WinGetInstaller"/> class.
        /// </summary>
        /// <param name="powerShellFactory">The PowerShell factory for executing commands.</param>
        /// <param name="taskProgressService">The task progress service for reporting progress.</param>
        /// <param name="installationVerifier">The installation verifier to check if packages are installed.</param>
        /// <param name="logService">The logging service for logging messages.</param>
        public WinGetInstaller(
            IPowerShellExecutionService powerShellService,
            ITaskProgressService taskProgressService,
            IInstallationVerifier installationVerifier = null,
            ILogService logService = null
        )
        {
            _powerShellService =
                powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _taskProgressService =
                taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
            _installationVerifier =
                installationVerifier
                ?? new CompositeInstallationVerifier(GetDefaultVerificationMethods());
            _logService = logService;
        }

        /// <inheritdoc/>
        public async Task<InstallationResult> InstallPackageAsync(
            string packageId,
            InstallationOptions options = null,
            string displayName = null,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException(
                    "Package ID cannot be null or whitespace.",
                    nameof(packageId)
                );

            options ??= new InstallationOptions();
            var arguments = new StringBuilder(
                $"install --id {EscapeArgument(packageId)} --accept-package-agreements --accept-source-agreements --disable-interactivity --silent --force"
            );

            if (!string.IsNullOrWhiteSpace(options.Version))
                arguments.Append($" --version {EscapeArgument(options.Version)}");

            // Force parameter is now always included
            // Interactive is disabled with --disable-interactivity
            // Silent is now always included

            try
            {
                // Create a wrapper function that will be passed to the extension method
                Func<IProgress<InstallationProgress>, Task<InstallationResult>> operation = async (
                    progress
                ) =>
                {
                    var commandResult = await ExecuteWinGetCommandAsync(
                            WinGetExe,
                            arguments.ToString(),
                            progress,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    // Convert the command result to an InstallationResult
                    return new InstallationResult
                    {
                        Success = commandResult.ExitCode == 0,
                        Message =
                            commandResult.ExitCode == 0
                                ? $"Successfully installed {packageId}"
                                : $"Failed to install {packageId}. Exit code: {commandResult.ExitCode}. Error: {commandResult.Error}",
                        PackageId = packageId,
                        Version = options?.Version,
                    };
                };

                // Use the display name if provided, otherwise use packageId
                string nameToDisplay = displayName ?? packageId;

                // Use the extension method to track progress
                var result = await _taskProgressService
                    .TrackWinGetInstallationAsync(operation, nameToDisplay, cancellationToken)
                    .ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    return new InstallationResult
                    {
                        Success = false,
                        Message =
                            $"Failed to install {packageId}. Exit code: {result.ExitCode}. Error: {result.Error}",
                        PackageId = packageId,
                        Version = options.Version,
                    };
                }

                // For Microsoft Store apps, trust the WinGet exit code as the primary indicator of success
                bool isMicrosoftStoreApp =
                    packageId.All(char.IsLetterOrDigit) && !packageId.Contains('.');

                if (isMicrosoftStoreApp)
                {
                    _logService?.LogInformation(
                        $"Microsoft Store app detected: {packageId}. Using WinGet exit code for success determination."
                    );

                    // For Microsoft Store apps, trust the exit code
                    return new InstallationResult
                    {
                        Success = true, // WinGet command succeeded, so consider the installation successful
                        Message = $"Successfully installed {packageId} {options.Version}",
                        PackageId = packageId,
                        Version = options.Version,
                    };
                }

                // For regular apps, try verification with a longer delay
                try
                {
                    // Add a longer delay for verification to allow Windows to complete registration
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken)
                        .ConfigureAwait(false);

                    var verification = await _installationVerifier
                        .VerifyInstallationAsync(packageId, options.Version, cancellationToken)
                        .ConfigureAwait(false);

                    // If verification succeeds, great! If not, but WinGet reported success, trust WinGet
                    bool success = verification.IsVerified || result.ExitCode == 0;

                    return new InstallationResult
                    {
                        Success = success,
                        Message = success
                            ? $"Successfully installed {packageId} {options.Version}"
                            : $"Installation may have failed: {verification.Message}",
                        PackageId = packageId,
                        Version = options.Version,
                    };
                }
                catch (Exception ex)
                {
                    _logService?.LogWarning(
                        $"Verification failed with error: {ex.Message}. Using WinGet exit code for success determination."
                    );

                    // If verification throws an exception, trust the WinGet exit code
                    return new InstallationResult
                    {
                        Success = true, // WinGet command succeeded, so consider the installation successful
                        Message =
                            $"Successfully installed {packageId} {options.Version} (verification skipped due to error)",
                        PackageId = packageId,
                        Version = options.Version,
                    };
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new InstallationResult
                {
                    Success = false,
                    Message = $"Error installing {packageId}: {ex.Message}",
                    PackageId = packageId,
                    Version = options.Version,
                };
            }
        }

        /// <inheritdoc/>
        public async Task<UpgradeResult> UpgradePackageAsync(
            string packageId,
            UpgradeOptions options = null,
            string displayName = null,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException(
                    "Package ID cannot be null or whitespace.",
                    nameof(packageId)
                );

            options ??= new UpgradeOptions();
            var arguments = new StringBuilder(
                $"upgrade --id {EscapeArgument(packageId)} --accept-package-agreements --accept-source-agreements --disable-interactivity --silent --force"
            );

            // Force parameter is now always included
            // Interactive is disabled with --disable-interactivity
            // Silent is now always included

            try
            {
                // Create a wrapper function that will be passed to the extension method
                Func<IProgress<UpgradeProgress>, Task<UpgradeResult>> operation = async (
                    progress
                ) =>
                {
                    // Create an adapter since ExecuteWinGetCommandAsync expects InstallationProgress
                    var progressAdapter = new Progress<InstallationProgress>(p =>
                    {
                        // Convert InstallationProgress to UpgradeProgress
                        progress.Report(
                            new UpgradeProgress
                            {
                                Percentage = p.Percentage,
                                Status = p.Status,
                                IsIndeterminate = p.IsIndeterminate,
                            }
                        );
                    });

                    var commandResult = await ExecuteWinGetCommandAsync(
                            WinGetExe,
                            arguments.ToString(),
                            progressAdapter,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    // Convert the command result to an UpgradeResult
                    return new UpgradeResult
                    {
                        Success = commandResult.ExitCode == 0,
                        Message =
                            commandResult.ExitCode == 0
                                ? $"Successfully upgraded {packageId}"
                                : $"Failed to upgrade {packageId}. Exit code: {commandResult.ExitCode}. Error: {commandResult.Error}",
                        PackageId = packageId,
                        Version = options?.Version,
                    };
                };

                // Use the display name if provided, otherwise use packageId
                string nameToDisplay = displayName ?? packageId;

                // Use the extension method to track progress
                var result = await _taskProgressService
                    .TrackWinGetUpgradeAsync(operation, nameToDisplay, cancellationToken)
                    .ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    return new UpgradeResult
                    {
                        Success = false,
                        Message =
                            $"Failed to upgrade {packageId}. Exit code: {result.ExitCode}. Error: {result.Error}",
                        PackageId = packageId,
                        Version = options.Version,
                    };
                }

                // For Microsoft Store apps, trust the WinGet exit code as the primary indicator of success
                bool isMicrosoftStoreApp =
                    packageId.All(char.IsLetterOrDigit) && !packageId.Contains('.');

                if (isMicrosoftStoreApp)
                {
                    _logService?.LogInformation(
                        $"Microsoft Store app detected: {packageId}. Using WinGet exit code for success determination."
                    );

                    // For Microsoft Store apps, trust the exit code
                    return new UpgradeResult
                    {
                        Success = true, // WinGet command succeeded, so consider the upgrade successful
                        Message = $"Successfully upgraded {packageId} to {options.Version}",
                        PackageId = packageId,
                        Version = options.Version,
                    };
                }

                // For regular apps, try verification with a longer delay
                try
                {
                    // Add a longer delay for verification to allow Windows to complete registration
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken)
                        .ConfigureAwait(false);

                    var verificationResult = await _installationVerifier
                        .VerifyInstallationAsync(packageId, options.Version, cancellationToken)
                        .ConfigureAwait(false);

                    // If verification succeeds, great! If not, but WinGet reported success, trust WinGet
                    bool success = verificationResult.IsVerified || result.ExitCode == 0;

                    return new UpgradeResult
                    {
                        Success = success,
                        Message = success
                            ? $"Successfully upgraded {packageId} to {options.Version}"
                            : $"Upgrade may have failed: {verificationResult.Message}",
                        PackageId = packageId,
                        Version = options.Version,
                    };
                }
                catch (Exception ex)
                {
                    _logService?.LogWarning(
                        $"Verification failed with error: {ex.Message}. Using WinGet exit code for success determination."
                    );

                    // If verification throws an exception, trust the WinGet exit code
                    return new UpgradeResult
                    {
                        Success = true, // WinGet command succeeded, so consider the upgrade successful
                        Message =
                            $"Successfully upgraded {packageId} to {options.Version} (verification skipped due to error)",
                        PackageId = packageId,
                        Version = options.Version,
                    };
                }
            }
            catch (Exception ex)
            {
                return new UpgradeResult
                {
                    Success = false,
                    Message = $"Error upgrading {packageId}: {ex.Message}",
                    PackageId = packageId,
                    Version = options.Version,
                };
            }
        }

        /// <inheritdoc/>
        public async Task<UninstallationResult> UninstallPackageAsync(
            string packageId,
            UninstallationOptions options = null,
            string displayName = null,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException(
                    "Package ID cannot be null or whitespace.",
                    nameof(packageId)
                );

            options ??= new UninstallationOptions();
            var arguments = new StringBuilder(
                $"uninstall --id {EscapeArgument(packageId)} --accept-package-agreements --accept-source-agreements --disable-interactivity --silent --force"
            );

            // Force parameter is now always included
            // Interactive is disabled with --disable-interactivity
            // Silent is now always included

            try
            {
                // Create a wrapper function that will be passed to the extension method
                Func<IProgress<UninstallationProgress>, Task<UninstallationResult>> operation =
                    async (progress) =>
                    {
                        // Create an adapter since ExecuteWinGetCommandAsync expects InstallationProgress
                        var progressAdapter = new Progress<InstallationProgress>(p =>
                        {
                            // Convert InstallationProgress to UninstallationProgress
                            progress.Report(
                                new UninstallationProgress
                                {
                                    Percentage = p.Percentage,
                                    Status = p.Status,
                                    IsIndeterminate = p.IsIndeterminate,
                                }
                            );
                        });

                        var commandResult = await ExecuteWinGetCommandAsync(
                                WinGetExe,
                                arguments.ToString(),
                                progressAdapter,
                                cancellationToken
                            )
                            .ConfigureAwait(false);

                        // Convert the command result to an UninstallationResult
                        return new UninstallationResult
                        {
                            Success = commandResult.ExitCode == 0,
                            Message =
                                commandResult.ExitCode == 0
                                    ? $"Successfully uninstalled {packageId}"
                                    : $"Failed to uninstall {packageId}. Exit code: {commandResult.ExitCode}. Error: {commandResult.Error}",
                            PackageId = packageId,
                        };
                    };

                // Use the display name if provided, otherwise use packageId
                string nameToDisplay = displayName ?? packageId;

                // Use the extension method to track progress
                var result = await _taskProgressService
                    .TrackWinGetUninstallationAsync(operation, nameToDisplay, cancellationToken)
                    .ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    return new UninstallationResult
                    {
                        Success = false,
                        Message =
                            $"Failed to uninstall {packageId}. Exit code: {result.ExitCode}. Error: {result.Error}",
                        PackageId = packageId,
                    };
                }

                // Verify uninstallation (should return false if successfully uninstalled)
                var verification = await _installationVerifier
                    .VerifyInstallationAsync(packageId, null, cancellationToken)
                    .ConfigureAwait(false);

                return new UninstallationResult
                {
                    Success = !verification.IsVerified,
                    Message = !verification.IsVerified
                        ? $"Successfully uninstalled {packageId}"
                        : $"Uninstallation may have failed: {verification.Message}",
                    PackageId = packageId,
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new UninstallationResult
                {
                    Success = false,
                    Message = $"Error uninstalling {packageId}: {ex.Message}",
                    PackageId = packageId,
                };
            }
        }

        /// <inheritdoc/>
        public async Task<PackageInfo> GetPackageInfoAsync(
            string packageId,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException(
                    "Package ID cannot be null or whitespace.",
                    nameof(packageId)
                );

            try
            {
                var arguments = $"show --id {EscapeArgument(packageId)} --accept-source-agreements";
                var result = await ExecuteWinGetCommandAsync(
                        WinGetExe,
                        arguments,
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    return null;
                }

                return ParsePackageInfo(result.Output);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<PackageInfo>> SearchPackagesAsync(
            string query,
            SearchOptions options = null,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException(
                    "Search query cannot be null or whitespace.",
                    nameof(query)
                );

            options ??= new SearchOptions();
            var arguments = new StringBuilder(
                $"search {EscapeArgument(query)} --accept-source-agreements"
            );

            if (options.IncludeAvailable)
                arguments.Append(" --available");

            if (options.Count > 0)
                arguments.Append($" --max {options.Count}");

            try
            {
                var result = await ExecuteWinGetCommandAsync(
                        WinGetExe,
                        arguments.ToString(),
                        null,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    return Enumerable.Empty<PackageInfo>();
                }

                return ParsePackageList(result.Output);
            }
            catch (Exception)
            {
                return Enumerable.Empty<PackageInfo>();
            }
        }

        private IEnumerable<IVerificationMethod> GetDefaultVerificationMethods()
        {
            return new IVerificationMethod[]
            {
                new WinGetVerificationMethod(),
                new AppxPackageVerificationMethod(),
                new RegistryVerificationMethod(),
                new FileSystemVerificationMethod(),
            };
        }

        /// <summary>
        /// Checks if WinGet is available in the PATH.
        /// </summary>
        /// <returns>True if WinGet is in the PATH, false otherwise.</returns>
        private bool IsWinGetInPath()
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            return pathEnv
                .Split(Path.PathSeparator)
                .Any(p => !string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, WinGetExe)));
        }
        
        /// <summary>
        /// Tries to verify WinGet is working by running a simple command (winget -v)
        /// </summary>
        /// <returns>True if WinGet command works, false otherwise</returns>
        private bool TryVerifyWinGetCommand()
        {
            try
            {
                _logService?.LogInformation("Verifying WinGet by running 'winget -v' command");
                
                // Create a process to run WinGet version command
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = WinGetExe,
                    Arguments = "-v",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                
                using (var process = new Process { StartInfo = processStartInfo })
                {
                    try
                    {
                        if (process.Start())
                        {
                            // Wait for the process to exit with a timeout
                            if (process.WaitForExit(5000)) // 5 second timeout
                            {
                                string output = process.StandardOutput.ReadToEnd();
                                string error = process.StandardError.ReadToEnd();
                                
                                // If we got output and no error, WinGet is working
                                if (!string.IsNullOrWhiteSpace(output) && string.IsNullOrWhiteSpace(error))
                                {
                                    _logService?.LogInformation($"WinGet command verification successful, version: {output.Trim()}");
                                    return true;
                                }
                                else if (!string.IsNullOrWhiteSpace(error))
                                {
                                    _logService?.LogWarning($"WinGet command verification failed with error: {error.Trim()}");
                                }
                            }
                            else
                            {
                                // Process didn't exit within timeout
                                _logService?.LogWarning("WinGet command verification timed out");
                                try { process.Kill(); } catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogWarning($"Error verifying WinGet command: {ex.Message}");
                    }
                }
                
                // Try an alternative approach - using WindowsApps path directly
                string windowsAppsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft\\WindowsApps"
                );
                
                if (Directory.Exists(windowsAppsPath))
                {
                    processStartInfo.FileName = Path.Combine(windowsAppsPath, WinGetExe);
                    _logService?.LogInformation($"Trying alternative WinGet path: {processStartInfo.FileName}");
                    
                    if (File.Exists(processStartInfo.FileName))
                    {
                        using (var process = new Process { StartInfo = processStartInfo })
                        {
                            try
                            {
                                if (process.Start())
                                {
                                    if (process.WaitForExit(5000))
                                    {
                                        string output = process.StandardOutput.ReadToEnd();
                                        string error = process.StandardError.ReadToEnd();
                                        
                                        if (!string.IsNullOrWhiteSpace(output) && string.IsNullOrWhiteSpace(error))
                                        {
                                            _logService?.LogInformation($"Alternative WinGet path verification successful, version: {output.Trim()}");
                                            return true;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in TryVerifyWinGetCommand: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<bool> TryInstallWinGetAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logService?.LogInformation("WinGet not found. Attempting to install WinGet...");

                // Create a progress adapter for the task progress service
                var progressAdapter = new Progress<TaskProgressDetail>(progress =>
                {
                    _taskProgressService?.UpdateDetailedProgress(progress);
                });

                // Use the WinGetInstallationScript to install WinGet
                var result = await WinGetInstallationScript.InstallWinGetAsync(
                    progressAdapter,
                    _logService,
                    cancellationToken
                );

                bool success = result.Success;
                string message = result.Message;

                if (success)
                {
                    _logService?.LogInformation("WinGet was successfully installed.");
                    return true;
                }
                else
                {
                    _logService?.LogError($"Failed to install WinGet: {message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error installing WinGet: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<(string WinGetPath, bool JustInstalled)> FindWinGetPathAsync(
            CancellationToken cancellationToken = default
        )
        {
            // First try to verify WinGet by running a command
            if (TryVerifyWinGetCommand())
            {
                _logService?.LogInformation("WinGet command verified and working");
                return (WinGetExe, false);
            }

            // Check if winget is in the PATH
            if (IsWinGetInPath())
            {
                return (WinGetExe, false);
            }

            // Check common installation paths
            var possiblePaths = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\WindowsApps\winget.exe"
                ),
                @"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_*\winget.exe",
            };

            // Try to find WinGet in common locations
            string foundPath = null;
            foreach (var pathPattern in possiblePaths)
            {
                if (pathPattern.Contains("*"))
                {
                    // Handle wildcard paths
                    var directory = Path.GetDirectoryName(pathPattern);
                    var filePattern = Path.GetFileName(pathPattern);
                    
                    if (Directory.Exists(directory))
                    {
                        var matchingFiles = Directory.GetFiles(directory, filePattern, SearchOption.AllDirectories);
                        if (matchingFiles.Length > 0)
                        {
                            foundPath = matchingFiles[0];
                            _logService?.LogInformation($"Found WinGet at: {foundPath}");
                            return (foundPath, false);
                        }
                    }
                }
                else if (File.Exists(pathPattern))
                {
                    foundPath = pathPattern;
                    _logService?.LogInformation($"Found WinGet at: {foundPath}");
                    return (foundPath, false);
                }
            }

            // If WinGet is not found, try to install it
            if (await TryInstallWinGetAsync(cancellationToken))
            {
                _logService?.LogInformation("WinGet installation completed, verifying installation");
                
                // First try to verify WinGet by running a command after installation
                if (TryVerifyWinGetCommand())
                {
                    _logService?.LogInformation("WinGet command verified and working after installation");
                    return (WinGetExe, true);
                }
                
                // After installation, check if it's now in the PATH
                if (IsWinGetInPath())
                {
                    return (WinGetExe, true);
                }

                // Check the common paths again
                foreach (var pathPattern in possiblePaths)
                {
                    if (pathPattern.Contains("*"))
                    {
                        // Handle wildcard paths
                        var directory = Path.GetDirectoryName(pathPattern);
                        var filePattern = Path.GetFileName(pathPattern);
                        
                        if (Directory.Exists(directory))
                        {
                            var matchingFiles = Directory.GetFiles(directory, filePattern, SearchOption.AllDirectories);
                            if (matchingFiles.Length > 0)
                            {
                                foundPath = matchingFiles[0];
                                _logService?.LogInformation($"Found WinGet at: {foundPath}");
                                return (foundPath, true);
                            }
                        }
                    }
                    else if (File.Exists(pathPattern))
                    {
                        foundPath = pathPattern;
                        _logService?.LogInformation($"Found WinGet at: {foundPath}");
                        return (foundPath, true);
                    }
                }
            }

            // If we still can't find it, throw an exception
            throw new InvalidOperationException("WinGet not found and installation failed.");
        }

        private string FindWinGetPath()
        {
            try
            {
                // Call the async method synchronously
                var result = FindWinGetPathAsync().GetAwaiter().GetResult();
                return result.WinGetPath;
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error finding WinGet: {ex.Message}", ex);
                throw;
            }
        }

        // Installation states have been moved to WinGetOutputParser.InstallationState

        private async Task<(int ExitCode, string Output, string Error)> ExecuteWinGetCommandAsync(
            string command,
            string arguments,
            IProgress<InstallationProgress> progressAdapter = null,
            CancellationToken cancellationToken = default
        )
        {
            var commandLine = $"{command} {arguments}";
            _logService?.LogInformation($"Executing WinGet command: {commandLine}");

            try
            {
                // Find WinGet path or install it if not found
                string winGetPath;
                bool justInstalled = false;
                try
                {
                    (winGetPath, justInstalled) = await FindWinGetPathAsync(cancellationToken);
                }
                catch (InvalidOperationException ex)
                {
                    _logService?.LogError($"WinGet error: {ex.Message}");
                    progressAdapter?.Report(
                        new InstallationProgress
                        {
                            Status = $"Error: {ex.Message}",
                            Percentage = 0,
                            IsIndeterminate = false,
                        }
                    );
                    return (1, string.Empty, ex.Message);
                }
                
                // If WinGet was just installed, we might need to wait a moment for permissions to be set up
                // This is especially important on LTSC editions where there can be a delay
                if (justInstalled)
                {
                    _logService?.LogInformation("WinGet was just installed. Waiting briefly before proceeding...");
                    await Task.Delay(2000, cancellationToken); // Wait 2 seconds
                    
                    // Notify the user that WinGet was installed and we're continuing with the app installation
                    progressAdapter?.Report(
                        new InstallationProgress
                        {
                            Status = "WinGet was successfully installed. Continuing with application installation...",
                            Percentage = 30,
                            IsIndeterminate = false,
                        }
                    );
                    
                    // For LTSC editions, we need to use a different approach after installation
                    // Try to use the full path to WinGet if it's not just "winget.exe"
                    if (!string.Equals(winGetPath, WinGetExe, StringComparison.OrdinalIgnoreCase))
                    {
                        _logService?.LogInformation($"Using full path to WinGet: {winGetPath}");
                        
                        // Check if the file exists and is accessible
                        if (File.Exists(winGetPath))
                        {
                            try
                            {
                                // Test file access permissions
                                using (File.OpenRead(winGetPath)) { }
                                _logService?.LogInformation("Successfully verified file access to WinGet executable");
                            }
                            catch (Exception ex)
                            {
                                _logService?.LogWarning($"Access issue with WinGet executable: {ex.Message}");
                                _logService?.LogInformation("Falling back to using 'winget.exe' command");
                                winGetPath = WinGetExe; // Fall back to using just the command name
                            }
                        }
                    }
                }

                // Create a process to run WinGet directly
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = winGetPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                _logService?.LogInformation(
                    $"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}"
                );

                var process = new Process
                {
                    StartInfo = processStartInfo,
                    EnableRaisingEvents = true,
                };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                // Progress tracking is now handled by WinGetOutputParser

                // Create an output parser for processing WinGet output
                var outputParser = new WinGetOutputParser(_logService);
                
                // Report initial progress with more detailed status
                progressAdapter?.Report(
                    new InstallationProgress
                    {
                        Status = "Searching for package...",
                        Percentage = 5,
                        IsIndeterminate = false,
                    }
                );

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        
                        // Parse the output line and get progress update
                        var progress = outputParser.ParseOutputLine(e.Data);
                        
                        // Report progress if available
                        if (progress != null)
                        {
                            progressAdapter?.Report(progress);
                        }
                    }
                };
                
                // The parsing functionality has been moved to WinGetOutputParser

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        _logService?.LogError($"WinGet error: {e.Data}");
                    }
                };

                // Start the process
                if (!process.Start())
                {
                    _logService?.LogError("Failed to start WinGet process");
                    return (1, string.Empty, "Failed to start WinGet process");
                }

                // Begin reading output and error streams
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Create a task that completes when the process exits
                var processExitTask = Task.Run(
                    () =>
                    {
                        process.WaitForExit();
                        return process.ExitCode;
                    },
                    cancellationToken
                );

                // Wait for the process to exit or cancellation
                int exitCode;
                try
                {
                    // Create a flag to track whether cancellation was due to user action or connectivity issues
                    bool isCancellationDueToConnectivity = false;
                    
                    // Register cancellation callback to kill the process when cancellation is requested
                    using var cancelRegistration = cancellationToken.Register(() =>
                    {
                        _logService?.LogWarning("Cancellation requested, attempting to kill WinGet process");
                        try
                        {
                            // Check if cancellation is due to connectivity issues
                            // This is determined by examining the cancellation token's source
                            // The AppInstallationCoordinatorService will set this flag when cancelling due to connectivity
                            if (cancellationToken.IsCancellationRequested)
                            {
                                // We can't directly check the reason for cancellation from the token itself,
                                // but the AppInstallationCoordinatorService will handle this distinction
                                _logService?.LogWarning("WinGet process cancellation requested");
                            }
                            
                            if (!process.HasExited)
                            {
                                // Report cancellation progress immediately
                                progressAdapter?.Report(
                                    new InstallationProgress
                                    {
                                        Status = "Cancelling installation...",
                                        Percentage = 0,
                                        IsIndeterminate = true,
                                        IsCancelled = true
                                    }
                                );
                                
                                // Kill the process and all child processes in a background task
                                // to prevent UI stalling
                                Task.Run(() => 
                                {
                                    try 
                                    {
                                        KillProcessAndChildren(process.Id);
                                        _logService?.LogWarning("WinGet process and all child processes were killed due to cancellation");
                                        
                                        // Update progress after killing processes
                                        progressAdapter?.Report(
                                            new InstallationProgress
                                            {
                                                Status = "Installation was cancelled",
                                                Percentage = 0,
                                                IsIndeterminate = false,
                                                IsCancelled = true
                                            }
                                        );
                                    }
                                    catch (Exception ex)
                                    {
                                        _logService?.LogError($"Error killing processes: {ex.Message}");
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService?.LogError($"Error killing WinGet process: {ex.Message}");
                        }
                    });
                    
                    exitCode = await processExitTask;
                    _logService?.LogInformation($"WinGet process exited with code: {exitCode}");

                    // Report completion progress
                    if (exitCode == 0)
                    {
                        progressAdapter?.Report(
                            new InstallationProgress
                            {
                                Status = "Installation completed successfully",
                                Percentage = 100,
                                IsIndeterminate = false,
                            }
                        );
                    }
                    else
                    {
                        // Report failure progress
                        progressAdapter?.Report(
                            new InstallationProgress
                            {
                                Status = $"Installation failed with exit code: {exitCode}",
                                Percentage = 100,
                                IsIndeterminate = false,
                                IsError = true
                            }
                        );
                    }
                }
                catch (OperationCanceledException)
                {
                    _logService?.LogWarning("WinGet process execution was cancelled");

                    // Try to kill the process if it's still running
                    if (!process.HasExited)
                    {
                        try
                        {
                            // Report cancellation progress immediately
                            progressAdapter?.Report(
                                new InstallationProgress
                                {
                                    Status = "Cancelling installation...",
                                    Percentage = 0,
                                    IsIndeterminate = true,
                                    IsCancelled = true
                                }
                            );
                            
                            // Kill the process and all child processes in a background task
                            Task.Run(() => 
                            {
                                try 
                                {
                                    KillProcessAndChildren(process.Id);
                                    _logService?.LogWarning(
                                        "WinGet process and all child processes were killed due to cancellation"
                                    );
                                    
                                    // Update progress after killing processes
                                    progressAdapter?.Report(
                                        new InstallationProgress
                                        {
                                            Status = "Installation was cancelled",
                                            Percentage = 0,
                                            IsIndeterminate = false,
                                            IsCancelled = true
                                        }
                                    );
                                }
                                catch (Exception ex)
                                {
                                    _logService?.LogError($"Error killing processes: {ex.Message}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            _logService?.LogError($"Error killing WinGet process: {ex.Message}");
                        }
                    }
                    
                    // Report cancellation progress
                    progressAdapter?.Report(
                        new InstallationProgress
                        {
                            Status = "Installation was cancelled",
                            Percentage = 0,
                            IsIndeterminate = false,
                            IsCancelled = true
                        }
                    );

                    throw;
                }

                string output = outputBuilder.ToString();
                string error = errorBuilder.ToString();

                return (exitCode, output, error);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logService?.LogError($"Error executing WinGet command: {ex.Message}", ex);

                // Report error progress
                progressAdapter?.Report(
                    new InstallationProgress
                    {
                        Status = $"Error: {ex.Message}",
                        Percentage = 0,
                        IsIndeterminate = false,
                    }
                );

                return (1, string.Empty, ex.Message);
            }
        }

        // These methods have been moved to WinGetOutputParser class:
        // - DetermineInstallationState
        // - CalculateProgressPercentage
        // - GetStatusMessage

        // Original FindWinGetPath method removed to avoid duplication

        /// <summary>
        /// Kills a process and all of its children recursively with optimizations to prevent UI stalling.
        /// </summary>
        /// <param name="pid">The process ID to kill.</param>
        private void KillProcessAndChildren(int pid)
        {
            try
            {
                // First, directly kill any Windows Store processes that might be related to the installation
                // This is a more direct approach to ensure the installation is cancelled
                KillWindowsStoreProcesses();
                
                // Get the process by ID
                Process process = null;
                try
                {
                    process = Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    _logService?.LogWarning($"Process with ID {pid} not found");
                    return;
                }
                
                // Use a more efficient approach to kill the process and its children
                // with a timeout to prevent hanging
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                {
                    try
                    {
                        // Kill the process directly first
                        if (!process.HasExited)
                        {
                            try
                            {
                                process.Kill(true); // true = kill entire process tree (Windows 10 1809+)
                                _logService?.LogInformation($"Killed process tree {process.ProcessName} (ID: {pid})");
                                return; // If this works, we're done
                            }
                            catch (PlatformNotSupportedException)
                            {
                                // Process.Kill(true) is not supported on this platform, continue with fallback
                                _logService?.LogInformation("Process.Kill(true) not supported, using fallback method");
                            }
                            catch (Exception ex)
                            {
                                _logService?.LogWarning($"Error killing process tree: {ex.Message}, using fallback method");
                            }
                        }
                        
                        // Fallback: Kill the process and its children manually
                        KillProcessTree(pid, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logService?.LogWarning("Process killing operation timed out");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in KillProcessAndChildren for PID {pid}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Kills the Windows Store processes that might be related to the installation.
        /// </summary>
        private void KillWindowsStoreProcesses()
        {
            try
            {
                // Target specific processes known to be related to Windows Store installations
                string[] targetProcessNames = new[] { 
                    "WinStore.App", 
                    "WinStore.Mobile", 
                    "WindowsPackageManagerServer", 
                    "AppInstaller",
                    "Microsoft.WindowsStore"
                };
                
                foreach (var processName in targetProcessNames)
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(processName);
                        foreach (var process in processes)
                        {
                            try
                            {
                                if (!process.HasExited)
                                {
                                    process.Kill();
                                    _logService?.LogInformation($"Killed Windows Store process: {processName}");
                                }
                            }
                            finally
                            {
                                process.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogWarning($"Error killing Windows Store process {processName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error killing Windows Store processes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Kills a process tree efficiently with cancellation support.
        /// </summary>
        /// <param name="pid">The process ID to kill.</param>
        /// <param name="cancellationToken">Cancellation token to prevent hanging.</param>
        private void KillProcessTree(int pid, CancellationToken cancellationToken)
        {
            try
            {
                // Get direct child processes using a more efficient method
                var childProcessIds = GetChildProcessIds(pid);
                
                // Kill child processes first
                foreach (var childPid in childProcessIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        // Recursively kill child process trees
                        KillProcessTree(childPid, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogWarning($"Error killing child process {childPid}: {ex.Message}");
                    }
                }
                
                // Kill the parent process
                try
                {
                    var process = Process.GetProcessById(pid);
                    if (!process.HasExited)
                    {
                        process.Kill();
                        _logService?.LogInformation($"Killed process {process.ProcessName} (ID: {pid})");
                    }
                    process.Dispose();
                }
                catch (ArgumentException)
                {
                    // Process already exited
                }
                catch (Exception ex)
                {
                    _logService?.LogWarning($"Error killing process {pid}: {ex.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Rethrow to be handled by the caller
            }
            catch (Exception ex)
            {
                _logService?.LogError($"Error in KillProcessTree for PID {pid}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the child process IDs for a given process ID efficiently.
        /// </summary>
        /// <param name="parentId">The parent process ID.</param>
        /// <returns>A list of child process IDs.</returns>
        private List<int> GetChildProcessIds(int parentId)
        {
            var result = new List<int>();
            
            try
            {
                // Use a more efficient query that only gets the data we need
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentId}"))
                {
                    searcher.Options.Timeout = TimeSpan.FromSeconds(1); // Set a timeout to prevent hanging
                    
                    foreach (var obj in searcher.Get())
                    {
                        try
                        {
                            result.Add(Convert.ToInt32(obj["ProcessId"]));
                        }
                        catch
                        {
                            // Skip this entry if conversion fails
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService?.LogWarning($"Error getting child process IDs: {ex.Message}");
            }
            
            return result;
        }
        
        private string EscapeArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";

            // Escape any double quotes
            arg = arg.Replace("\"", "\\\"");

            // Surround with quotes if it contains spaces
            if (arg.Contains(" "))
                arg = $"\"{arg}\"";

            return arg;
        }

        private PackageInfo ParsePackageInfo(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return null;

            var info = new PackageInfo();
            var lines = output.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries
            );

            foreach (var line in lines)
            {
                if (!line.Contains(":"))
                    continue;

                var parts = line.Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim().ToLowerInvariant();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "id":
                        info.Id = value;
                        break;
                    case "name":
                        info.Name = value;
                        break;
                    case "version":
                        info.Version = value;
                        break;
                    // Description property doesn't exist in PackageInfo
                    case "description":
                        // info.Description = value;
                        break;
                }
            }

            return info;
        }

        private IEnumerable<PackageInfo> ParsePackageList(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                yield break;

            var lines = output.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries
            );

            // Skip header lines
            bool headerPassed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Skip until we find a line with dashes (header separator)
                if (!headerPassed)
                {
                    if (line.Contains("---"))
                    {
                        headerPassed = true;
                    }
                    continue;
                }

                // Parse package info from the line
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    continue;

                yield return new PackageInfo
                {
                    Name = parts[0],
                    Id = parts[1],
                    Version = parts[2],
                    Source = parts.Length > 3 ? parts[3] : string.Empty,
                    IsInstalled = line.Contains("[Installed]"),
                };
            }
        }
    }
}

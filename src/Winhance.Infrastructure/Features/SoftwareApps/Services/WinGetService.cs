using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        ILogService logService = null) : IWinGetService
    {
        private static string _cachedWinGetPath;
        private const string WinGetExe = "winget.exe";

        public async Task<bool> InstallPackageAsync(string packageId, string displayName = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));

            displayName ??= packageId;

            if (!await EnsureWinGetInstalledAsync(cancellationToken))
            {
                taskProgressService?.UpdateProgress(0, $"Failed to install WinGet. Cannot install {displayName}.");
                return false;
            }

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

            try
            {
                var result = await InstallPackageInternalAsync(packageId, displayName, progress, cancellationToken);

                if (result.Success)
                {
                    await Task.Delay(2000, cancellationToken);
                    var isInstalled = await IsPackageInstalledAsync(packageId, cancellationToken);
                    if (isInstalled)
                    {
                        taskProgressService?.UpdateProgress(100, $"Successfully installed {displayName}");
                        return true;
                    }

                    taskProgressService?.UpdateProgress(100, $"Installation reported success but verification failed for {displayName}");
                    return true; // Trust WinGet's exit code
                }

                taskProgressService?.UpdateProgress(0, $"Failed to install {displayName}: {result.Message}");
                return false;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error installing {packageId}: {ex.Message}");
                taskProgressService?.UpdateProgress(0, $"Error installing {displayName}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnsureWinGetInstalledAsync(CancellationToken cancellationToken = default)
        {
            if (IsWinGetInstalled())
                return true;

            var progress = new Progress<TaskProgressDetail>(p =>
            {
                taskProgressService?.UpdateDetailedProgress(p);
            });

            try
            {
                taskProgressService?.UpdateProgress(0, "WinGet not found. Installing WinGet...");
                var result = await InstallWinGetIfNeededAsync(progress, cancellationToken);

                if (result)
                {
                    taskProgressService?.UpdateProgress(100, "WinGet installed successfully");
                    return true;
                }

                taskProgressService?.UpdateProgress(0, "Failed to install WinGet");
                return false;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Failed to ensure WinGet is installed: {ex.Message}");
                taskProgressService?.UpdateProgress(0, $"Error installing WinGet: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsWinGetInstalledAsync()
        {
            return await Task.FromResult(IsWinGetInstalled());
        }

        public async Task<bool> IsPackageInstalledAsync(string packageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return false;

            if (!IsWinGetInstalled())
                return false;

            try
            {
                var result = await ExecuteCommandAsync("list", $"--id {packageId} --exact", cancellationToken);
                return result.Success;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error checking if package {packageId} is installed: {ex.Message}");
                return false;
            }
        }

        private async Task<WinGetOperationResult> InstallPackageInternalAsync(string packageId, string displayName, IProgress<WinGetProgress> progress, CancellationToken cancellationToken)
        {
            var wingetPath = FindWinGetPath();
            if (string.IsNullOrEmpty(wingetPath))
            {
                return new WinGetOperationResult
                {
                    Success = false,
                    Message = "WinGet not found",
                    PackageId = packageId
                };
            }

            var args = $"install --id {EscapeArgument(packageId)} --accept-package-agreements --accept-source-agreements --disable-interactivity --silent --force";

            try
            {
                var result = await ExecuteProcessWithProgressAsync(wingetPath, args, displayName, progress, cancellationToken);

                return new WinGetOperationResult
                {
                    Success = result.ExitCode == 0,
                    Message = result.ExitCode == 0 ? $"Successfully installed {displayName ?? packageId}" : $"Installation failed with exit code {result.ExitCode}",
                    PackageId = packageId
                };
            }
            catch (Exception ex)
            {
                logService?.LogError($"Package installation failed: {ex.Message}");
                return new WinGetOperationResult
                {
                    Success = false,
                    Message = ex.Message,
                    PackageId = packageId
                };
            }
        }

        private async Task<WinGetOperationResult> ExecuteCommandAsync(string command, string args, CancellationToken cancellationToken)
        {
            var wingetPath = FindWinGetPath();
            if (string.IsNullOrEmpty(wingetPath))
            {
                return new WinGetOperationResult
                {
                    Success = false,
                    Message = "WinGet not found"
                };
            }

            try
            {
                var result = await ExecuteProcessAsync(wingetPath, $"{command} {args}", cancellationToken);

                return new WinGetOperationResult
                {
                    Success = result.ExitCode == 0,
                    Message = result.ExitCode == 0 ? $"Successfully executed {command}" : $"Command failed with exit code {result.ExitCode}"
                };
            }
            catch (Exception ex)
            {
                logService?.LogError($"WinGet command execution failed: {ex.Message}");
                return new WinGetOperationResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        private async Task<(int ExitCode, string Output)> ExecuteProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
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

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();

            await Task.Run(() =>
            {
                while (!process.WaitForExit(100))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try { process.Kill(); } catch { }
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }, cancellationToken);

            return (process.ExitCode, outputBuilder.ToString());
        }

        private async Task<(int ExitCode, string Output)> ExecuteProcessWithProgressAsync(string fileName, string arguments, string displayName, IProgress<WinGetProgress> progress, CancellationToken cancellationToken)
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

            progress?.Report(new WinGetProgress { Status = $"Installing {displayName}...", IsActive = true });

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    var installProgress = outputParser.ParseOutputLine(e.Data);
                    if (installProgress != null)
                    {
                        progress?.Report(new WinGetProgress
                        {
                            Status = installProgress.Status,
                            Details = installProgress.LastLine,
                            IsActive = installProgress.IsActive,
                            IsCancelled = installProgress.IsCancelled
                        });
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
                        progress?.Report(new WinGetProgress { Status = "Cancelling...", IsCancelled = true });
                        try { process.Kill(); } catch { }
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }, cancellationToken);

            if (process.ExitCode == 0)
            {
                progress?.Report(new WinGetProgress { Status = "Installation completed", IsActive = false });
            }

            return (process.ExitCode, outputBuilder.ToString());
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

        private string FindWinGetPath()
        {
            if (_cachedWinGetPath != null)
                return _cachedWinGetPath;

            if (IsWinGetInPath())
            {
                _cachedWinGetPath = WinGetExe;
                return _cachedWinGetPath;
            }

            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe"),
                @"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_*\winget.exe"
            };

            foreach (var pathPattern in possiblePaths)
            {
                if (pathPattern.Contains("*"))
                {
                    var directory = Path.GetDirectoryName(pathPattern);
                    if (Directory.Exists(directory))
                    {
                        var matchingFiles = Directory.GetFiles(directory, Path.GetFileName(pathPattern), SearchOption.AllDirectories);
                        if (matchingFiles.Length > 0)
                        {
                            _cachedWinGetPath = matchingFiles[0];
                            return _cachedWinGetPath;
                        }
                    }
                }
                else if (File.Exists(pathPattern))
                {
                    _cachedWinGetPath = pathPattern;
                    return _cachedWinGetPath;
                }
            }

            return null;
        }

        private async Task<bool> InstallWinGetIfNeededAsync(IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken = default)
        {
            if (IsWinGetInstalled())
                return true;

            try
            {
                var result = await WinGetInstallationScript.InstallWinGetAsync(progress, logService, cancellationToken);
                if (result.Success)
                {
                    _cachedWinGetPath = null; // Clear cache to force re-detection
                    return IsWinGetInstalled();
                }
                return false;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Failed to install WinGet: {ex.Message}");
                return false;
            }
        }

        private bool IsWinGetInstalled()
        {
            return FindWinGetPath() != null;
        }

        private bool IsWinGetInPath()
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            return pathEnv.Split(Path.PathSeparator)
                .Any(p => !string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, WinGetExe)));
        }
    }
}
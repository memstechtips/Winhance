using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services
{
    public class WimUtilService : IWimUtilService
    {
        private readonly ILogService _logService;
        private readonly HttpClient _httpClient;
        private readonly IWinGetService _winGetService;
        private readonly ILocalizationService _localization;
        private readonly IProcessExecutor _processExecutor;

        private static readonly string[] AdkDownloadSources = new[]
        {
            "https://go.microsoft.com/fwlink/?linkid=2289980",
            "https://download.microsoft.com/download/2/d/9/2d9c8902-3fcd-48a6-a22a-432b08bed61e/ADK/adksetup.exe"
        };

        private const string UnattendedWinstallXmlUrl = "https://raw.githubusercontent.com/memstechtips/UnattendedWinstall/main/autounattend.xml";

        public WimUtilService(
            ILogService logService,
            HttpClient httpClient,
            IWinGetService winGetService,
            ILocalizationService localization,
            IProcessExecutor processExecutor)
        {
            _logService = logService;
            _httpClient = httpClient;
            _winGetService = winGetService;
            _localization = localization;
            _processExecutor = processExecutor;
        }

        public string GetOscdimgPath()
        {
            var adkPaths = new[]
            {
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\11\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\x86\Oscdimg\oscdimg.exe",
            };

            foreach (var adkPath in adkPaths)
            {
                if (File.Exists(adkPath))
                {
                    return adkPath;
                }
            }

            return string.Empty;
        }

        public async Task<ImageFormatInfo?> DetectImageFormatAsync(string workingDirectory)
        {
            try
            {
                var sourcesPath = Path.Combine(workingDirectory, "sources");
                if (!Directory.Exists(sourcesPath))
                {
                    _logService.LogWarning($"Sources directory not found: {sourcesPath}");
                    return null;
                }

                var wimPath = Path.Combine(sourcesPath, "install.wim");
                if (File.Exists(wimPath))
                {
                    return await GetImageInfoAsync(wimPath, ImageFormat.Wim).ConfigureAwait(false);
                }

                var esdPath = Path.Combine(sourcesPath, "install.esd");
                if (File.Exists(esdPath))
                {
                    return await GetImageInfoAsync(esdPath, ImageFormat.Esd).ConfigureAwait(false);
                }

                _logService.LogWarning("No install.wim or install.esd found");
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error detecting image format: {ex.Message}", ex);
                return null;
            }
        }

        public async Task<ImageDetectionResult> DetectAllImageFormatsAsync(string workingDirectory)
        {
            var result = new ImageDetectionResult();

            try
            {
                var sourcesPath = Path.Combine(workingDirectory, "sources");
                if (!Directory.Exists(sourcesPath))
                {
                    _logService.LogWarning($"Sources directory not found: {sourcesPath}");
                    return result;
                }

                var wimPath = Path.Combine(sourcesPath, "install.wim");
                if (File.Exists(wimPath))
                {
                    result.WimInfo = await GetImageInfoAsync(wimPath, ImageFormat.Wim).ConfigureAwait(false);
                }

                var esdPath = Path.Combine(sourcesPath, "install.esd");
                if (File.Exists(esdPath))
                {
                    result.EsdInfo = await GetImageInfoAsync(esdPath, ImageFormat.Esd).ConfigureAwait(false);
                }

                if (result.BothExist)
                {
                    _logService.LogWarning("Both install.wim and install.esd found - only one should exist");
                }
                else if (result.NeitherExists)
                {
                    _logService.LogWarning("No install.wim or install.esd found");
                }
                else
                {
                    var format = result.WimInfo != null ? "WIM" : "ESD";
                    _logService.LogInformation($"Found {format} format");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error detecting image formats: {ex.Message}", ex);
            }

            return result;
        }

        public async Task<bool> DeleteImageFileAsync(
            string workingDirectory,
            ImageFormat format,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var sourcesPath = Path.Combine(workingDirectory, "sources");
                var fileName = format == ImageFormat.Wim ? "install.wim" : "install.esd";
                var filePath = Path.Combine(sourcesPath, fileName);

                if (!File.Exists(filePath))
                {
                    _logService.LogWarning($"File not found for deletion: {filePath}");
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                var fileSizeGB = fileInfo.Length / (1024.0 * 1024 * 1024);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_DeletingImageFile"),
                    TerminalOutput = $"Deleting {fileName} ({fileSizeGB:F2} GB)..."
                });

                _logService.LogInformation($"Deleting {fileName} from {sourcesPath}");

                var deleted = false;
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    try
                    {
                        var file = new FileInfo(filePath);
                        if (file.Exists)
                        {
                            file.Attributes = FileAttributes.Normal;
                            file.Delete();
                            _logService.LogInformation($"Successfully deleted {fileName}");
                            deleted = true;

                            progress?.Report(new TaskProgressDetail
                            {
                                StatusText = _localization.GetString("Progress_ImageFileDeleted"),
                                TerminalOutput = $"{fileName} deleted successfully"
                            });

                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Attempt {attempt}/5 to delete {fileName} failed: {ex.Message}");
                        if (attempt < 5)
                        {
                            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                if (!deleted)
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_ImageFileDeletionFailed"),
                        TerminalOutput = $"Could not delete {fileName} after 5 attempts. File may be in use."
                    });
                }

                return deleted;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error deleting image file: {ex.Message}", ex);
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_ImageFileDeletionFailed"),
                    TerminalOutput = ex.Message
                });
                return false;
            }
        }

        private async Task<ImageFormatInfo> GetImageInfoAsync(string imagePath, ImageFormat format)
        {
            var fileInfo = new FileInfo(imagePath);
            var info = new ImageFormatInfo
            {
                Format = format,
                FilePath = imagePath,
                FileSizeBytes = fileInfo.Length
            };

            try
            {
                var arguments = $"/Get-ImageInfo /ImageFile:\"{imagePath}\"";
                _logService.LogInformation($"Running: dism.exe {arguments}");

                var result = await _processExecutor.ExecuteAsync("dism.exe", arguments).ConfigureAwait(false);
                var stdout = result.StandardOutput;

                if (!result.Succeeded)
                {
                    _logService.LogWarning($"dism.exe /Get-ImageInfo exited with code {result.ExitCode}");
                    info.ImageCount = 1;
                    return info;
                }

                int imageCount = 0;
                var editionNames = new List<string>();
                foreach (var line in stdout.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Index :", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Index:", StringComparison.OrdinalIgnoreCase))
                    {
                        imageCount++;
                    }
                    else if (trimmed.StartsWith("Name :", StringComparison.OrdinalIgnoreCase) ||
                             trimmed.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                    {
                        var name = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
                        if (!string.IsNullOrEmpty(name))
                            editionNames.Add(name);
                    }
                }

                info.EditionNames = editionNames;
                info.ImageCount = imageCount > 0 ? imageCount : 1;
                _logService.LogInformation($"Image: {format}, {info.ImageCount} editions, {info.FileSizeBytes:N0} bytes");
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not get detailed image info: {ex.Message}");
                info.ImageCount = 1;
            }

            return info;
        }

        private static readonly System.Text.RegularExpressions.Regex ProgressRegex =
            new(@"(\d+\.?\d*)\s*%", System.Text.RegularExpressions.RegexOptions.Compiled);

        private async Task<(int ExitCode, string Output)> RunProcessWithProgressAsync(
            string fileName,
            string arguments,
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            var output = new StringBuilder();

            var result = await _processExecutor.ExecuteWithStreamingAsync(
                fileName,
                arguments,
                onOutputLine: line =>
                {
                    output.AppendLine(line);
                    var match = ProgressRegex.Match(line);
                    if (match.Success && double.TryParse(match.Groups[1].Value, out var pct))
                    {
                        progress?.Report(new TaskProgressDetail
                        {
                            TerminalOutput = line,
                            Progress = pct
                        });
                    }
                    else
                    {
                        progress?.Report(new TaskProgressDetail { TerminalOutput = line });
                    }
                },
                onErrorLine: line =>
                {
                    output.AppendLine(line);
                    progress?.Report(new TaskProgressDetail { TerminalOutput = line });
                },
                ct: cancellationToken).ConfigureAwait(false);

            return (result.ExitCode, output.ToString());
        }

        private void KillDismProcesses()
        {
            try
            {
                var dismProcesses = Process.GetProcessesByName("dism");
                foreach (var process in dismProcesses)
                {
                    try
                    {
                        _logService.LogInformation($"Killing DISM process (PID: {process.Id})");
                        process.Kill();
                        process.WaitForExit(5000);
                        _logService.LogInformation($"DISM process (PID: {process.Id}) terminated");
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Failed to kill DISM process (PID: {process.Id}): {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error killing DISM processes: {ex.Message}", ex);
            }
        }

        public async Task<bool> ConvertImageAsync(
            string workingDirectory,
            ImageFormat targetFormat,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string targetFile = string.Empty;

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                _logService.LogInformation("Cancellation requested - killing DISM processes");
                KillDismProcesses();
            });

            try
            {
                var currentInfo = await DetectImageFormatAsync(workingDirectory).ConfigureAwait(false);
                if (currentInfo == null)
                {
                    _logService.LogError("Could not detect current image format");
                    return false;
                }

                if (currentInfo.Format == targetFormat)
                {
                    _logService.LogInformation($"Image is already in {targetFormat} format");
                    return true;
                }

                var sourcesPath = Path.Combine(workingDirectory, "sources");
                var sourceFile = currentInfo.FilePath;
                targetFile = targetFormat == ImageFormat.Wim
                    ? Path.Combine(sourcesPath, "install.wim")
                    : Path.Combine(sourcesPath, "install.esd");

                var requiredSpace = currentInfo.FileSizeBytes * 2;
                await CheckDiskSpace(workingDirectory, requiredSpace, "Image conversion").ConfigureAwait(false);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_ConvertingFormat", currentInfo.Format.ToString(), targetFormat.ToString()),
                    TerminalOutput = "This may take 10-20 minutes"
                });

                _logService.LogInformation($"Starting conversion: {currentInfo.Format} → {targetFormat}");

                var compressionType = targetFormat == ImageFormat.Esd ? "recovery" : "max";

                var imageCount = currentInfo.ImageCount > 0 ? currentInfo.ImageCount : 1;
                _logService.LogInformation($"Converting {imageCount} image(s)");

                for (int i = 1; i <= imageCount; i++)
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_ConvertingEdition", i.ToString(), imageCount.ToString()),
                        TerminalOutput = currentInfo.EditionNames.Count >= i
                            ? currentInfo.EditionNames[i - 1]
                            : $"Index {i}"
                    });

                    var arguments = $"/Export-Image /SourceImageFile:\"{sourceFile}\" /SourceIndex:{i} /DestinationImageFile:\"{targetFile}\" /Compress:{compressionType} /CheckIntegrity";

                    _logService.LogInformation($"Exporting index {i}: dism.exe {arguments}");

                    var (exitCode, _) = await RunProcessWithProgressAsync("dism.exe", arguments, progress, cancellationToken).ConfigureAwait(false);
                    if (exitCode != 0)
                    {
                        throw new Exception($"DISM failed with exit code: {exitCode}");
                    }
                }

                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

                if (!File.Exists(targetFile))
                {
                    _logService.LogError($"Target file not found: {targetFile}");
                    return false;
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_RemovingOldFile"),
                    TerminalOutput = $"Deleting {Path.GetFileName(sourceFile)}"
                });

                var deleted = false;
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    try
                    {
                        if (File.Exists(sourceFile))
                        {
                            var file = new FileInfo(sourceFile);
                            file.Attributes = FileAttributes.Normal;
                            file.Delete();
                            _logService.LogInformation($"Deleted source file: {sourceFile}");
                            deleted = true;
                            break;
                        }
                        else
                        {
                            deleted = true;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Attempt {attempt}/5 to delete source file failed: {ex.Message}");
                        if (attempt < 5)
                        {
                            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                var targetFileInfo = new FileInfo(targetFile);

                if (!deleted && File.Exists(sourceFile))
                {
                    _logService.LogWarning($"Could not delete source file after multiple attempts: {sourceFile}");

                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_ConversionCompleted"),
                        TerminalOutput = $"Conversion succeeded! New size: {targetFileInfo.Length / (1024.0 * 1024 * 1024):F2} GB\n\n" +
                                       $"However, the source file is still in use and could not be deleted automatically.\n\n" +
                                       $"Please manually delete:\n{sourceFile}"
                    });

                    return true;
                }

                var sizeDiff = currentInfo.FileSizeBytes - targetFileInfo.Length;
                var savedSpace = sizeDiff > 0
                    ? $"Saved {sizeDiff / (1024.0 * 1024 * 1024):F2} GB"
                    : $"Used {Math.Abs(sizeDiff) / (1024.0 * 1024 * 1024):F2} GB more";

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_ConversionCompleted"),
                    TerminalOutput = $"New size: {targetFileInfo.Length / (1024.0 * 1024 * 1024):F2} GB\n{savedSpace}"
                });

                _logService.LogInformation($"Conversion successful: {currentInfo.Format} → {targetFormat}");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logService.LogInformation("Image conversion was cancelled");

                if (File.Exists(targetFile))
                {
                    try
                    {
                        _logService.LogInformation($"Cleaning up incomplete target file: {targetFile}");
                        File.Delete(targetFile);
                        _logService.LogInformation("Incomplete target file deleted successfully");
                    }
                    catch (Exception cleanupEx)
                    {
                        _logService.LogWarning($"Could not delete incomplete target file: {cleanupEx.Message}");
                    }
                }

                throw;
            }
            catch (InsufficientDiskSpaceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error converting image: {ex.Message}", ex);

                if (File.Exists(targetFile))
                {
                    try
                    {
                        _logService.LogInformation($"Cleaning up incomplete target file: {targetFile}");
                        File.Delete(targetFile);
                        _logService.LogInformation("Incomplete target file deleted successfully");
                    }
                    catch (Exception cleanupEx)
                    {
                        _logService.LogWarning($"Could not delete incomplete target file: {cleanupEx.Message}");
                    }
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_ConversionFailed"),
                    TerminalOutput = ex.Message
                });
                return false;
            }
        }

        public Task<bool> IsOscdimgAvailableAsync()
        {
            var oscdimgPath = GetOscdimgPath();
            if (string.IsNullOrEmpty(oscdimgPath))
            {
                _logService.LogInformation("oscdimg.exe not found in Windows Kits directories");
                return Task.FromResult(false);
            }

            _logService.LogInformation($"oscdimg.exe found at: {oscdimgPath}");
            return Task.FromResult(true);
        }

        public async Task<bool> EnsureOscdimgAvailableAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (await IsOscdimgAvailableAsync().ConfigureAwait(false))
            {
                _logService.LogInformation("oscdimg.exe already available");
                return true;
            }

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_PreparingInstallAdk"),
                TerminalOutput = "Checking installation methods"
            });

            if (await InstallAdkDeploymentToolsAsync(progress, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            _logService.LogWarning("Standard ADK installation failed, trying winget...");
            if (await InstallAdkViaWingetAsync(progress, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            _logService.LogError("All methods to install oscdimg.exe failed");
            return false;
        }

        private async Task<string?> DownloadAdkSetupAsync(
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            var tempPath = Path.GetTempPath();
            var adkSetupPath = Path.Combine(tempPath, "adksetup.exe");

            foreach (var sourceUrl in AdkDownloadSources)
            {
                try
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_DownloadingAdkInstaller"),
                        TerminalOutput = $"Source: {sourceUrl}"
                    });

                    _httpClient.Timeout = TimeSpan.FromMinutes(30);
                    var response = await _httpClient.GetAsync(sourceUrl, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var setupBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    await File.WriteAllBytesAsync(adkSetupPath, setupBytes, cancellationToken).ConfigureAwait(false);

                    _logService.LogInformation($"ADK installer downloaded successfully from: {sourceUrl}");
                    return adkSetupPath;
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Failed to download from {sourceUrl}: {ex.Message}");
                }
            }

            return null;
        }

        private async Task<bool> InstallAdkDeploymentToolsAsync(
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                var adkSetupPath = await DownloadAdkSetupAsync(progress, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(adkSetupPath))
                {
                    _logService.LogError("Failed to download ADK installer from all sources");
                    return false;
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_InstallingAdkTools"),
                    TerminalOutput = "This may take several minutes"
                });

                var logPath = Path.Combine(Path.GetTempPath(), "adk_install.log");
                var arguments = $"/quiet /norestart /features OptionId.DeploymentTools /ceip off /log \"{logPath}\"";

                progress?.Report(new TaskProgressDetail
                {
                    TerminalOutput = "Starting ADK Deployment Tools installation..."
                });

                var (exitCode, _) = await RunProcessWithProgressAsync(adkSetupPath, arguments, progress, cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new Exception($"ADK installation failed with exit code: {exitCode}");
                }

                if (await IsOscdimgAvailableAsync().ConfigureAwait(false))
                {
                    _logService.LogInformation("ADK installed and oscdimg.exe found");
                    return true;
                }

                _logService.LogError("ADK installed but oscdimg.exe not found");
                return false;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error installing ADK: {ex.Message}", ex);
                return false;
            }
            finally
            {
                try
                {
                    var adkSetupPath = Path.Combine(Path.GetTempPath(), "adksetup.exe");
                    if (File.Exists(adkSetupPath))
                    {
                        File.Delete(adkSetupPath);
                    }
                }
                catch (Exception ex) { _logService.LogDebug($"Best-effort ADK setup file cleanup failed: {ex.Message}"); }
            }
        }

        private async Task<bool> InstallAdkViaWingetAsync(
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CheckingWinget"),
                    TerminalOutput = "Verifying winget availability"
                });

                var wingetInstalled = await _winGetService.IsWinGetInstalledAsync(cancellationToken).ConfigureAwait(false);

                if (!wingetInstalled)
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_InstallingWinget"),
                        TerminalOutput = "winget is required for this installation method"
                    });

                    var wingetInstallSuccess = await _winGetService.InstallWinGetAsync(cancellationToken).ConfigureAwait(false);
                    if (!wingetInstallSuccess)
                    {
                        _logService.LogError("Failed to install winget");
                        return false;
                    }
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_InstallingAdkViaWinget"),
                    TerminalOutput = "This may take several minutes"
                });

                var logPath = Path.Combine(Path.GetTempPath(), "adk_winget_install.log");
                var arguments = $"install Microsoft.WindowsADK --exact --silent --accept-package-agreements --accept-source-agreements --override \"/quiet /norestart /features OptionId.DeploymentTools /ceip off\" --log \"{logPath}\"";

                progress?.Report(new TaskProgressDetail
                {
                    TerminalOutput = "Starting ADK installation via winget..."
                });

                // Prefer system winget (kept up-to-date via Store) over bundled CLI
                var systemAvailable = _winGetService.IsSystemWinGetAvailable
                    || WinGetCliRunner.IsSystemWinGetAvailable();

                string wingetExe;
                if (systemAvailable)
                {
                    // GetWinGetExePath checks system PATH → WindowsApps → bundled, in order
                    wingetExe = WinGetCliRunner.GetWinGetExePath() ?? "winget";
                    _logService.LogInformation($"Using system winget for ADK install: {wingetExe}");
                }
                else
                {
                    wingetExe = WinGetCliRunner.GetBundledWinGetExePath()
                        ?? WinGetCliRunner.GetWinGetExePath()
                        ?? "winget";
                    _logService.LogInformation($"No system winget — using bundled CLI for ADK install: {wingetExe}");
                }

                var (exitCode, _) = await RunProcessWithProgressAsync(wingetExe, arguments, progress, cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new Exception($"winget install failed with exit code: {exitCode}");
                }

                if (await IsOscdimgAvailableAsync().ConfigureAwait(false))
                {
                    _logService.LogInformation("ADK installed via winget and oscdimg.exe found");
                    return true;
                }

                _logService.LogError("ADK installed via winget but oscdimg.exe not found");
                return false;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error installing ADK via winget: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<bool> CheckDiskSpace(string path, long requiredBytes, string operationName)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path)!);
                var availableBytes = drive.AvailableFreeSpace;

                var availableGB = availableBytes / (1024.0 * 1024 * 1024);
                var requiredGB = requiredBytes / (1024.0 * 1024 * 1024);

                _logService.LogInformation(
                    $"Disk space check for {operationName}: " +
                    $"Required: {requiredGB:F2} GB, Available: {availableGB:F2} GB on {drive.Name}"
                );

                if (availableBytes < requiredBytes)
                {
                    _logService.LogError(
                        $"Insufficient disk space for {operationName}. " +
                        $"Required: {requiredGB:F2} GB, Available: {availableGB:F2} GB"
                    );

                    throw new InsufficientDiskSpaceException(
                        drive.Name,
                        requiredGB,
                        availableGB,
                        operationName
                    );
                }

                return true;
            }
            catch (InsufficientDiskSpaceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not check disk space: {ex.Message}");
                return true;
            }
        }

        public async Task<bool> ValidateIsoFileAsync(string isoPath)
        {
            if (!File.Exists(isoPath))
            {
                _logService.LogError($"ISO file not found: {isoPath}");
                return false;
            }

            var extension = Path.GetExtension(isoPath).ToLowerInvariant();
            if (extension != ".iso")
            {
                _logService.LogError($"Invalid file extension: {extension}. Expected .iso");
                return false;
            }

            // Check if it's a valid ISO by attempting to read it
            try
            {
                var fileInfo = new FileInfo(isoPath);
                if (fileInfo.Length < 1024 * 1024) // Less than 1MB
                {
                    _logService.LogError("ISO file is too small to be valid");
                    return false;
                }

                _logService.LogInformation($"ISO file validated: {isoPath} ({fileInfo.Length:N0} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error validating ISO: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<bool> ExtractIsoAsync(
            string isoPath,
            string workingDirectory,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var isoMounted = false;

            try
            {
                if (!await ValidateIsoFileAsync(isoPath).ConfigureAwait(false))
                {
                    return false;
                }

                var isoFileInfo = new FileInfo(isoPath);
                var requiredSpace = isoFileInfo.Length + (2L * 1024 * 1024 * 1024);

                await CheckDiskSpace(workingDirectory, requiredSpace, "ISO extraction").ConfigureAwait(false);

                if (Directory.Exists(workingDirectory))
                {
                    _logService.LogInformation($"Clearing existing working directory: {workingDirectory}");

                    try
                    {
                        var script = $@"
                            Get-ChildItem -Path '{workingDirectory}' -Recurse -Force | ForEach-Object {{ $_.Attributes = 'Normal' }}
                            Remove-Item -Path '{workingDirectory}' -Recurse -Force -ErrorAction Stop
                        ";

                        var removeResult = await _processExecutor.ExecuteAsync(
                            "powershell.exe",
                            $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                            cancellationToken).ConfigureAwait(false);
                        var errorOutput = removeResult.StandardError;

                        if (Directory.Exists(workingDirectory))
                        {
                            _logService.LogError($"Failed to delete working directory. It may be in use by another process: {errorOutput}");
                            throw new InvalidOperationException(
                                $"Could not delete the existing working directory '{workingDirectory}'. " +
                                "It may be open in Windows Explorer or being used by another process. " +
                                "Please close delete it manually and try again."
                            );
                        }

                        _logService.LogInformation("Working directory cleared successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (InvalidOperationException)
                    {
                        throw;
                    }
                    catch (Exception cleanupEx)
                    {
                        _logService.LogError($"Failed to clear working directory: {cleanupEx.Message}", cleanupEx);
                        throw new InvalidOperationException($"Could not clear existing working directory: {cleanupEx.Message}", cleanupEx);
                    }
                }

                Directory.CreateDirectory(workingDirectory);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_MountingIso"),
                    TerminalOutput = $"ISO: {isoPath}"
                });

                _logService.LogInformation($"Mounting ISO: {isoPath}");

                var mountResult = await _processExecutor.ExecuteAsync(
                    "powershell.exe",
                    $"-NoProfile -Command \"(Mount-DiskImage -ImagePath '{isoPath}' -PassThru | Get-Volume).DriveLetter\"",
                    cancellationToken).ConfigureAwait(false);
                var rawOutput = mountResult.StandardOutput;

                var driveLetterMatch = System.Text.RegularExpressions.Regex.Match(rawOutput, @"\b[A-Z]\b");
                var driveLetter = driveLetterMatch.Success ? driveLetterMatch.Value : string.Empty;

                if (string.IsNullOrEmpty(driveLetter) || !mountResult.Succeeded)
                {
                    _logService.LogError("Failed to mount ISO or get drive letter");
                    return false;
                }

                isoMounted = true;
                var mountedPath = $"{driveLetter}:\\";
                _logService.LogInformation($"ISO mounted to: {mountedPath}");

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CopyingIsoContents"),
                    TerminalOutput = $"Source: {mountedPath}"
                });

                await Task.Run(() => CopyDirectory(mountedPath, workingDirectory, progress, cancellationToken), cancellationToken).ConfigureAwait(false);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_DismountingIso"),
                    TerminalOutput = "Cleaning up..."
                });

                _logService.LogInformation("Dismounting ISO");

                await _processExecutor.ExecuteAsync(
                    "powershell.exe",
                    $"-NoProfile -Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"",
                    cancellationToken).ConfigureAwait(false);
                isoMounted = false;

                var extractedDirs = Directory.GetDirectories(workingDirectory);
                var dirNames = extractedDirs.Select(d => Path.GetFileName(d)).ToList();
                _logService.LogInformation($"Found {extractedDirs.Length} directories: {string.Join(", ", dirNames)}");

                var hasSourcesDir = extractedDirs.Any(d =>
                    Path.GetFileName(d).Equals("sources", StringComparison.OrdinalIgnoreCase));
                var hasBootDir = extractedDirs.Any(d =>
                    Path.GetFileName(d).Equals("boot", StringComparison.OrdinalIgnoreCase));

                if (!hasSourcesDir || !hasBootDir)
                {
                    var foundDirs = string.Join(", ", dirNames);
                    _logService.LogError($"ISO extraction verification failed. Expected 'sources' and 'boot' folders. Found: {foundDirs}");
                    return false;
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_IsoExtractionCompleted"),
                    TerminalOutput = $"Extracted to: {workingDirectory}"
                });

                _logService.LogInformation($"ISO extracted successfully to: {workingDirectory}");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logService.LogInformation("ISO extraction was cancelled");

                if (isoMounted)
                {
                    try
                    {
                        _logService.LogInformation("Dismounting ISO due to cancellation");
                        await _processExecutor.ExecuteAsync(
                            "powershell.exe",
                            $"-NoProfile -Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"").ConfigureAwait(false);
                        _logService.LogInformation("ISO dismounted successfully");
                    }
                    catch (Exception dismountEx)
                    {
                        _logService.LogWarning($"Failed to dismount ISO on cancellation: {dismountEx.Message}");
                    }
                }

                throw;
            }
            catch (InsufficientDiskSpaceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error extracting ISO: {ex.Message}", ex);

                if (isoMounted)
                {
                    try
                    {
                        _logService.LogInformation("Dismounting ISO due to error");
                        await _processExecutor.ExecuteAsync(
                            "powershell.exe",
                            $"-NoProfile -Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"").ConfigureAwait(false);
                    }
                    catch (Exception dismountEx)
                    {
                        _logService.LogWarning($"Failed to dismount ISO on error: {dismountEx.Message}");
                    }
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_IsoExtractionFailed"),
                    TerminalOutput = ex.Message
                });
                return false;
            }
        }

        private void CopyDirectory(string sourceDir, string destDir, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dir = new DirectoryInfo(sourceDir);
            var dirs = dir.GetDirectories();

            Directory.CreateDirectory(destDir);

            foreach (var file in dir.GetFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var targetFilePath = Path.Combine(destDir, file.Name);
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CopyingFile", file.Name),
                    TerminalOutput = file.Name
                });
                file.CopyTo(targetFilePath, overwrite: true);
            }

            foreach (var subDir in dirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var newDestDir = Path.Combine(destDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestDir, progress, cancellationToken);
            }
        }

        public async Task<bool> AddXmlToImageAsync(string xmlPath, string workingDirectory)
        {
            try
            {
                if (!File.Exists(xmlPath))
                {
                    _logService.LogError($"XML file not found: {xmlPath}");
                    return false;
                }

                if (!Directory.Exists(workingDirectory))
                {
                    _logService.LogError($"Working directory not found: {workingDirectory}");
                    return false;
                }

                var destPath = Path.Combine(workingDirectory, "autounattend.xml");

                var xmlContent = await File.ReadAllTextAsync(xmlPath).ConfigureAwait(false);
                await File.WriteAllTextAsync(destPath, xmlContent).ConfigureAwait(false);

                _logService.LogInformation($"Added autounattend.xml to image: {destPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding XML to image: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<string> DownloadUnattendedWinstallXmlAsync(
            string destinationPath,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_DownloadingXml"),
                    TerminalOutput = UnattendedWinstallXmlUrl
                });

                var xmlContent = await _httpClient.GetStringAsync(UnattendedWinstallXmlUrl, cancellationToken).ConfigureAwait(false);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await File.WriteAllTextAsync(destinationPath, xmlContent, cancellationToken).ConfigureAwait(false);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_XmlDownloaded"),
                    TerminalOutput = $"Saved to: {destinationPath}"
                });

                _logService.LogInformation($"Downloaded UnattendedWinstall XML to: {destinationPath}");
                return destinationPath;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error downloading UnattendedWinstall XML: {ex.Message}", ex);
                throw;
            }
        }

        public async Task<bool> AddDriversAsync(
            string workingDirectory,
            string? driverSourcePath = null,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string sourceDirectory;

                if (string.IsNullOrEmpty(driverSourcePath))
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_ExportingDrivers"),
                        TerminalOutput = "This may take several minutes"
                    });

                    var tempDriverPath = Path.Combine(Path.GetTempPath(), $"WinhanceDrivers_{Guid.NewGuid()}");
                    Directory.CreateDirectory(tempDriverPath);

                    try
                    {
                        var arguments = $"/Online /Export-Driver /Destination:\"{tempDriverPath}\"";

                        progress?.Report(new TaskProgressDetail
                        {
                            TerminalOutput = "Exporting drivers from current system..."
                        });

                        var (exitCode, _) = await RunProcessWithProgressAsync("dism.exe", arguments, progress, cancellationToken).ConfigureAwait(false);
                        if (exitCode != 0)
                        {
                            throw new Exception($"DISM Export-Driver failed with exit code: {exitCode}");
                        }

                        sourceDirectory = tempDriverPath;
                    }
                    catch (Exception ex)
                    {
                        try { Directory.Delete(tempDriverPath, recursive: true); } catch (Exception cleanupEx) { _logService.LogDebug($"Best-effort temp driver directory cleanup failed: {cleanupEx.Message}"); }
                        _logService.LogError($"Failed to export system drivers: {ex.Message}", ex);
                        return false;
                    }
                }
                else
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_ValidatingDrivers"),
                        TerminalOutput = driverSourcePath
                    });

                    if (!Directory.Exists(driverSourcePath))
                    {
                        _logService.LogError($"Driver source path does not exist: {driverSourcePath}");
                        return false;
                    }

                    sourceDirectory = driverSourcePath;
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CategorizingDrivers"),
                    TerminalOutput = "Separating storage and post-install drivers"
                });

                var winpeDriverPath = Path.Combine(workingDirectory, "sources", "$WinpeDriver$");
                var oemDriverPath = Path.Combine(workingDirectory, "sources", "$OEM$", "$$", "Drivers");

                _logService.LogInformation($"Searching for drivers in: {sourceDirectory}");

                int copiedCount = await Task.Run(() => DriverCategorizer.CategorizeAndCopyDrivers(
                    sourceDirectory,
                    winpeDriverPath,
                    oemDriverPath,
                    _logService,
                    workingDirectory
                ), cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(driverSourcePath))
                {
                    try
                    {
                        Directory.Delete(sourceDirectory, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Could not delete temp directory: {ex.Message}");
                    }
                }

                if (copiedCount == 0)
                {
                    _logService.LogWarning($"No drivers were found or copied from: {sourceDirectory}");
                    return false;
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CreatingDriverScript"),
                    TerminalOutput = "Setting up SetupComplete.cmd"
                });

                CreateSetupCompleteScript(workingDirectory);

                _logService.LogInformation($"Successfully added {copiedCount} driver(s) - WinPE: {winpeDriverPath}, OEM: {oemDriverPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding drivers: {ex.Message}", ex);
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_DriverAdditionFailed"),
                    TerminalOutput = ex.Message
                });
                return false;
            }
        }

        private void CreateSetupCompleteScript(string workingDirectory)
        {
            try
            {
                var scriptsPath = Path.Combine(workingDirectory, "sources", "$OEM$", "$$", "Setup", "Scripts");
                Directory.CreateDirectory(scriptsPath);

                var setupCompleteScript = @"@echo off
REM Winhance Automatic Driver Installation Script
REM This script is executed automatically by Windows Setup

set LOGFILE=C:\Windows\Logs\DriverInstall.log

echo ================================================== > %LOGFILE%
echo Winhance Driver Installation Log >> %LOGFILE%
echo Date: %DATE% %TIME% >> %LOGFILE%
echo ================================================== >> %LOGFILE%
echo. >> %LOGFILE%

echo Installing drivers from C:\Windows\Drivers... >> %LOGFILE%
pnputil /add-driver C:\Windows\Drivers\*.inf /subdirs /install >> %LOGFILE% 2>&1

echo. >> %LOGFILE%
echo Driver installation completed >> %LOGFILE%
echo Exit Code: %ERRORLEVEL% >> %LOGFILE%

exit
";

                var scriptPath = Path.Combine(scriptsPath, "SetupComplete.cmd");
                File.WriteAllText(scriptPath, setupCompleteScript);

                _logService.LogInformation($"Created SetupComplete.cmd at: {scriptPath}");
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not create SetupComplete.cmd: {ex.Message}");
            }
        }

        public async Task<bool> CreateIsoAsync(
            string workingDirectory,
            string outputPath,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var oscdimgPath = GetOscdimgPath();

                if (!await IsOscdimgAvailableAsync().ConfigureAwait(false))
                {
                    _logService.LogError("oscdimg.exe is not available. Please download it first.");
                    return false;
                }

                var workingDirInfo = new DirectoryInfo(workingDirectory);
                var workingDirSize = workingDirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);

                var requiredSpace = workingDirSize + (2L * 1024 * 1024 * 1024);

                await CheckDiskSpace(outputPath, requiredSpace, "ISO creation").ConfigureAwait(false);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CreatingBootableIso"),
                    TerminalOutput = $"Output: {outputPath}"
                });

                var efisysPath = Path.Combine(workingDirectory, "efi", "microsoft", "boot", "efisys.bin");
                var etfsbootPath = Path.Combine(workingDirectory, "boot", "etfsboot.com");

                if (!File.Exists(etfsbootPath))
                    throw new FileNotFoundException($"Boot file not found: {etfsbootPath}");

                if (!File.Exists(efisysPath))
                    throw new FileNotFoundException($"UEFI boot file not found: {efisysPath}");

                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    _logService.LogInformation("Removed existing ISO file");
                }

                var arguments = $"-m -o -u2 -udfver102 -bootdata:2#p0,e,b\"{etfsbootPath}\"#pEF,e,b\"{efisysPath}\" \"{workingDirectory}\" \"{outputPath}\"";

                progress?.Report(new TaskProgressDetail
                {
                    TerminalOutput = "Running oscdimg.exe...\nThis may take several minutes..."
                });

                var (exitCode, _) = await RunProcessWithProgressAsync(oscdimgPath, arguments, progress, cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new Exception($"oscdimg.exe failed with exit code: {exitCode}");
                }

                // Verify ISO was created
                if (!File.Exists(outputPath))
                {
                    _logService.LogError("ISO file was not created");
                    return false;
                }

                var isoFileInfo = new FileInfo(outputPath);
                _logService.LogInformation($"ISO created successfully: {outputPath} ({isoFileInfo.Length:N0} bytes)");

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_IsoCreatedSuccess"),
                    TerminalOutput = $"Location: {outputPath}\nSize: {isoFileInfo.Length / (1024 * 1024):F2} MB"
                });

                return true;
            }
            catch (OperationCanceledException)
            {
                _logService.LogInformation("ISO creation was cancelled");
                throw;
            }
            catch (InsufficientDiskSpaceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error creating ISO: {ex.Message}", ex);
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_IsoCreationFailed"),
                    TerminalOutput = ex.Message
                });
                return false;
            }
        }

        public async Task<bool> CleanupWorkingDirectoryAsync(string workingDirectory)
        {
            try
            {
                if (!Directory.Exists(workingDirectory))
                {
                    return true;
                }

                _logService.LogInformation($"Cleaning up working directory: {workingDirectory}");

                await Task.Run(() =>
                {
                    Directory.Delete(workingDirectory, recursive: true);
                }).ConfigureAwait(false);

                _logService.LogInformation("Working directory cleaned up successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error cleaning up working directory: {ex.Message}", ex);
                return false;
            }
        }
    }
}

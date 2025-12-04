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

namespace Winhance.Infrastructure.Features.AdvancedTools.Services
{
    public class WimUtilService : IWimUtilService
    {
        private readonly IPowerShellExecutionService _powerShellService;
        private readonly ILogService _logService;
        private readonly HttpClient _httpClient;
        private readonly IWinGetService _winGetService;
        private readonly ILocalizationService _localization;

        private static readonly string[] AdkDownloadSources = new[]
        {
            "https://go.microsoft.com/fwlink/?linkid=2289980",
            "https://download.microsoft.com/download/2/d/9/2d9c8902-3fcd-48a6-a22a-432b08bed61e/ADK/adksetup.exe"
        };

        private const string UnattendedWinstallXmlUrl = "https://raw.githubusercontent.com/memstechtips/UnattendedWinstall/main/autounattend.xml";

        public WimUtilService(
            IPowerShellExecutionService powerShellService,
            ILogService logService,
            HttpClient httpClient,
            IWinGetService winGetService,
            ILocalizationService localization)
        {
            _powerShellService = powerShellService;
            _logService = logService;
            _httpClient = httpClient;
            _winGetService = winGetService;
            _localization = localization;
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
                    return await GetImageInfoAsync(wimPath, ImageFormat.Wim);
                }

                var esdPath = Path.Combine(sourcesPath, "install.esd");
                if (File.Exists(esdPath))
                {
                    return await GetImageInfoAsync(esdPath, ImageFormat.Esd);
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

        private async Task<ImageFormatInfo> GetImageInfoAsync(string imagePath, ImageFormat format)
        {
            try
            {
                var fileInfo = new FileInfo(imagePath);
                var info = new ImageFormatInfo
                {
                    Format = format,
                    FilePath = imagePath,
                    FileSizeBytes = fileInfo.Length
                };

                var script = $@"
$ErrorActionPreference = 'Stop'
$imagePath = '{imagePath.Replace("'", "''")}'

try {{
    $images = Get-WindowsImage -ImagePath $imagePath
    $imageCount = ($images | Measure-Object).Count

    Write-Host ""ImageCount:$imageCount""

    foreach ($img in $images) {{
        Write-Host ""Edition:$($img.ImageName)""
    }}

    exit 0
}}
catch {{
    Write-Host ""Error: $($_.Exception.Message)"" -ForegroundColor Red
    exit 1
}}
";

                var output = new System.Text.StringBuilder();
                var progress = new Progress<TaskProgressDetail>(detail =>
                {
                    if (!string.IsNullOrEmpty(detail.TerminalOutput))
                    {
                        output.AppendLine(detail.TerminalOutput);
                    }
                });

                await _powerShellService.ExecuteScriptFromContentAsync(script, progress, CancellationToken.None);

                var outputText = output.ToString();
                foreach (var line in outputText.Split('\n'))
                {
                    if (line.StartsWith("ImageCount:"))
                    {
                        if (int.TryParse(line.Substring(11).Trim(), out int count))
                        {
                            info.ImageCount = count;
                        }
                    }
                    else if (line.StartsWith("Edition:"))
                    {
                        info.EditionNames.Add(line.Substring(8).Trim());
                    }
                }

                _logService.LogInformation($"Image: {format}, {info.ImageCount} editions, {info.FileSizeBytes:N0} bytes");
                return info;
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not get detailed image info: {ex.Message}");
                var fileInfo = new FileInfo(imagePath);
                return new ImageFormatInfo
                {
                    Format = format,
                    FilePath = imagePath,
                    FileSizeBytes = fileInfo.Length,
                    ImageCount = 1
                };
            }
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
                var currentInfo = await DetectImageFormatAsync(workingDirectory);
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
                await CheckDiskSpace(workingDirectory, requiredSpace, "Image conversion");

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

                    var script = $@"
$ErrorActionPreference = 'Stop'
$sourceFile = '{sourceFile.Replace("'", "''")}'
$targetFile = '{targetFile.Replace("'", "''")}'
$index = {i}
$compressionType = '{compressionType}'
$isFirstIndex = {(i == 1 ? "$true" : "$false")}

try {{
    Write-Host ""Exporting index $index...""
    Write-Host ""Source: $sourceFile""
    Write-Host ""Target: $targetFile""
    Write-Host ""Compression: $compressionType""
    Write-Host ""First index: $isFirstIndex""

    if ($isFirstIndex) {{
        Write-Host ""Creating new image file...""
        dism /Export-Image /SourceImageFile:""$sourceFile"" /SourceIndex:$index /DestinationImageFile:""$targetFile"" /Compress:$compressionType /CheckIntegrity
    }} else {{
        Write-Host ""Appending to existing image file...""
        dism /Export-Image /SourceImageFile:""$sourceFile"" /SourceIndex:$index /DestinationImageFile:""$targetFile"" /Compress:$compressionType /CheckIntegrity
    }}

    if ($LASTEXITCODE -ne 0) {{
        throw ""DISM failed with exit code: $LASTEXITCODE""
    }}

    Write-Host ""Successfully exported index $index""
    exit 0
}}
catch {{
    Write-Host ""Error: $($_.Exception.Message)"" -ForegroundColor Red
    Write-Host ""Full error: $($_)"" -ForegroundColor Red
    exit 1
}}
";

                    await _powerShellService.ExecuteScriptFromContentAsync(script, progress, cancellationToken);
                }

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
                            File.Delete(sourceFile);
                            _logService.LogInformation($"Deleted source file: {sourceFile}");
                            deleted = true;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Attempt {attempt}/5 to delete source file failed: {ex.Message}");
                        if (attempt < 5)
                        {
                            await Task.Delay(2000, cancellationToken);
                        }
                    }
                }

                if (!deleted && File.Exists(sourceFile))
                {
                    _logService.LogError($"Failed to delete source file after 5 attempts: {sourceFile}");
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_WarningCouldNotDeleteFile"),
                        TerminalOutput = $"Please manually delete: {Path.GetFileName(sourceFile)}"
                    });
                }

                var targetFileInfo = new FileInfo(targetFile);
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

        public async Task<bool> IsOscdimgAvailableAsync()
        {
            var oscdimgPath = GetOscdimgPath();
            if (string.IsNullOrEmpty(oscdimgPath))
            {
                _logService.LogInformation("oscdimg.exe not found in Windows Kits directories");
                return false;
            }

            _logService.LogInformation($"oscdimg.exe found at: {oscdimgPath}");
            return await Task.FromResult(true);
        }

        public async Task<bool> EnsureOscdimgAvailableAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (await IsOscdimgAvailableAsync())
            {
                _logService.LogInformation("oscdimg.exe already available");
                return true;
            }

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_PreparingInstallAdk"),
                TerminalOutput = "Checking installation methods"
            });

            if (await InstallAdkDeploymentToolsAsync(progress, cancellationToken))
            {
                return true;
            }

            _logService.LogWarning("Standard ADK installation failed, trying winget...");
            if (await InstallAdkViaWingetAsync(progress, cancellationToken))
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
                    var response = await _httpClient.GetAsync(sourceUrl, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var setupBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    await File.WriteAllBytesAsync(adkSetupPath, setupBytes, cancellationToken);

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
                var adkSetupPath = await DownloadAdkSetupAsync(progress, cancellationToken);
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
                var installScript = $@"
$ErrorActionPreference = 'Stop'
$adkSetup = '{adkSetupPath.Replace("'", "''")}'
$logPath = '{logPath.Replace("'", "''")}'

try {{
    Write-Host 'Starting ADK Deployment Tools installation...'

    $arguments = @(
        '/quiet',
        '/norestart',
        '/features', 'OptionId.DeploymentTools',
        '/ceip', 'off',
        '/log', $logPath
    )

    $process = Start-Process -FilePath $adkSetup -ArgumentList $arguments -NoNewWindow -Wait -PassThru

    if ($process.ExitCode -eq 0) {{
        Write-Host 'ADK Deployment Tools installed successfully!'
        exit 0
    }} else {{
        throw ""Installation failed with exit code: $($process.ExitCode)""
    }}
}}
catch {{
    Write-Host ""Error: $($_.Exception.Message)"" -ForegroundColor Red
    if (Test-Path $logPath) {{
        Get-Content $logPath | Select-Object -Last 20
    }}
    exit 1
}}
";

                await _powerShellService.ExecuteScriptAsync(installScript, progress, cancellationToken);

                if (await IsOscdimgAvailableAsync())
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
                catch { }
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

                var wingetInstalled = await _winGetService.IsWinGetInstalledAsync(cancellationToken);

                if (!wingetInstalled)
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_InstallingWinget"),
                        TerminalOutput = "winget is required for this installation method"
                    });

                    var wingetInstallSuccess = await _winGetService.InstallWinGetAsync(cancellationToken);
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
                var installScript = $@"
$ErrorActionPreference = 'Stop'
$logPath = '{logPath.Replace("'", "''")}'

try {{
    Write-Host 'Starting ADK installation via winget...'

    $arguments = '/quiet /norestart /features OptionId.DeploymentTools /ceip off'

    winget install Microsoft.WindowsADK --exact --silent --accept-package-agreements --accept-source-agreements --override $arguments --log $logPath

    if ($LASTEXITCODE -eq 0) {{
        Write-Host 'ADK installed successfully via winget!'
        exit 0
    }} else {{
        throw ""winget install failed with exit code: $LASTEXITCODE""
    }}
}}
catch {{
    Write-Host ""Error: $($_.Exception.Message)"" -ForegroundColor Red
    if (Test-Path $logPath) {{
        Write-Host 'Installation log:'
        Get-Content $logPath | Select-Object -Last 20
    }}
    exit 1
}}
";

                await _powerShellService.ExecuteScriptAsync(installScript, progress, cancellationToken);

                if (await IsOscdimgAvailableAsync())
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
                if (!await ValidateIsoFileAsync(isoPath))
                {
                    return false;
                }

                var isoFileInfo = new FileInfo(isoPath);
                var requiredSpace = isoFileInfo.Length + (2L * 1024 * 1024 * 1024);

                await CheckDiskSpace(workingDirectory, requiredSpace, "ISO extraction");

                if (Directory.Exists(workingDirectory))
                {
                    _logService.LogInformation($"Clearing existing working directory: {workingDirectory}");

                    try
                    {
                        var script = $@"
                            Get-ChildItem -Path '{workingDirectory}' -Recurse -Force | ForEach-Object {{ $_.Attributes = 'Normal' }}
                            Remove-Item -Path '{workingDirectory}' -Recurse -Force -ErrorAction Stop
                        ";

                        var removeProcess = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "powershell.exe",
                                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };

                        removeProcess.Start();
                        var errorOutput = await removeProcess.StandardError.ReadToEndAsync();
                        await removeProcess.WaitForExitAsync(cancellationToken);

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

                var mountProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"(Mount-DiskImage -ImagePath '{isoPath}' -PassThru | Get-Volume).DriveLetter\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                mountProcess.Start();
                var driveLetter = (await mountProcess.StandardOutput.ReadToEndAsync()).Trim();
                await mountProcess.WaitForExitAsync(cancellationToken);

                if (string.IsNullOrEmpty(driveLetter) || mountProcess.ExitCode != 0)
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

                await Task.Run(() => CopyDirectory(mountedPath, workingDirectory, progress, cancellationToken), cancellationToken);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_DismountingIso"),
                    TerminalOutput = "Cleaning up..."
                });

                _logService.LogInformation("Dismounting ISO");

                var dismountProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                dismountProcess.Start();
                await dismountProcess.WaitForExitAsync(cancellationToken);
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
                        var dismountProcess = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "powershell.exe",
                                Arguments = $"-Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        dismountProcess.Start();
                        await dismountProcess.WaitForExitAsync();
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
                        var dismountProcess = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "powershell.exe",
                                Arguments = $"-Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        dismountProcess.Start();
                        await dismountProcess.WaitForExitAsync();
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

                var xmlContent = await File.ReadAllTextAsync(xmlPath);
                await File.WriteAllTextAsync(destPath, xmlContent);

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

                var xmlContent = await _httpClient.GetStringAsync(UnattendedWinstallXmlUrl, cancellationToken);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await File.WriteAllTextAsync(destinationPath, xmlContent, cancellationToken);

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

                    string script = $@"
$ErrorActionPreference = 'Stop'
$destPath = '{tempDriverPath.Replace("'", "''")}'

try {{
    Write-Host 'Exporting drivers from current system...'
    Write-Host 'This operation may take several minutes...'

    $result = Export-WindowsDriver -Online -Destination $destPath

    $driverCount = ($result | Measure-Object).Count
    Write-Host ""Successfully exported $driverCount driver packages""

    exit 0
}}
catch {{
    Write-Host ""Error: $($_.Exception.Message)"" -ForegroundColor Red
    exit 1
}}
";

                    try
                    {
                        await _powerShellService.ExecuteScriptAsync(script, progress, cancellationToken);
                        sourceDirectory = tempDriverPath;
                    }
                    catch (Exception ex)
                    {
                        try { Directory.Delete(tempDriverPath, recursive: true); } catch { }
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
                ), cancellationToken);

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

                if (!await IsOscdimgAvailableAsync())
                {
                    _logService.LogError("oscdimg.exe is not available. Please download it first.");
                    return false;
                }

                var workingDirInfo = new DirectoryInfo(workingDirectory);
                var workingDirSize = workingDirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);

                var requiredSpace = workingDirSize + (2L * 1024 * 1024 * 1024);

                await CheckDiskSpace(outputPath, requiredSpace, "ISO creation");

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CreatingBootableIso"),
                    TerminalOutput = $"Output: {outputPath}"
                });

                var efisysPath = Path.Combine(workingDirectory, "efi", "microsoft", "boot", "efisys.bin");
                var etfsbootPath = Path.Combine(workingDirectory, "boot", "etfsboot.com");

                var script = $@"
$ErrorActionPreference = 'Stop'
$oscdimgPath = '{oscdimgPath.Replace("'", "''")}'
$workingDir = '{workingDirectory.Replace("'", "''")}'
$outputPath = '{outputPath.Replace("'", "''")}'
$etfsbootPath = '{etfsbootPath.Replace("'", "''")}'
$efisysPath = '{efisysPath.Replace("'", "''")}'

try {{
    Write-Host 'Creating bootable ISO with oscdimg.exe...'
    Write-Host ""Source: $workingDir""
    Write-Host ""Output: $outputPath""

    if (!(Test-Path $etfsbootPath)) {{
        throw 'Boot file not found: $etfsbootPath'
    }}

    if (!(Test-Path $efisysPath)) {{
        throw 'UEFI boot file not found: $efisysPath'
    }}

    $outputDir = Split-Path $outputPath -Parent
    if (!(Test-Path $outputDir)) {{
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }}

    if (Test-Path $outputPath) {{
        Remove-Item $outputPath -Force
        Write-Host 'Removed existing ISO file'
    }}

    $arguments = ""-m -o -u2 -udfver102 -bootdata:2#p0,e,b""""$etfsbootPath""""#pEF,e,b""""$efisysPath"""" """"$workingDir"""" """"$outputPath""""""

    Write-Host 'Running oscdimg.exe...'
    Write-Host 'This may take several minutes...'

    $process = Start-Process -FilePath $oscdimgPath -ArgumentList $arguments -NoNewWindow -Wait -PassThru

    if ($process.ExitCode -eq 0) {{
        Write-Host 'ISO created successfully!'
        if (Test-Path $outputPath) {{
            $fileInfo = Get-Item $outputPath
            Write-Host ""ISO Size: $($fileInfo.Length / 1MB) MB""
            exit 0
        }} else {{
            throw 'ISO file was not created'
        }}
    }} else {{
        throw ""oscdimg.exe failed with exit code: $($process.ExitCode)""
    }}
}}
catch {{
    Write-Host ""Error: $($_.Exception.Message)"" -ForegroundColor Red
    exit 1
}}
";

                await _powerShellService.ExecuteScriptFromContentAsync(script, progress, cancellationToken);

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
                });

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

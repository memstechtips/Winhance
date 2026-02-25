using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services
{
    public class IsoService : IIsoService
    {
        private static readonly Regex DriveLetterRegex = new(@"\b[A-Z]\b", RegexOptions.Compiled);
        private readonly IFileSystemService _fileSystemService;
        private readonly ILogService _logService;
        private readonly ILocalizationService _localization;
        private readonly IProcessExecutor _processExecutor;
        private readonly IDismProcessRunner _dismProcessRunner;
        private readonly IOscdimgToolManager _oscdimgToolManager;

        public IsoService(
            IFileSystemService fileSystemService,
            ILogService logService,
            ILocalizationService localization,
            IProcessExecutor processExecutor,
            IDismProcessRunner dismProcessRunner,
            IOscdimgToolManager oscdimgToolManager)
        {
            _fileSystemService = fileSystemService;
            _logService = logService;
            _localization = localization;
            _processExecutor = processExecutor;
            _dismProcessRunner = dismProcessRunner;
            _oscdimgToolManager = oscdimgToolManager;
        }

        public Task<bool> ValidateIsoFileAsync(string isoPath)
        {
            if (!_fileSystemService.FileExists(isoPath))
            {
                _logService.LogError($"ISO file not found: {isoPath}");
                return Task.FromResult(false);
            }

            var extension = _fileSystemService.GetExtension(isoPath).ToLowerInvariant();
            if (extension != ".iso")
            {
                _logService.LogError($"Invalid file extension: {extension}. Expected .iso");
                return Task.FromResult(false);
            }

            // Check if it's a valid ISO by attempting to read it
            try
            {
                var fileSize = _fileSystemService.GetFileSize(isoPath);
                if (fileSize < 1024 * 1024) // Less than 1MB
                {
                    _logService.LogError("ISO file is too small to be valid");
                    return Task.FromResult(false);
                }

                _logService.LogInformation($"ISO file validated: {isoPath} ({fileSize:N0} bytes)");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error validating ISO: {ex.Message}", ex);
                return Task.FromResult(false);
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

                var isoFileSize = _fileSystemService.GetFileSize(isoPath);
                var requiredSpace = isoFileSize + (2L * 1024 * 1024 * 1024);

                await _dismProcessRunner.CheckDiskSpaceAsync(workingDirectory, requiredSpace, "ISO extraction").ConfigureAwait(false);

                if (_fileSystemService.DirectoryExists(workingDirectory))
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

                        if (_fileSystemService.DirectoryExists(workingDirectory))
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

                _fileSystemService.CreateDirectory(workingDirectory);

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

                var driveLetterMatch = DriveLetterRegex.Match(rawOutput);
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

                var extractedDirs = _fileSystemService.GetDirectories(workingDirectory);
                var dirNames = extractedDirs.Select(d => _fileSystemService.GetFileName(d)).ToList();
                _logService.LogInformation($"Found {extractedDirs.Length} directories: {string.Join(", ", dirNames)}");

                var hasSourcesDir = extractedDirs.Any(d =>
                    _fileSystemService.GetFileName(d).Equals("sources", StringComparison.OrdinalIgnoreCase));
                var hasBootDir = extractedDirs.Any(d =>
                    _fileSystemService.GetFileName(d).Equals("boot", StringComparison.OrdinalIgnoreCase));

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

        public async Task<bool> CreateIsoAsync(
            string workingDirectory,
            string outputPath,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var oscdimgPath = _oscdimgToolManager.GetOscdimgPath();

                if (!await _oscdimgToolManager.IsOscdimgAvailableAsync().ConfigureAwait(false))
                {
                    _logService.LogError("oscdimg.exe is not available. Please download it first.");
                    return false;
                }

                var workingDirSize = _fileSystemService.GetFiles(workingDirectory, "*", SearchOption.AllDirectories)
                    .Sum(f => _fileSystemService.GetFileSize(f));

                var requiredSpace = workingDirSize + (2L * 1024 * 1024 * 1024);

                await _dismProcessRunner.CheckDiskSpaceAsync(outputPath, requiredSpace, "ISO creation").ConfigureAwait(false);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CreatingBootableIso"),
                    TerminalOutput = $"Output: {outputPath}"
                });

                var efisysPath = _fileSystemService.CombinePath(workingDirectory, "efi", "microsoft", "boot", "efisys.bin");
                var etfsbootPath = _fileSystemService.CombinePath(workingDirectory, "boot", "etfsboot.com");

                if (!_fileSystemService.FileExists(etfsbootPath))
                    throw new FileNotFoundException($"Boot file not found: {etfsbootPath}");

                if (!_fileSystemService.FileExists(efisysPath))
                    throw new FileNotFoundException($"UEFI boot file not found: {efisysPath}");

                var outputDir = _fileSystemService.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !_fileSystemService.DirectoryExists(outputDir))
                    _fileSystemService.CreateDirectory(outputDir);

                if (_fileSystemService.FileExists(outputPath))
                {
                    _fileSystemService.DeleteFile(outputPath);
                    _logService.LogInformation("Removed existing ISO file");
                }

                var arguments = $"-m -o -u2 -udfver102 -bootdata:2#p0,e,b\"{etfsbootPath}\"#pEF,e,b\"{efisysPath}\" \"{workingDirectory}\" \"{outputPath}\"";

                progress?.Report(new TaskProgressDetail
                {
                    TerminalOutput = "Running oscdimg.exe...\nThis may take several minutes..."
                });

                var (exitCode, _) = await _dismProcessRunner.RunProcessWithProgressAsync(oscdimgPath, arguments, progress, cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new Exception($"oscdimg.exe failed with exit code: {exitCode}");
                }

                // Verify ISO was created
                if (!_fileSystemService.FileExists(outputPath))
                {
                    _logService.LogError("ISO file was not created");
                    return false;
                }

                var isoFileSize = _fileSystemService.GetFileSize(outputPath);
                _logService.LogInformation($"ISO created successfully: {outputPath} ({isoFileSize:N0} bytes)");

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_IsoCreatedSuccess"),
                    TerminalOutput = $"Location: {outputPath}\nSize: {isoFileSize / (1024 * 1024):F2} MB"
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
                if (!_fileSystemService.DirectoryExists(workingDirectory))
                {
                    return true;
                }

                _logService.LogInformation($"Cleaning up working directory: {workingDirectory}");

                await Task.Run(() =>
                {
                    _fileSystemService.DeleteDirectory(workingDirectory, recursive: true);
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

        private void CopyDirectory(string sourceDir, string destDir, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dirs = _fileSystemService.GetDirectories(sourceDir);

            _fileSystemService.CreateDirectory(destDir);

            foreach (var file in _fileSystemService.GetFiles(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = _fileSystemService.GetFileName(file);
                var targetFilePath = _fileSystemService.CombinePath(destDir, fileName);
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CopyingFile", fileName),
                    TerminalOutput = fileName
                });
                _fileSystemService.CopyFile(file, targetFilePath, true);
            }

            foreach (var subDir in dirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var subDirName = _fileSystemService.GetFileName(subDir);
                var newDestDir = _fileSystemService.CombinePath(destDir, subDirName);
                CopyDirectory(subDir, newDestDir, progress, cancellationToken);
            }
        }
    }
}

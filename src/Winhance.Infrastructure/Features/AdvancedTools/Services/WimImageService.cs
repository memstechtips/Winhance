using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services
{
    public class WimImageService : IWimImageService
    {
        private readonly IFileSystemService _fileSystemService;
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogService _logService;
        private readonly ILocalizationService _localization;
        private readonly IDismProcessRunner _dismProcessRunner;

        public WimImageService(
            IFileSystemService fileSystemService,
            IProcessExecutor processExecutor,
            ILogService logService,
            ILocalizationService localization,
            IDismProcessRunner dismProcessRunner)
        {
            _fileSystemService = fileSystemService;
            _processExecutor = processExecutor;
            _logService = logService;
            _localization = localization;
            _dismProcessRunner = dismProcessRunner;
        }

        public async Task<ImageFormatInfo?> DetectImageFormatAsync(string workingDirectory)
        {
            try
            {
                var sourcesPath = _fileSystemService.CombinePath(workingDirectory, "sources");
                if (!_fileSystemService.DirectoryExists(sourcesPath))
                {
                    _logService.LogWarning($"Sources directory not found: {sourcesPath}");
                    return null;
                }

                var wimPath = _fileSystemService.CombinePath(sourcesPath, "install.wim");
                if (_fileSystemService.FileExists(wimPath))
                {
                    return await GetImageInfoAsync(wimPath, ImageFormat.Wim).ConfigureAwait(false);
                }

                var esdPath = _fileSystemService.CombinePath(sourcesPath, "install.esd");
                if (_fileSystemService.FileExists(esdPath))
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
            ImageFormatInfo? wimInfo = null;
            ImageFormatInfo? esdInfo = null;

            try
            {
                var sourcesPath = _fileSystemService.CombinePath(workingDirectory, "sources");
                if (!_fileSystemService.DirectoryExists(sourcesPath))
                {
                    _logService.LogWarning($"Sources directory not found: {sourcesPath}");
                    return new ImageDetectionResult();
                }

                var wimPath = _fileSystemService.CombinePath(sourcesPath, "install.wim");
                if (_fileSystemService.FileExists(wimPath))
                {
                    wimInfo = await GetImageInfoAsync(wimPath, ImageFormat.Wim).ConfigureAwait(false);
                }

                var esdPath = _fileSystemService.CombinePath(sourcesPath, "install.esd");
                if (_fileSystemService.FileExists(esdPath))
                {
                    esdInfo = await GetImageInfoAsync(esdPath, ImageFormat.Esd).ConfigureAwait(false);
                }

                var result = new ImageDetectionResult { WimInfo = wimInfo, EsdInfo = esdInfo };

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

                return result;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error detecting image formats: {ex.Message}", ex);
            }

            return new ImageDetectionResult { WimInfo = wimInfo, EsdInfo = esdInfo };
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

                var sourcesPath = _fileSystemService.CombinePath(workingDirectory, "sources");
                var sourceFile = currentInfo.FilePath;
                targetFile = targetFormat == ImageFormat.Wim
                    ? _fileSystemService.CombinePath(sourcesPath, "install.wim")
                    : _fileSystemService.CombinePath(sourcesPath, "install.esd");

                var requiredSpace = currentInfo.FileSizeBytes * 2;
                await _dismProcessRunner.CheckDiskSpaceAsync(workingDirectory, requiredSpace, "Image conversion").ConfigureAwait(false);

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

                    var (exitCode, _) = await _dismProcessRunner.RunProcessWithProgressAsync("dism.exe", arguments, progress, cancellationToken).ConfigureAwait(false);
                    if (exitCode != 0)
                    {
                        throw new Exception($"DISM failed with exit code: {exitCode}");
                    }
                }

                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

                if (!_fileSystemService.FileExists(targetFile))
                {
                    _logService.LogError($"Target file not found: {targetFile}");
                    return false;
                }

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_RemovingOldFile"),
                    TerminalOutput = $"Deleting {_fileSystemService.GetFileName(sourceFile)}"
                });

                var deleted = false;
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    try
                    {
                        if (_fileSystemService.FileExists(sourceFile))
                        {
                            _fileSystemService.SetFileAttributes(sourceFile, FileAttributes.Normal);
                            _fileSystemService.DeleteFile(sourceFile);
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

                var targetFileSize = _fileSystemService.GetFileSize(targetFile);

                if (!deleted && _fileSystemService.FileExists(sourceFile))
                {
                    _logService.LogWarning($"Could not delete source file after multiple attempts: {sourceFile}");

                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = _localization.GetString("Progress_ConversionCompleted"),
                        TerminalOutput = $"Conversion succeeded! New size: {targetFileSize / (1024.0 * 1024 * 1024):F2} GB\n\n" +
                                       $"However, the source file is still in use and could not be deleted automatically.\n\n" +
                                       $"Please manually delete:\n{sourceFile}"
                    });

                    return true;
                }

                var sizeDiff = currentInfo.FileSizeBytes - targetFileSize;
                var savedSpace = sizeDiff > 0
                    ? $"Saved {sizeDiff / (1024.0 * 1024 * 1024):F2} GB"
                    : $"Used {Math.Abs(sizeDiff) / (1024.0 * 1024 * 1024):F2} GB more";

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_ConversionCompleted"),
                    TerminalOutput = $"New size: {targetFileSize / (1024.0 * 1024 * 1024):F2} GB\n{savedSpace}"
                });

                _logService.LogInformation($"Conversion successful: {currentInfo.Format} → {targetFormat}");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logService.LogInformation("Image conversion was cancelled");

                if (_fileSystemService.FileExists(targetFile))
                {
                    try
                    {
                        _logService.LogInformation($"Cleaning up incomplete target file: {targetFile}");
                        _fileSystemService.DeleteFile(targetFile);
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

                if (_fileSystemService.FileExists(targetFile))
                {
                    try
                    {
                        _logService.LogInformation($"Cleaning up incomplete target file: {targetFile}");
                        _fileSystemService.DeleteFile(targetFile);
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

        public async Task<bool> DeleteImageFileAsync(
            string workingDirectory,
            ImageFormat format,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var sourcesPath = _fileSystemService.CombinePath(workingDirectory, "sources");
                var fileName = format == ImageFormat.Wim ? "install.wim" : "install.esd";
                var filePath = _fileSystemService.CombinePath(sourcesPath, fileName);

                if (!_fileSystemService.FileExists(filePath))
                {
                    _logService.LogWarning($"File not found for deletion: {filePath}");
                    return false;
                }

                var fileSizeGB = _fileSystemService.GetFileSize(filePath) / (1024.0 * 1024 * 1024);

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
                        if (_fileSystemService.FileExists(filePath))
                        {
                            _fileSystemService.SetFileAttributes(filePath, FileAttributes.Normal);
                            _fileSystemService.DeleteFile(filePath);
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
            long fileSizeBytes = _fileSystemService.GetFileSize(imagePath);
            int imageCount = 1;
            IReadOnlyList<string> editionNames = new List<string>();

            try
            {
                var arguments = $"/Get-ImageInfo /ImageFile:\"{imagePath}\"";
                _logService.LogInformation($"Running: dism.exe {arguments}");

                var result = await _processExecutor.ExecuteAsync("dism.exe", arguments).ConfigureAwait(false);
                var stdout = result.StandardOutput;

                if (result.Succeeded)
                {
                    int parsedCount = 0;
                    var parsedNames = new List<string>();
                    foreach (var line in stdout.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("Index :", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("Index:", StringComparison.OrdinalIgnoreCase))
                        {
                            parsedCount++;
                        }
                        else if (trimmed.StartsWith("Name :", StringComparison.OrdinalIgnoreCase) ||
                                 trimmed.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                        {
                            var name = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
                            if (!string.IsNullOrEmpty(name))
                                parsedNames.Add(name);
                        }
                    }

                    editionNames = parsedNames;
                    imageCount = parsedCount > 0 ? parsedCount : 1;
                    _logService.LogInformation($"Image: {format}, {imageCount} editions, {fileSizeBytes:N0} bytes");
                }
                else
                {
                    _logService.LogWarning($"dism.exe /Get-ImageInfo exited with code {result.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not get detailed image info: {ex.Message}");
            }

            return new ImageFormatInfo
            {
                Format = format,
                FilePath = imagePath,
                FileSizeBytes = fileSizeBytes,
                ImageCount = imageCount,
                EditionNames = editionNames
            };
        }

        private void KillDismProcesses()
        {
            try
            {
                _logService.LogInformation("Killing all DISM processes");
                _processExecutor.KillProcessesByName("dism");
                _logService.LogInformation("DISM process kill completed");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error killing DISM processes: {ex.Message}", ex);
            }
        }
    }
}

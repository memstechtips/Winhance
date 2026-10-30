using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal class WimImageService : IWimImageService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localization;
    private readonly IDismProcessRunner _dismProcessRunner;
    private readonly IDismImageInfoReader _dismImageInfoReader;

    internal TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public WimImageService(
        IFileSystemService fileSystemService,
        ILogService logService,
        ILocalizationService localization,
        IDismProcessRunner dismProcessRunner,
        IDismImageInfoReader dismImageInfoReader)
    {
        _fileSystemService = fileSystemService;
        _logService = logService;
        _localization = localization;
        _dismProcessRunner = dismProcessRunner;
        _dismImageInfoReader = dismImageInfoReader;
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

            var editions = currentInfo.Editions;
            _logService.LogInformation($"Converting {editions.Count} image(s)");

            for (var i = 0; i < editions.Count; i++)
            {
                var status = _localization.GetString("Progress_ConvertingEdition", (i + 1).ToString(), editions.Count.ToString());
                await ExportAsync(sourceFile, editions[i], targetFile, CompressionFor(targetFormat), status, progress, cancellationToken).ConfigureAwait(false);
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

            var deleted = await DeleteWithRetryAsync(sourceFile, cancellationToken).ConfigureAwait(false);

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

            DeleteIncompleteFile(targetFile);

            throw;
        }
        catch (InsufficientDiskSpaceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error converting image: {ex.Message}", ex);

            DeleteIncompleteFile(targetFile);

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_ConversionFailed"),
                TerminalOutput = ex.Message
            });
            return false;
        }
    }

    public Task<EditionRemovalResult> KeepEditionsAsync(
        string workingDirectory,
        IReadOnlyCollection<string> editionNames,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default) =>
        RewriteKeepingAsync(workingDirectory, editionNames, null, progress, cancellationToken);

    public Task<EditionRemovalResult> KeepEditionAsWimAsync(
        string workingDirectory,
        string editionName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default) =>
        RewriteKeepingAsync(workingDirectory, [editionName], ImageFormat.Wim, progress, cancellationToken);

    // A null targetFormat keeps the image in the format it is in.
    private async Task<EditionRemovalResult> RewriteKeepingAsync(
        string workingDirectory,
        IReadOnlyCollection<string> editionNames,
        ImageFormat? targetFormat,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        string tempFile = string.Empty;

        try
        {
            var currentInfo = await DetectImageFormatAsync(workingDirectory).ConfigureAwait(false);
            if (currentInfo == null)
            {
                _logService.LogError("Could not detect the image to remove editions from");
                return EditionRemovalResult.Failed;
            }

            var kept = currentInfo.Editions.Where(e => editionNames.Contains(e.Name)).ToList();
            if (kept.Count == 0)
            {
                _logService.LogError("No edition was chosen to keep");
                return EditionRemovalResult.Failed;
            }

            var format = targetFormat ?? currentInfo.Format;
            if (kept.Count == currentInfo.Editions.Count && format == currentInfo.Format)
            {
                _logService.LogInformation("Every edition is kept, nothing to remove");
                return new EditionRemovalResult(true);
            }

            var sourceFile = currentInfo.FilePath;
            var sourcesPath = _fileSystemService.CombinePath(workingDirectory, "sources");
            var targetFile = _fileSystemService.CombinePath(sourcesPath, $"install.{ExtensionOf(format)}");
            tempFile = _fileSystemService.CombinePath(sourcesPath, $"install.keep.{ExtensionOf(format)}");
            var backupFile = _fileSystemService.CombinePath(sourcesPath, $"install.old.{ExtensionOf(currentInfo.Format)}");

            // An ESD exported to a WIM roughly doubles in size, the same factor ConvertImageAsync reserves.
            var requiredSpace = format == currentInfo.Format ? currentInfo.FileSizeBytes : currentInfo.FileSizeBytes * 2;
            await _dismProcessRunner.CheckDiskSpaceAsync(workingDirectory, requiredSpace, "Edition removal").ConfigureAwait(false);

            // /Export-Image appends to an existing destination, so a file left by an interrupted run would carry its editions in.
            if (!await DeleteWithRetryAsync(tempFile, cancellationToken).ConfigureAwait(false)
                || !await DeleteWithRetryAsync(backupFile, cancellationToken).ConfigureAwait(false))
            {
                _logService.LogError("A file left by an earlier edition removal could not be deleted");
                return EditionRemovalResult.Failed;
            }

            _logService.LogInformation($"Keeping {kept.Count} of {currentInfo.Editions.Count} editions: {string.Join(", ", kept.Select(e => e.Name))}");

            for (var i = 0; i < kept.Count; i++)
            {
                var status = _localization.GetString("Progress_KeepingEdition", (i + 1).ToString(), kept.Count.ToString());
                await ExportAsync(sourceFile, kept[i], tempFile, CompressionFor(format), status, progress, cancellationToken).ConfigureAwait(false);
            }

            if (!_fileSystemService.FileExists(tempFile))
            {
                _logService.LogError($"Export finished but {tempFile} is missing");
                return EditionRemovalResult.Failed;
            }

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_RemovingOldFile"),
                TerminalOutput = $"Replacing {_fileSystemService.GetFileName(sourceFile)}"
            });

            // The original steps aside before the new file takes its name, so no failure point leaves the media without an image.
            if (!await RetryAsync($"move {sourceFile} aside", () => _fileSystemService.MoveFile(sourceFile, backupFile), cancellationToken).ConfigureAwait(false))
            {
                DeleteIncompleteFile(tempFile);
                return EditionRemovalResult.Failed;
            }

            try
            {
                _fileSystemService.MoveFile(tempFile, targetFile);
            }
            catch
            {
                _fileSystemService.MoveFile(backupFile, sourceFile);
                throw;
            }

            // The new image has the name by now, so a cancel has nothing left to undo and must not skip the clean-up.
            var oldImageDeleted = await DeleteWithRetryAsync(backupFile, CancellationToken.None).ConfigureAwait(false);
            if (!oldImageDeleted)
                _logService.LogWarning($"Could not delete {backupFile}; it would go onto the ISO");

            _logService.LogInformation($"Image now holds {kept.Count} edition(s), {_fileSystemService.GetFileSize(targetFile):N0} bytes");
            return new EditionRemovalResult(true, oldImageDeleted ? null : backupFile);
        }
        catch (OperationCanceledException)
        {
            _logService.LogInformation("Edition removal was cancelled");
            DeleteIncompleteFile(tempFile);
            throw;
        }
        catch (InsufficientDiskSpaceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error removing editions: {ex.Message}", ex);
            DeleteIncompleteFile(tempFile);
            return EditionRemovalResult.Failed;
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

            var deleted = await DeleteWithRetryAsync(filePath, cancellationToken).ConfigureAwait(false);
            if (deleted)
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_ImageFileDeleted"),
                    TerminalOutput = $"{fileName} deleted successfully"
                });
            }
            else
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

    private static string CompressionFor(ImageFormat format) => format == ImageFormat.Esd ? "recovery" : "max";

    private static string ExtensionOf(ImageFormat format) => format == ImageFormat.Esd ? "esd" : "wim";

    private async Task ExportAsync(
        string sourceFile,
        ImageEdition edition,
        string targetFile,
        string compression,
        string status,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new TaskProgressDetail { StatusText = status, TerminalOutput = edition.Name });

        // dism.exe, not the DISM API: DismApi.dll services images, it cannot export one.
        // There is no DismExportImage, and WIMGAPI ships with the ADK rather than the SDK
        // and cannot open an ESD at all. This shell-out is permanent.
        var arguments = $"/Export-Image /SourceImageFile:\"{sourceFile}\" /SourceIndex:{edition.Index} /DestinationImageFile:\"{targetFile}\" /Compress:{compression} /CheckIntegrity";

        _logService.LogInformation($"Exporting index {edition.Index}: dism.exe {arguments}");

        var (exitCode, _) = await _dismProcessRunner.RunProcessWithProgressAsync("dism.exe", arguments, progress, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new Exception($"DISM failed with exit code: {exitCode}");
        }
    }

    private Task<bool> DeleteWithRetryAsync(string path, CancellationToken cancellationToken) =>
        RetryAsync($"delete {path}", () =>
        {
            if (!_fileSystemService.FileExists(path))
                return;
            _fileSystemService.SetFileAttributes(path, FileAttributes.Normal);
            _fileSystemService.DeleteFile(path);
            _logService.LogInformation($"Deleted {path}");
        }, cancellationToken);

    // DISM can hold an image open for a few seconds after it exits.
    private async Task<bool> RetryAsync(string what, Action action, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Attempt {attempt}/5 to {what} failed: {ex.Message}");
                if (attempt < 5)
                {
                    await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return false;
    }

    private void DeleteIncompleteFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !_fileSystemService.FileExists(path))
            return;

        try
        {
            _fileSystemService.DeleteFile(path);
            _logService.LogInformation($"Deleted incomplete file: {path}");
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not delete incomplete file {path}: {ex.Message}");
        }
    }

    private async Task<ImageFormatInfo?> GetImageInfoAsync(string imagePath, ImageFormat format)
    {
        IReadOnlyList<DismImageEntry> entries;

        try
        {
            entries = await Task.Run(() => _dismImageInfoReader.GetImageInfo(imagePath)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Could not read image info for {imagePath}: {ex.Message}", ex);
            return null;
        }

        if (entries.Count == 0)
        {
            _logService.LogWarning($"DISM reported no images inside {imagePath}");
            return null;
        }

        long fileSizeBytes = _fileSystemService.GetFileSize(imagePath);
        _logService.LogInformation($"Image: {format}, {entries.Count} editions, {fileSizeBytes:N0} bytes");

        return new ImageFormatInfo
        {
            Format = format,
            FilePath = imagePath,
            FileSizeBytes = fileSizeBytes,
            Editions = entries.Select(e => new ImageEdition(e.Index, e.Name, e.EditionId)).ToList(),
            Build = entries[0].Build,
            Ubr = entries[0].Ubr,
            Architecture = entries[0].Architecture,
        };
    }
}

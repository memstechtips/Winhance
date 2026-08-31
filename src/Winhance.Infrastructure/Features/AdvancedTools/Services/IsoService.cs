using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal class IsoService : IIsoService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localization;
    private readonly IProcessExecutor _processExecutor;
    private readonly IDismProcessRunner _dismProcessRunner;
    private readonly IIsoImageReader _isoImageReader;
    private readonly IIsoImageWriter _isoImageWriter;
    private readonly IMediaCopier _mediaCopier;

    public IsoService(
        IFileSystemService fileSystemService,
        ILogService logService,
        ILocalizationService localization,
        IProcessExecutor processExecutor,
        IDismProcessRunner dismProcessRunner,
        IIsoImageReader isoImageReader,
        IIsoImageWriter isoImageWriter,
        IMediaCopier mediaCopier)
    {
        _fileSystemService = fileSystemService;
        _logService = logService;
        _localization = localization;
        _processExecutor = processExecutor;
        _dismProcessRunner = dismProcessRunner;
        _isoImageReader = isoImageReader;
        _isoImageWriter = isoImageWriter;
        _mediaCopier = mediaCopier;
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

        try
        {
            var fileSize = _fileSystemService.GetFileSize(isoPath);
            if (fileSize < 1024 * 1024)
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
                            "Please close it or delete it manually and try again."
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

            _logService.LogInformation($"Attaching ISO: {isoPath}");

            // The attachment lives and dies with this using block, so a cancellation or a crash
            // mid-copy cannot leave the image attached for the user to clear by hand.
            using (var attachment = _isoImageReader.Attach(isoPath))
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_CopyingIsoContents"),
                    TerminalOutput = $"Source: {attachment.RootPath}"
                });

                await Task.Run(() => _mediaCopier.CopyTree(attachment.RootPath, workingDirectory, null, null, progress, cancellationToken), cancellationToken).ConfigureAwait(false);

                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_DismountingIso"),
                    TerminalOutput = "Cleaning up..."
                });
            }

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
            throw;
        }
        catch (InsufficientDiskSpaceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error extracting ISO: {ex.Message}", ex);

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
            if (string.IsNullOrEmpty(workingDirectory))
            {
                _logService.LogError("Working directory path is empty.");
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

            var outputDir = _fileSystemService.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !_fileSystemService.DirectoryExists(outputDir))
                _fileSystemService.CreateDirectory(outputDir);

            if (_fileSystemService.FileExists(outputPath))
            {
                _fileSystemService.DeleteFile(outputPath);
                _logService.LogInformation("Removed existing ISO file");
            }

            await Task.Run(
                () => _isoImageWriter.Write(workingDirectory, outputPath, progress, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (!_fileSystemService.FileExists(outputPath))
            {
                throw new InvalidOperationException($"IMAPI2 finished without producing {outputPath}.");
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

            // The ViewModel shows the message. Swallowed into false, the user got "check the logs"
            // for the ISO while the USB path next to it named the reason.
            throw;
        }
    }
}

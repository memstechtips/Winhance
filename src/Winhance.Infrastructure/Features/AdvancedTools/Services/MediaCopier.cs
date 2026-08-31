using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal interface IMediaCopier
{
    // skipFile is how the USB path leaves install.wim behind: FAT32 cannot hold it whole, so it
    // is split onto the media afterwards rather than copied. knownTotalBytes is the byte count
    // the caller already measured for the files that will be copied; null makes the copier walk
    // the tree itself, which the ISO extraction has no other reason to do.
    void CopyTree(
        string sourceDirectory,
        string destinationDirectory,
        Func<string, bool>? skipFile,
        long? knownTotalBytes,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}

internal sealed class MediaCopier : IMediaCopier
{
    private readonly IFileCopyNative _native;
    private readonly IFileSystemService _fileSystemService;
    private readonly ILocalizationService _localization;
    private readonly ILogService _logService;
    private readonly TimeProvider _timeProvider;

    public MediaCopier(
        IFileCopyNative native,
        IFileSystemService fileSystemService,
        ILocalizationService localization,
        ILogService logService,
        TimeProvider timeProvider)
    {
        _native = native;
        _fileSystemService = fileSystemService;
        _localization = localization;
        _logService = logService;
        _timeProvider = timeProvider;
    }

    public void CopyTree(
        string sourceDirectory,
        string destinationDirectory,
        Func<string, bool>? skipFile,
        long? knownTotalBytes,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        // The tree total is what turns a per-file callback into a bar that moves smoothly through
        // a 7 GB install.wim instead of standing still and jumping.
        var state = new TreeCopy
        {
            SkipFile = skipFile,
            Reporter = new ByteProgressReporter(
                progress,
                new TransferRate(_timeProvider),
                percent => _localization.GetString("Progress_CopyingIsoContentsPercent", percent.ToString())),
            TotalBytes = knownTotalBytes ?? _fileSystemService
                .GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Where(f => skipFile is null || !skipFile(f))
                .Sum(f => _fileSystemService.GetFileSize(f)),
        };

        _logService.LogInformation($"Copying {state.TotalBytes:N0} bytes from {sourceDirectory}");

        CopyInto(sourceDirectory, destinationDirectory, state, cancellationToken);
    }

    private void CopyInto(
        string sourceDirectory,
        string destinationDirectory,
        TreeCopy state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _fileSystemService.CreateDirectory(destinationDirectory);

        foreach (var file in _fileSystemService.GetFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (state.SkipFile?.Invoke(file) == true)
            {
                continue;
            }

            var fileName = _fileSystemService.GetFileName(file);
            var target = _fileSystemService.CombinePath(destinationDirectory, fileName);
            var startedAt = state.CopiedBytes;

            _native.CopyWithProgress(
                file,
                target,
                transferred => state.Reporter.Report(startedAt + transferred, state.TotalBytes, fileName),
                cancellationToken);

            state.CopiedBytes = startedAt + _fileSystemService.GetFileSize(file);
        }

        foreach (var subDirectory in _fileSystemService.GetDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = _fileSystemService.GetFileName(subDirectory);
            CopyInto(
                subDirectory,
                _fileSystemService.CombinePath(destinationDirectory, name),
                state,
                cancellationToken);
        }
    }

    private sealed class TreeCopy
    {
        internal Func<string, bool>? SkipFile { get; set; }

        internal required ByteProgressReporter Reporter { get; init; }

        internal long TotalBytes { get; set; }

        internal long CopiedBytes { get; set; }
    }
}

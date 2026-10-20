using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal interface IMediaCopier
{
    // skip sees folders as well as files, and a skipped folder is never created: it is how the
    // USB writer splits one tree across two partitions. knownTotalBytes is the byte count the
    // caller already measured; null makes the copier walk the tree itself, which the ISO
    // extraction has no other reason to do. bytesBefore lets a second pass carry on the first
    // pass's bar instead of starting a new one at zero.
    void CopyTree(
        string sourceDirectory,
        string destinationDirectory,
        Func<string, bool>? skip,
        long? knownTotalBytes,
        long bytesBefore,
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
        Func<string, bool>? skip,
        long? knownTotalBytes,
        long bytesBefore,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        // The tree total is what turns a per-file callback into a bar that moves smoothly through
        // a 7 GB install.wim instead of standing still and jumping.
        var state = new TreeCopy
        {
            Skip = skip,
            CopiedBytes = bytesBefore,
            Reporter = new ByteProgressReporter(
                progress,
                new TransferRate(_timeProvider),
                percent => _localization.GetString("Progress_CopyingIsoContentsPercent", percent.ToString())),
            TotalBytes = knownTotalBytes ?? _fileSystemService
                .GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Where(f => skip is null || !skip(f))
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

            if (state.Skip?.Invoke(file) == true)
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

            // Files on ISO media are read-only, and the working folder has to stay editable.
            _fileSystemService.SetFileAttributes(target, FileAttributes.Normal);

            state.CopiedBytes = startedAt + _fileSystemService.GetFileSize(file);
        }

        foreach (var subDirectory in _fileSystemService.GetDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (state.Skip?.Invoke(subDirectory) == true)
            {
                continue;
            }

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
        internal Func<string, bool>? Skip { get; init; }

        internal required ByteProgressReporter Reporter { get; init; }

        internal long TotalBytes { get; set; }

        internal long CopiedBytes { get; set; }
    }
}

using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal interface IMediaCopier
{
    // skipFile is how the USB path leaves install.wim behind: FAT32 cannot hold it whole, so it
    // is split onto the media afterwards rather than copied.
    void CopyTree(
        string sourceDirectory,
        string destinationDirectory,
        Func<string, bool>? skipFile,
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
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        // The tree total is measured once, up front: it is what turns a per-file callback into a
        // bar that moves smoothly through a 7 GB install.wim instead of standing still and jumping.
        var state = new TreeCopy
        {
            SkipFile = skipFile,
            Rate = new TransferRate(_timeProvider),
            TotalBytes = _fileSystemService
                .GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Where(f => skipFile is null || !skipFile(f))
                .Sum(f => _fileSystemService.GetFileSize(f)),
        };

        _logService.LogInformation($"Copying {state.TotalBytes:N0} bytes from {sourceDirectory}");

        CopyInto(sourceDirectory, destinationDirectory, state, progress, cancellationToken);
    }

    private void CopyInto(
        string sourceDirectory,
        string destinationDirectory,
        TreeCopy state,
        IProgress<TaskProgressDetail>? progress,
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
                (transferred, _) => Report(state, startedAt + transferred, fileName, progress),
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
                progress,
                cancellationToken);
        }
    }

    private void Report(TreeCopy state, long done, string fileName, IProgress<TaskProgressDetail>? progress)
    {
        // CopyFileEx fires its routine every chunk, which on a multi-GB tree is thousands of calls;
        // only a whole-percent change or a fresh speed sample is worth pushing at the UI.
        var percent = state.TotalBytes <= 0 ? 0 : (int)Math.Min(100, done * 100 / state.TotalBytes);
        var freshRate = state.Rate.Update(done);
        if (percent == state.LastReportedPercent && !freshRate)
        {
            return;
        }

        state.LastReportedPercent = percent;

        var rate = state.Rate.ToString();
        progress?.Report(new TaskProgressDetail
        {
            Progress = percent,
            StatusText = _localization.GetString("Progress_CopyingIsoContentsPercent", percent.ToString()),
            TerminalOutput = rate.Length == 0 ? fileName : $"{fileName} ({rate})",
            IsProgressIndicator = true
        });
    }

    private sealed class TreeCopy
    {
        internal Func<string, bool>? SkipFile { get; set; }

        internal required TransferRate Rate { get; init; }

        internal long TotalBytes { get; set; }

        internal long CopiedBytes { get; set; }

        internal int LastReportedPercent { get; set; } = -1;
    }
}

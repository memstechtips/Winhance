using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class StorageApiUsbMediaWriter : IUsbMediaWriter
{
    private const string UsbBusType = "USB";
    private const string VolumeLabel = "WINHANCE";

    private readonly IStorageOperations _operations;
    private readonly IMediaCopier _mediaCopier;
    private readonly IDismProcessRunner _dismProcessRunner;
    private readonly IFileSystemService _fileSystemService;
    private readonly ILocalizationService _localization;
    private readonly ILogService _logService;
    private readonly TimeProvider _timeProvider;

    public StorageApiUsbMediaWriter(
        IStorageOperations operations,
        IMediaCopier mediaCopier,
        IDismProcessRunner dismProcessRunner,
        IFileSystemService fileSystemService,
        ILocalizationService localization,
        ILogService logService,
        TimeProvider timeProvider)
    {
        _operations = operations;
        _mediaCopier = mediaCopier;
        _dismProcessRunner = dismProcessRunner;
        _fileSystemService = fileSystemService;
        _localization = localization;
        _logService = logService;
        _timeProvider = timeProvider;
    }

    public IReadOnlyList<RemovableDrive> GetCandidateTargets()
    {
        var disks = _operations.GetDisks();

        // Logged in full, because the only question users ask about this list is why their drive
        // is not on it, and the two reasons it can be missing are both here.
        foreach (var disk in disks)
        {
            _logService.LogInformation(
                $"Disk {disk.DiskNumber}: {disk.Model}, {disk.BusType}, system={disk.IsSystemDisk}");
        }

        return disks
            .Where(disk => !disk.IsSystemDisk
                        && disk.BusType.Equals(UsbBusType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(disk => disk.DiskNumber)
            .ToArray();
    }

    public void Write(
        RemovableDrive target,
        string workingDirectory,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        // The system disk is refused here as well as in the picker: the picker is a UI list a
        // caller can bypass, and MSFT_Disk's own 42010 arrives only after the wipe has started.
        if (!GetCandidateTargets().Any(candidate => candidate.DiskNumber == target.DiskNumber))
        {
            throw new InvalidOperationException(
                $"Disk {target.DiskNumber} ({target.Model}) is not a removable USB drive.");
        }

        // A working folder on the target would be wiped before it is copied. The drive's letters
        // are still readable here and gone once Clear has run, so this is the moment to look.
        var sourceRoot = _fileSystemService.GetPathRoot(workingDirectory);
        if (!string.IsNullOrEmpty(sourceRoot)
            && _operations.GetDriveLetters(target.DiskNumber)
                .Any(letter => char.ToUpperInvariant(letter) == char.ToUpperInvariant(sourceRoot[0])))
        {
            throw new InvalidOperationException(_localization.GetStringOrDefault(
                "WIMUtil_Err_UsbTargetHoldsSource",
                $"{target.Model} holds the working folder Winhance is about to copy from. "
                + "Move the working folder to another drive first.",
                target.Model));
        }

        // One pass over the tree: the layout, the split decision and the copier's total all come
        // from these sizes, so nothing walks the working folder twice.
        var sizes = _fileSystemService.GetFiles(workingDirectory, "*", SearchOption.AllDirectories)
            .ToDictionary(file => file, _fileSystemService.GetFileSize, StringComparer.OrdinalIgnoreCase);
        var totalBytes = sizes.Values.Sum();
        var largestFile = sizes.Count == 0 ? 0 : sizes.Values.Max();
        var layout = UsbWriteLayoutPlanner.Plan(totalBytes, largestFile);
        var payloadGigabytes = (layout.TotalPayloadBytes / (1024.0 * 1024 * 1024)).ToString("F1");

        // Everything that can refuse the write happens before anything is erased.
        if (layout.ExceedsFat32Ceiling)
        {
            throw new InvalidOperationException(_localization.GetStringOrDefault(
                "WIMUtil_Err_UsbPayloadTooLarge",
                $"This image is {payloadGigabytes} GB. A FAT32 drive can only be formatted to 32 GB, "
                + "so this image would need a second USB drive and Winhance cannot write it to one.",
                payloadGigabytes));
        }

        if (target.SizeBytes < layout.TotalPayloadBytes)
        {
            throw new InvalidOperationException(_localization.GetStringOrDefault(
                "WIMUtil_Err_UsbDriveTooSmall",
                $"{target.Model} holds {target.SizeGigabytes:F1} GB but the media needs {payloadGigabytes} GB.",
                target.Model,
                target.SizeGigabytes.ToString("F1"),
                payloadGigabytes));
        }

        var imageToSplit = layout.RequiresSplit ? FindImageToSplit(workingDirectory, sizes) : null;

        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new TaskProgressDetail
        {
            StatusText = _localization.GetString("Progress_FormattingUsb"),
            TerminalOutput = $"{target.Model} ({target.SizeGigabytes:F1} GB)",
            IsIndeterminate = true
        });

        // Format, mark active, copy - Microsoft's documented procedure, and no bcdboot: the media
        // already ships \EFI\BOOT\BOOTX64.EFI and \boot\bcd, and copying them verbatim is what
        // makes the stick boot.
        _operations.Clear(target.DiskNumber);

        // From here on the drive is blank, and whatever stops the write has to say so: a bare
        // failure reads as "nothing happened", and the user unplugs an empty stick.
        try
        {
            _operations.EnsureMbr(target.DiskNumber);
            var partitionNumber = _operations.CreateActiveFat32Partition(target.DiskNumber);
            _operations.FormatFat32(target.DiskNumber, partitionNumber, VolumeLabel);
            var driveLetter = _operations.AssignDriveLetter(target.DiskNumber, partitionNumber);

            var mediaRoot = $"{driveLetter}:\\";
            _logService.LogInformation($"Writing {layout.TotalPayloadBytes:N0} bytes to {mediaRoot}");

            _mediaCopier.CopyTree(
                workingDirectory,
                mediaRoot,
                imageToSplit is null ? null : path => string.Equals(path, imageToSplit, StringComparison.OrdinalIgnoreCase),
                imageToSplit is not null && sizes.TryGetValue(imageToSplit, out var wimBytes) ? totalBytes - wimBytes : totalBytes,
                progress,
                cancellationToken);

            if (imageToSplit is not null)
            {
                SplitImageOntoMedia(imageToSplit, mediaRoot, progress, cancellationToken);
            }
        }
        catch (OperationCanceledException ex)
        {
            throw new UsbMediaErasedException(target, wasCancelled: true, ex);
        }
        catch (Exception ex)
        {
            throw new UsbMediaErasedException(target, wasCancelled: false, ex);
        }
    }

    // dism /Split-Image splits sources\install.wim and nothing else, so any other file past FAT32's
    // limit - an ESD, or a stale one left beside a converted WIM - has to be dealt with first, and
    // before the drive is touched.
    private string FindImageToSplit(string workingDirectory, IReadOnlyDictionary<string, long> sizes)
    {
        var wimPath = _fileSystemService.CombinePath(workingDirectory, "sources", "install.wim");
        var unsplittable = sizes.FirstOrDefault(entry =>
            entry.Value >= UsbWriteLayoutPlanner.Fat32MaxFileBytes
            && !string.Equals(entry.Key, wimPath, StringComparison.OrdinalIgnoreCase)).Key;

        if (unsplittable is not null)
        {
            var fileName = _fileSystemService.GetFileName(unsplittable);
            throw new InvalidOperationException(_localization.GetStringOrDefault(
                "WIMUtil_Err_UsbCannotSplit",
                $"{fileName} is larger than 4 GB, which FAT32 cannot hold, and Winhance can only split "
                + "sources\\install.wim. Convert an ESD image to WIM format in the previous step, or remove "
                + "the file from the working folder, then write the USB drive.",
                fileName));
        }

        return wimPath;
    }

    private void SplitImageOntoMedia(
        string imagePath,
        string mediaRoot,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var target = UsbWriteLayoutPlanner.SplitTargetPath(mediaRoot);

        progress?.Report(new TaskProgressDetail
        {
            StatusText = _localization.GetString("Progress_SplittingImage"),
            TerminalOutput = target,
            IsIndeterminate = true
        });

        // dism.exe, not the DISM API: there is no DismSplitImage, and WIMGAPI's WIMSplitFile ships
        // in the ADK rather than the SDK. This is also literally the command in Microsoft's own USB
        // instructions, so it is the documented method rather than a fallback from one.
        var arguments =
            $"/Split-Image /ImageFile:\"{imagePath}\" /SWMFile:\"{target}\" /FileSize:{UsbWriteLayoutPlanner.SplitSizeMb}";

        var splitProgress = progress is null
            ? null
            : new SplitProgress(progress, _fileSystemService.GetFileSize(imagePath), new TransferRate(_timeProvider));

        // Blocking on the runner is safe here and nowhere else: Write is a synchronous contract
        // (formatting a disk has no async half) and its only caller already runs it on a pool
        // thread, so there is no synchronization context to deadlock against.
        var (exitCode, _) = _dismProcessRunner
            .RunProcessWithProgressAsync("dism.exe", arguments, splitProgress, cancellationToken)
            .GetAwaiter()
            .GetResult();

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Splitting the Windows image failed with exit code {exitCode}.");
        }

        _logService.LogInformation($"Split {imagePath} into {target} at {UsbWriteLayoutPlanner.SplitSizeMb} MB per piece");
    }

    // DISM prints only its own bar, and the bytes behind each percent are the image size, so the
    // write speed can be read off it.
    private sealed class SplitProgress(IProgress<TaskProgressDetail> inner, long imageBytes, TransferRate rate)
        : IProgress<TaskProgressDetail>
    {
        public void Report(TaskProgressDetail detail)
        {
            if (detail.Progress is { } percent)
            {
                rate.Update((long)(imageBytes * percent / 100));
                var speed = rate.ToString();
                if (speed.Length > 0)
                {
                    detail.TerminalOutput = $"{detail.TerminalOutput} {speed}";
                }
            }

            inner.Report(detail);
        }
    }
}

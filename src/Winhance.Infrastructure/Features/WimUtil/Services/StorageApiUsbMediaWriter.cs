using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class StorageApiUsbMediaWriter : IUsbMediaWriter
{
    private const string UsbBusType = "USB";
    private const string BootLabel = "WINBOOT";
    private const string InstallLabel = "WINSETUP";

    private readonly IStorageOperations _operations;
    private readonly IMediaCopier _mediaCopier;
    private readonly IFileSystemService _fileSystemService;
    private readonly ILocalizationService _localization;
    private readonly ILogService _logService;

    public StorageApiUsbMediaWriter(
        IStorageOperations operations,
        IMediaCopier mediaCopier,
        IFileSystemService fileSystemService,
        ILocalizationService localization,
        ILogService logService)
    {
        _operations = operations;
        _mediaCopier = mediaCopier;
        _fileSystemService = fileSystemService;
        _localization = localization;
        _logService = logService;
    }

    public IReadOnlyList<RemovableDrive> GetCandidateTargets()
    {
        var disks = _operations.GetDisks();

        // Logged in full, because the only question users ask about this list is why their drive
        // is not on it, and the three reasons it can be missing are all here.
        foreach (var disk in disks)
        {
            _logService.LogInformation(
                $"Disk {disk.DiskNumber}: {disk.Model}, {disk.BusType}, system={disk.IsSystemDisk}, removable={disk.HasRemovableMedia}");
        }

        // USB hard drives and SSDs stay off the list, as Rufus keeps them off by default: they are
        // usually someone's backup drive, and one wrong pick erases it.
        return disks
            .Where(disk => !disk.IsSystemDisk
                        && disk.HasRemovableMedia
                        && disk.BusType.Equals(UsbBusType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(disk => disk.DiskNumber)
            .ToArray();
    }

    public void Write(
        RemovableDrive target,
        string workingDirectory,
        BootFileReplacements replacements,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        // The system disk is refused here as well as in the picker: the picker is a UI list a
        // caller can bypass, and MSFT_Disk's own 42010 arrives only after the wipe has started.
        // The whole record is compared because Windows reuses disk numbers: a drive plugged in
        // after the picked one was pulled can hold its number.
        if (!GetCandidateTargets().Contains(target))
        {
            throw new InvalidOperationException(
                $"Disk {target.DiskNumber} ({target.Model}) is not a USB flash drive.");
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

        // One pass over the tree: the layout and the copier's total both come from these sizes,
        // so nothing walks the working folder twice.
        var sizes = _fileSystemService.GetFiles(workingDirectory, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(workingDirectory, file), _fileSystemService.GetFileSize, StringComparer.OrdinalIgnoreCase);
        var layout = UsbWriteLayoutPlanner.Plan(sizes);

        // Everything that can refuse the write happens before anything is erased.
        if (layout.BootBytes > UsbWriteLayoutPlanner.BootPartitionBytes)
        {
            throw new InvalidOperationException(_localization.GetStringOrDefault(
                "WIMUtil_Err_UsbBootFilesTooLarge",
                $"The files the PC starts from (boot.wim and the boot folders) come to {Gigabytes(layout.BootBytes)} GB, "
                + $"more than the {Gigabytes(UsbWriteLayoutPlanner.BootPartitionBytes)} GB boot partition holds. "
                + "Start again from an unmodified Windows ISO.",
                Gigabytes(layout.BootBytes),
                Gigabytes(UsbWriteLayoutPlanner.BootPartitionBytes)));
        }

        var neededBytes = UsbWriteLayoutPlanner.BootPartitionBytes + layout.InstallBytes;
        if (target.SizeBytes < neededBytes)
        {
            throw new InvalidOperationException(_localization.GetStringOrDefault(
                "WIMUtil_Err_UsbDriveTooSmall",
                $"{target.Model} holds {target.SizeGigabytes:F1} GB but the media needs {Gigabytes(neededBytes)} GB.",
                target.Model,
                target.SizeGigabytes.ToString("F1"),
                Gigabytes(neededBytes)));
        }

        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new TaskProgressDetail
        {
            StatusText = _localization.GetString("Progress_FormattingUsb"),
            TerminalOutput = $"{target.Model} ({target.SizeGigabytes:F1} GB)",
            IsIndeterminate = true
        });

        // From Clear on the drive is blank, and whatever stops the write has to say so: a bare
        // failure reads as "nothing happened", and the user unplugs an empty stick. Clear is
        // inside because it wipes first and can still fail on the steps that follow the wipe.
        try
        {
            // No bcdboot: the media already ships \EFI\BOOT\BOOTX64.EFI and \boot\bcd, and copying
            // them verbatim is what makes the stick boot.
            _operations.Clear(target.DiskNumber);
            _operations.EnsureMbr(target.DiskNumber);
            var bootPartition = _operations.CreateBootPartition(target.DiskNumber, UsbWriteLayoutPlanner.BootPartitionBytes);
            var installPartition = _operations.CreateInstallPartition(target.DiskNumber);
            _operations.Format(target.DiskNumber, bootPartition, "FAT32", BootLabel);
            _operations.Format(target.DiskNumber, installPartition, "NTFS", InstallLabel);
            var bootRoot = $"{_operations.AssignDriveLetter(target.DiskNumber, bootPartition)}:\\";
            var installRoot = $"{_operations.AssignDriveLetter(target.DiskNumber, installPartition)}:\\";

            var totalBytes = layout.BootBytes + layout.InstallBytes;
            _logService.LogInformation($"Writing {layout.BootBytes:N0} bytes to {bootRoot} and {layout.InstallBytes:N0} to {installRoot}");

            _mediaCopier.CopyTree(
                workingDirectory,
                bootRoot,
                path => !UsbWriteLayoutPlanner.IsOnBootPartition(Path.GetRelativePath(workingDirectory, path)),
                totalBytes,
                0,
                progress,
                cancellationToken);
            _mediaCopier.CopyTree(
                workingDirectory,
                installRoot,
                path => UsbWriteLayoutPlanner.IsLeftOffInstallPartition(Path.GetRelativePath(workingDirectory, path)),
                totalBytes,
                layout.BootBytes,
                progress,
                cancellationToken);

            foreach (var (relativePath, replacement) in replacements.Files)
            {
                if (UsbWriteLayoutPlanner.IsOnBootPartition(relativePath))
                    _fileSystemService.CopyFile(replacement, Path.Combine(bootRoot, relativePath), overwrite: true);

                if (!UsbWriteLayoutPlanner.IsLeftOffInstallPartition(relativePath))
                    _fileSystemService.CopyFile(replacement, Path.Combine(installRoot, relativePath), overwrite: true);
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

    private static string Gigabytes(long bytes) => (bytes / (1024.0 * 1024 * 1024)).ToString("F1");
}

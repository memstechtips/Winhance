using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class OfflineServicingService(
    IDismProcessRunner dism,
    IFileSystemService files,
    ILocalizationService localization,
    ILogService log,
    IDriverInstallStepWriter driverStep,
    IUpdateInstallStepWriter updateStep,
    IWindowsUpdateStager updateStager,
    IDismImageInfoReader images) : IOfflineServicingService
{
    // Outside the working directory, so writing the ISO or USB never picks the mounted image up.
    internal static string MountDirFor(string workingDirectory, IFileSystemService files)
    {
        var parent = files.GetDirectoryName(workingDirectory.TrimEnd('\\'));
        return files.CombinePath(string.IsNullOrEmpty(parent) ? files.GetTempPath() : parent, "WinhanceMount");
    }

    public bool HasStagedItems(string workingDirectory) =>
        DriverStaging.IsStaged(files, workingDirectory) || UpdateStaging.IsStaged(files, workingDirectory);

    // DISM refuses a mount folder off a fixed drive ("mount to a volume on a fixed drive", for a share and a mapped
    // letter alike) and on FAT32 ("does not support reparse points"), measured on 26H2. A root it cannot read gets
    // the benefit of the doubt: the mount itself still falls back to setup if it fails.
    public bool CanMountFor(string workingDirectory)
    {
        var root = files.GetPathRoot(MountDirFor(workingDirectory, files));
        if (string.IsNullOrEmpty(root))
        {
            return true;
        }
        if (root.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var drive = new DriveInfo(root);
            return drive.DriveType == DriveType.Fixed && string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            log.LogWarning($"Could not read the file system of {root}: {ex.Message}");
            return true;
        }
    }

    public async Task<OfflineServicingResult> ServiceAsync(
        string workingDirectory,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var wim = files.CombinePath(workingDirectory, "sources", "install.wim");
        if (!files.FileExists(wim))
        {
            throw new InvalidOperationException(localization.GetString("WIMUtil_Msg_ServicingNeedsWim"));
        }

        var hasDrivers = DriverStaging.IsStaged(files, workingDirectory);
        var target = UpdateStaging.TargetOf(files, workingDirectory);
        string[] updates = target is null ? [] : UpdateStaging.PackagesOf(files, workingDirectory);
        if (!hasDrivers && updates.Length == 0)
        {
            return new OfflineServicingResult(ServicedItemOutcome.NotStaged, ServicedItemOutcome.NotStaged);
        }

        if (!CanMountFor(workingDirectory))
        {
            log.LogWarning($"{workingDirectory} is not on a local NTFS drive, so the image cannot be mounted beside it; everything installs during setup");
            return await FallBackToSetupAsync(workingDirectory, hasDrivers, updates.Length > 0).ConfigureAwait(false);
        }

        if (!await HoldsOneImageAsync(wim).ConfigureAwait(false))
        {
            return await FallBackToSetupAsync(workingDirectory, hasDrivers, updates.Length > 0).ConfigureAwait(false);
        }

        var mountDir = MountDirFor(workingDirectory, files);
        var requiredBytes = (long)(files.GetFileSize(wim) * 2.5) + updates.Sum(f => files.GetFileSize(f)) * 2;
        try
        {
            await dism.CheckDiskSpaceAsync(mountDir, requiredBytes, "Offline servicing").ConfigureAwait(false);
        }
        catch (InsufficientDiskSpaceException ex)
        {
            log.LogWarning($"No room to mount the image, everything installs during setup: {ex.Message}");
            return await FallBackToSetupAsync(workingDirectory, hasDrivers, updates.Length > 0).ConfigureAwait(false);
        }

        var drivers = hasDrivers ? ServicedItemOutcome.FellBackToSetup : ServicedItemOutcome.NotStaged;
        var update = updates.Length > 0 ? ServicedItemOutcome.FellBackToSetup : ServicedItemOutcome.NotStaged;
        try
        {
            // A mount left by a crashed run blocks a new one on the same folder. With no folder there is no mount, and DISM would log exit 50 as a failure.
            if (files.DirectoryExists(mountDir))
            {
                await RunDismAsync($"/Unmount-Image /MountDir:\"{mountDir}\" /Discard", null, cancellationToken).ConfigureAwait(false);
            }
            await RunDismAsync("/Cleanup-Mountpoints", null, cancellationToken).ConfigureAwait(false);
            DeleteIfEmpty(mountDir);
            if (files.DirectoryExists(mountDir))
            {
                log.LogWarning($"{mountDir} still holds files after the cleanup, so the image cannot be mounted there");
                return await FallBackToSetupAsync(workingDirectory, hasDrivers, updates.Length > 0).ConfigureAwait(false);
            }

            files.CreateDirectory(mountDir);

            // Files copied off ISO media are read-only, and a read-write mount refuses a read-only image.
            files.SetFileAttributes(wim, FileAttributes.Normal);

            Report(progress, "WIMUtil_Progress_Mounting");
            if (await RunDismAsync($"/Mount-Image /ImageFile:\"{wim}\" /Index:1 /MountDir:\"{mountDir}\"", progress, cancellationToken).ConfigureAwait(false) != 0)
            {
                return await DiscardAsync(workingDirectory, mountDir, hasDrivers, updates.Length > 0).ConfigureAwait(false);
            }

            if (hasDrivers)
            {
                Report(progress, "WIMUtil_Progress_AddingDrivers");
                var oem = DriverStaging.FolderOf(files, workingDirectory);
                var exit = await RunDismAsync($"/Image:\"{mountDir}\" /Add-Driver /Driver:\"{oem}\" /Recurse", progress, cancellationToken).ConfigureAwait(false);
                drivers = exit == 0 ? ServicedItemOutcome.Added : ServicedItemOutcome.FellBackToSetup;
            }

            if (updates.Length > 0)
            {
                Report(progress, "WIMUtil_Progress_AddingUpdate");
                var exit = await RunDismAsync($"/Image:\"{mountDir}\" /Add-Package /PackagePath:\"{target}\"", progress, cancellationToken).ConfigureAwait(false);

                // One commit must not carry half a package, so a failed update takes the drivers back out with it.
                if (exit is not (0 or 3010))
                {
                    return await DiscardAsync(workingDirectory, mountDir, hasDrivers, true).ConfigureAwait(false);
                }
                update = ServicedItemOutcome.Added;
            }

            if (drivers != ServicedItemOutcome.Added && update != ServicedItemOutcome.Added)
            {
                return await DiscardAsync(workingDirectory, mountDir, hasDrivers, false).ConfigureAwait(false);
            }

            Report(progress, "WIMUtil_Progress_CleaningUp");
            var cleanup = await RunDismAsync($"/Image:\"{mountDir}\" /Cleanup-Image /StartComponentCleanup", progress, cancellationToken).ConfigureAwait(false);
            if (cleanup != 0)
            {
                log.LogWarning($"Component cleanup exited with {cleanup}; the image is saved without it");
            }

            Report(progress, "WIMUtil_Progress_Saving");

            // Cancelling kills dism.exe, and a commit killed halfway leaves install.wim half-written.
            cancellationToken.ThrowIfCancellationRequested();
            if (await RunDismAsync($"/Unmount-Image /MountDir:\"{mountDir}\" /Commit", progress, CancellationToken.None).ConfigureAwait(false) != 0)
            {
                return await DiscardAsync(workingDirectory, mountDir, hasDrivers, updates.Length > 0).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            await UnmountDiscardingAsync(mountDir).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            log.LogError($"Offline servicing failed: {ex.Message}", ex);
            return await DiscardAsync(workingDirectory, mountDir, hasDrivers, updates.Length > 0).ConfigureAwait(false);
        }

        DeleteIfEmpty(mountDir);
        await TidyAsync(workingDirectory, drivers, update).ConfigureAwait(false);
        log.LogInformation($"Serviced {wim}: drivers {drivers}, update {update}");
        return new OfflineServicingResult(drivers, update);
    }

    private async Task<OfflineServicingResult> DiscardAsync(string workingDirectory, string mountDir, bool hasDrivers, bool hasUpdate)
    {
        await UnmountDiscardingAsync(mountDir).ConfigureAwait(false);
        return await FallBackToSetupAsync(workingDirectory, hasDrivers, hasUpdate).ConfigureAwait(false);
    }

    private async Task<OfflineServicingResult> FallBackToSetupAsync(string workingDirectory, bool hasDrivers, bool hasUpdate)
    {
        var result = new OfflineServicingResult(
            hasDrivers ? ServicedItemOutcome.FellBackToSetup : ServicedItemOutcome.NotStaged,
            hasUpdate ? ServicedItemOutcome.FellBackToSetup : ServicedItemOutcome.NotStaged);
        await TidyAsync(workingDirectory, result.Drivers, result.Update).ConfigureAwait(false);
        log.LogWarning($"Nothing was built into the image; drivers {result.Drivers}, update {result.Update}");
        return result;
    }

    private async Task UnmountDiscardingAsync(string mountDir)
    {
        try
        {
            await RunDismAsync($"/Unmount-Image /MountDir:\"{mountDir}\" /Discard", null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.LogWarning($"Could not discard the mounted image at {mountDir}: {ex.Message}");
        }

        DeleteIfEmpty(mountDir);
    }

    // No cancellation: the image is already committed or discarded, and the answer file has to match it.
    private async Task TidyAsync(string workingDirectory, ServicedItemOutcome drivers, ServicedItemOutcome update)
    {
        try
        {
            // The step goes first: a step left pointing at deleted drivers fails Setup, while drivers left behind only reinstall.
            if (drivers == ServicedItemOutcome.Added)
            {
                await driverStep.RemoveAsync(workingDirectory, CancellationToken.None).ConfigureAwait(false);
                DeleteStagedDrivers(workingDirectory);
            }
            else if (drivers == ServicedItemOutcome.FellBackToSetup)
            {
                await driverStep.EnsureAsync(workingDirectory, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning($"Could not tidy the staged drivers after servicing: {ex.Message}");
        }

        try
        {
            if (update == ServicedItemOutcome.Added)
            {
                updateStager.RemoveStaged(workingDirectory);
                await updateStep.RemoveAsync(workingDirectory, CancellationToken.None).ConfigureAwait(false);
            }
            else if (update == ServicedItemOutcome.FellBackToSetup)
            {
                await updateStep.EnsureAsync(workingDirectory, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning($"Could not update the update install step after servicing: {ex.Message}");
        }

        if (drivers == ServicedItemOutcome.Added || update == ServicedItemOutcome.Added)
        {
            PruneEmptyOemFolders(workingDirectory);
        }
    }

    // A copied driver file keeps its source's read-only bit, and a recursive delete refuses a read-only file.
    private void DeleteStagedDrivers(string workingDirectory)
    {
        var folder = DriverStaging.FolderOf(files, workingDirectory);
        foreach (var file in files.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            files.SetFileAttributes(file, FileAttributes.Normal);
        }
        files.DeleteDirectory(folder, true);
    }

    // /Index:1 is the only edition serviced, so an image whose other editions the UI missed would ship them without
    // the drivers and update while their setup-time steps are removed.
    private async Task<bool> HoldsOneImageAsync(string wim)
    {
        try
        {
            var count = (await Task.Run(() => images.GetImageInfo(wim)).ConfigureAwait(false)).Count;
            if (count == 1)
            {
                return true;
            }
            log.LogWarning($"{wim} holds {count} images, and servicing handles only a single-image file; everything installs during setup");
        }
        catch (Exception ex)
        {
            log.LogWarning($"Could not count the images in {wim}, so everything installs during setup: {ex.Message}");
        }
        return false;
    }

    // Deepest first, and only the empty ones: staged apps and the installer share $OEM$\$1\ProgramData\Winhance\Unattend.
    private void PruneEmptyOemFolders(string workingDirectory)
    {
        var oem = files.CombinePath(workingDirectory, "sources", "$OEM$");
        var programData = files.CombinePath(oem, "$1", "ProgramData");
        foreach (var folder in new[]
        {
            files.CombinePath(oem, "$$"),
            files.CombinePath(programData, "Winhance", "Unattend"),
            files.CombinePath(programData, "Winhance"),
            programData,
            files.CombinePath(oem, "$1"),
            oem,
        })
        {
            DeleteIfEmpty(folder);
        }
    }

    // Only an empty folder goes: a failed discard leaves the image mounted there, and its files are not ours to delete.
    private void DeleteIfEmpty(string folder)
    {
        try
        {
            if (files.DirectoryExists(folder) && files.GetFiles(folder).Length == 0 && files.GetDirectories(folder).Length == 0)
            {
                files.DeleteDirectory(folder);
            }
        }
        catch (Exception ex)
        {
            log.LogWarning($"Could not delete {folder}: {ex.Message}");
        }
    }

    private async Task<int> RunDismAsync(string arguments, IProgress<TaskProgressDetail>? progress, CancellationToken cancellationToken)
    {
        log.LogInformation($"dism.exe {arguments}");
        var (exitCode, output) = await dism.RunProcessWithProgressAsync("dism.exe", arguments, progress, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            log.LogWarning($"dism.exe exited with {exitCode}: {output[^Math.Min(output.Length, 500)..]}");
        }
        return exitCode;
    }

    private void Report(IProgress<TaskProgressDetail>? progress, string key) =>
        progress?.Report(new TaskProgressDetail { StatusText = localization.GetString(key) });
}

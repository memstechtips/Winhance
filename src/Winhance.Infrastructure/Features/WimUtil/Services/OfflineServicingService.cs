using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class OfflineServicingService(
    IDismProcessRunner dism,
    IFileSystemService files,
    ILocalizationService localization,
    ILogService log,
    IDriverInstallStepWriter driverStep,
    IUpdateInstallStepWriter updateStep,
    IWindowsUpdateStager updateStager,
    IWimImageService wimImages,
    IOfflineAppRemover appRemover,
    IOfflineSettingsApplier settingsApplier) : IOfflineServicingService
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

    public IReadOnlyList<ServicingPhase> PhasesFor(OfflineBuildRequest request)
    {
        var work = Inspect(request.WorkingDirectory);
        return PlanFor(request, work with
        {
            HasDrivers = work.HasDrivers || request.StagesDrivers,
            HasUpdate = work.HasUpdate || request.StagesUpdate,
        });
    }

    public async Task<OfflineServicingResult> ServiceAsync(
        OfflineBuildRequest request,
        IProgress<TaskProgressDetail>? progress,
        IProgress<ServicingPhaseUpdate>? phaseUpdates,
        CancellationToken cancellationToken)
    {
        var workingDirectory = request.WorkingDirectory;
        if (!files.FileExists(files.CombinePath(workingDirectory, "sources", "install.wim"))
            && !files.FileExists(files.CombinePath(workingDirectory, "sources", "install.esd")))
        {
            throw new InvalidOperationException(localization.GetString("WIMUtil_Msg_ServicingNeedsWim"));
        }

        var work = Inspect(workingDirectory);
        var phases = new PhaseTracker(PlanFor(request, work), phaseUpdates);
        var result = await BuildAsync(request, work, phases, progress, cancellationToken).ConfigureAwait(false);
        RemoveDotNetSource(request, phases, progress);
        return result with { Phases = phases.Snapshot() };
    }

    private async Task<OfflineServicingResult> BuildAsync(
        OfflineBuildRequest request,
        StagedWork work,
        PhaseTracker phases,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var workingDirectory = request.WorkingDirectory;
        var wim = files.CombinePath(workingDirectory, "sources", "install.wim");
        var hasUpdate = work.Updates.Length > 0;
        if (!NeedsMount(request, work))
        {
            return new OfflineServicingResult(ServicedItemOutcome.NotStaged, ServicedItemOutcome.NotStaged);
        }

        if (!CanMountFor(workingDirectory))
        {
            log.LogWarning($"{workingDirectory} is not on a local NTFS drive, so the image cannot be mounted beside it; everything installs during setup");
            return await FallBackToSetupAsync(workingDirectory, work.HasDrivers, hasUpdate, phases).ConfigureAwait(false);
        }

        try
        {
            if (!await PrepareImageAsync(request, phases, progress, cancellationToken).ConfigureAwait(false))
            {
                return await FallBackToSetupAsync(workingDirectory, work.HasDrivers, hasUpdate, phases).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning($"Could not prepare the image, everything installs during setup: {ex.Message}");
            phases.Set(ServicingPhase.PrepareImage, ServicingPhaseState.Failed);
            return await FallBackToSetupAsync(workingDirectory, work.HasDrivers, hasUpdate, phases).ConfigureAwait(false);
        }

        var mountDir = MountDirFor(workingDirectory, files);

        // The final export writes a second copy of the image beside the first, hence the extra wim size.
        var requiredBytes = (long)(files.GetFileSize(wim) * 3.5) + work.Updates.Sum(f => files.GetFileSize(f)) * 2;
        try
        {
            await dism.CheckDiskSpaceAsync(mountDir, requiredBytes, "Offline servicing").ConfigureAwait(false);
        }
        catch (InsufficientDiskSpaceException ex)
        {
            log.LogWarning($"No room to mount the image, everything installs during setup: {ex.Message}");
            return await FallBackToSetupAsync(workingDirectory, work.HasDrivers, hasUpdate, phases).ConfigureAwait(false);
        }

        var drivers = work.HasDrivers ? ServicedItemOutcome.FellBackToSetup : ServicedItemOutcome.NotStaged;
        var update = hasUpdate ? ServicedItemOutcome.FellBackToSetup : ServicedItemOutcome.NotStaged;
        var appsRemoved = false;
        var appsApplied = 0;
        var settingsOk = false;
        var settingsApplied = 0;
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
                return await FallBackToSetupAsync(workingDirectory, work.HasDrivers, hasUpdate, phases).ConfigureAwait(false);
            }

            if (!files.CreatePrivateDirectory(mountDir))
            {
                log.LogWarning($"{mountDir} could not be made private to administrators, so the image cannot be mounted there");
                return await FallBackToSetupAsync(workingDirectory, work.HasDrivers, hasUpdate, phases).ConfigureAwait(false);
            }

            // Files copied off ISO media are read-only, and a read-write mount refuses a read-only image.
            files.SetFileAttributes(wim, FileAttributes.Normal);

            phases.Set(ServicingPhase.Mount, ServicingPhaseState.Running);
            Report(progress, "WIMUtil_Progress_Mounting");
            if (await RunDismAsync($"/Mount-Image /ImageFile:\"{wim}\" /Index:1 /MountDir:\"{mountDir}\"", progress, cancellationToken).ConfigureAwait(false) != 0)
            {
                phases.Set(ServicingPhase.Mount, ServicingPhaseState.Failed);
                return await DiscardAsync(workingDirectory, mountDir, work.HasDrivers, hasUpdate, phases).ConfigureAwait(false);
            }
            phases.Set(ServicingPhase.Mount, ServicingPhaseState.Done);

            if (hasUpdate)
            {
                phases.Set(ServicingPhase.Update, ServicingPhaseState.Running);
                Report(progress, "WIMUtil_Progress_AddingUpdate");
                var exit = await RunDismAsync($"/Image:\"{mountDir}\" /Add-Package /PackagePath:\"{work.Target}\"", progress, cancellationToken).ConfigureAwait(false);

                // One commit must not carry half a package, so a failed update takes everything else back out with it.
                if (!InstallerExitCode.IsSuccess(exit))
                {
                    phases.Set(ServicingPhase.Update, ServicingPhaseState.Failed);
                    return await DiscardAsync(workingDirectory, mountDir, work.HasDrivers, true, phases).ConfigureAwait(false);
                }
                update = ServicedItemOutcome.Added;
                phases.Set(ServicingPhase.Update, ServicingPhaseState.Done);
            }

            if (work.HasDrivers)
            {
                phases.Set(ServicingPhase.Drivers, ServicingPhaseState.Running);
                Report(progress, "WIMUtil_Progress_AddingDrivers");
                var oem = DriverStaging.FolderOf(files, workingDirectory);
                var exit = await RunDismAsync($"/Image:\"{mountDir}\" /Add-Driver /Driver:\"{oem}\" /Recurse", progress, cancellationToken).ConfigureAwait(false);
                var added = InstallerExitCode.IsSuccess(exit);
                drivers = added ? ServicedItemOutcome.Added : ServicedItemOutcome.FellBackToSetup;
                phases.Set(ServicingPhase.Drivers, added ? ServicingPhaseState.Done : ServicingPhaseState.Failed);
            }

            // A failed app or settings step leaves the answer file to do the same work during setup, so the build goes on.
            if (HasApps(request))
            {
                phases.Set(ServicingPhase.RemoveApps, ServicingPhaseState.Running);
                Report(progress, "WIMUtil_Progress_RemovingApps");
                (appsRemoved, appsApplied) = await RunStepAsync("Removing apps", () => appRemover.RemoveAsync(mountDir, request.Selections!, progress, cancellationToken)).ConfigureAwait(false);
                phases.Set(ServicingPhase.RemoveApps, appsRemoved ? ServicingPhaseState.Done : ServicingPhaseState.Failed);
            }

            if (HasSettings(request))
            {
                phases.Set(ServicingPhase.ApplySettings, ServicingPhaseState.Running);
                Report(progress, "WIMUtil_Progress_ApplyingSettings");
                (settingsOk, settingsApplied) = await RunStepAsync("Applying settings", () => settingsApplier.ApplyAsync(mountDir, request.Selections!, progress, cancellationToken)).ConfigureAwait(false);
                phases.Set(ServicingPhase.ApplySettings, settingsOk ? ServicingPhaseState.Done : ServicingPhaseState.Failed);
            }

            var changed = drivers == ServicedItemOutcome.Added || update == ServicedItemOutcome.Added
                || appsApplied > 0 || settingsApplied > 0 || request.Cleanup.Any || request.EnableDotNet35;
            if (!changed)
            {
                return await DiscardAsync(workingDirectory, mountDir, work.HasDrivers, false, phases).ConfigureAwait(false);
            }

            await CleanUpAsync(request, mountDir, phases, progress, cancellationToken).ConfigureAwait(false);

            if (request.EnableDotNet35)
            {
                await EnableDotNetAsync(request, mountDir, phases, progress, cancellationToken).ConfigureAwait(false);
            }

            phases.Set(ServicingPhase.Save, ServicingPhaseState.Running);
            Report(progress, "WIMUtil_Progress_Saving");

            // Cancelling kills dism.exe, and a commit killed halfway leaves install.wim half-written.
            cancellationToken.ThrowIfCancellationRequested();
            if (await RunDismAsync($"/Unmount-Image /MountDir:\"{mountDir}\" /Commit", progress, CancellationToken.None).ConfigureAwait(false) != 0)
            {
                phases.Set(ServicingPhase.Save, ServicingPhaseState.Failed);
                return await DiscardAsync(workingDirectory, mountDir, work.HasDrivers, hasUpdate, phases).ConfigureAwait(false);
            }
            phases.Set(ServicingPhase.Save, ServicingPhaseState.Done);
        }
        catch (OperationCanceledException)
        {
            await UnmountDiscardingAsync(mountDir).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            log.LogError($"Offline servicing failed: {ex.Message}", ex);
            return await DiscardAsync(workingDirectory, mountDir, work.HasDrivers, hasUpdate, phases).ConfigureAwait(false);
        }

        DeleteIfEmpty(mountDir);

        // The image is committed by now, so a cancel here still has to bring the answer file in line with it.
        try
        {
            await ExportImageAsync(workingDirectory, request.OutputFormat, phases, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await TidyAsync(workingDirectory, drivers, update).ConfigureAwait(false);
            throw;
        }

        await TidyAsync(workingDirectory, drivers, update).ConfigureAwait(false);
        log.LogInformation($"Serviced {wim}: drivers {drivers}, update {update}, apps removed {appsRemoved}, settings applied {settingsOk}");
        return new OfflineServicingResult(drivers, update);
    }

    private StagedWork Inspect(string workingDirectory)
    {
        var target = UpdateStaging.TargetOf(files, workingDirectory);
        return new StagedWork(
            DriverStaging.IsStaged(files, workingDirectory),
            target,
            target is null ? [] : UpdateStaging.PackagesOf(files, workingDirectory));
    }

    private static bool HasApps(OfflineBuildRequest request) => request.Selections?.WindowsApps.Count > 0;

    private static bool HasSettings(OfflineBuildRequest request) => request.Selections?.Settings.Count > 0;

    private static bool NeedsMount(OfflineBuildRequest request, StagedWork work) =>
        work.HasDrivers || work.HasUpdate || HasApps(request) || HasSettings(request)
        || request.EnableDotNet35 || request.Cleanup.Any;

    private List<ServicingPhase> PlanFor(OfflineBuildRequest request, StagedWork work)
    {
        var plan = new List<ServicingPhase>();
        if (NeedsMount(request, work))
        {
            // A multi-edition WIM is only known to need this once it is read, which the build does on its own.
            if (request.EditionIndex is not null || files.FileExists(files.CombinePath(request.WorkingDirectory, "sources", "install.esd")))
            {
                plan.Add(ServicingPhase.PrepareImage);
            }
            plan.Add(ServicingPhase.Mount);
            if (work.HasUpdate)
            {
                plan.Add(ServicingPhase.Update);
            }
            if (work.HasDrivers)
            {
                plan.Add(ServicingPhase.Drivers);
            }
            if (HasApps(request))
            {
                plan.Add(ServicingPhase.RemoveApps);
            }
            if (HasSettings(request))
            {
                plan.Add(ServicingPhase.ApplySettings);
            }
            plan.Add(ServicingPhase.Cleanup);
            if (request.EnableDotNet35)
            {
                plan.Add(ServicingPhase.DotNet);
            }
            plan.Add(ServicingPhase.Save);
            plan.Add(ServicingPhase.Export);
        }
        if (request.RemoveDotNetSource)
        {
            plan.Add(ServicingPhase.RemoveDotNetSource);
        }
        return plan;
    }

    // Mounting takes a WIM with one edition. Which edition of several to keep is the request's call, so without an
    // index a multi-edition image stays as it is and its items install during setup.
    private async Task<bool> PrepareImageAsync(
        OfflineBuildRequest request,
        PhaseTracker phases,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var workingDirectory = request.WorkingDirectory;
        ImageFormatInfo? info;
        try
        {
            info = await wimImages.DetectImageFormatAsync(workingDirectory).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning($"Could not read the image in {workingDirectory}, so everything installs during setup: {ex.Message}");
            info = null;
        }

        if (info is null)
        {
            phases.Set(ServicingPhase.PrepareImage, ServicingPhaseState.Failed);
            return false;
        }

        if (info.Format == ImageFormat.Wim && info.Editions.Count == 1)
        {
            phases.Set(ServicingPhase.PrepareImage, ServicingPhaseState.Skipped);
            return true;
        }

        var keep = request.EditionIndex is { } index
            ? info.Editions.FirstOrDefault(e => e.Index == index)
            : info.Editions.Count == 1 ? info.Editions[0] : null;
        if (keep is null)
        {
            log.LogWarning($"{info.FilePath} holds {info.Editions.Count} images and none was chosen to keep; everything installs during setup");
            phases.Set(ServicingPhase.PrepareImage, ServicingPhaseState.Failed);
            return false;
        }

        phases.Set(ServicingPhase.PrepareImage, ServicingPhaseState.Running);
        Report(progress, "WIMUtil_Progress_PreparingImage");
        var result = await wimImages.KeepEditionAsWimAsync(workingDirectory, keep.Name, progress, cancellationToken).ConfigureAwait(false);
        if (!result.Removed)
        {
            phases.Set(ServicingPhase.PrepareImage, ServicingPhaseState.Failed);
            return false;
        }

        if (result.LeftoverImage is { } leftover)
        {
            log.LogWarning($"{leftover} could not be deleted after the export and would go onto the media");
        }
        phases.Set(ServicingPhase.PrepareImage, ServicingPhaseState.Done);
        return true;
    }

    private async Task<(bool Succeeded, int Applied)> RunStepAsync(string what, Func<Task<OfflineStepOutcome>> step)
    {
        try
        {
            var outcome = await step().ConfigureAwait(false);
            if (!outcome.Succeeded)
            {
                log.LogWarning($"{what} did not finish; left for setup: {string.Join(", ", outcome.Failed)}");
            }
            return (outcome.Succeeded, outcome.Applied);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError($"{what} failed: {ex.Message}", ex);
            return (false, 0);
        }
    }

    private async Task CleanUpAsync(
        OfflineBuildRequest request,
        string mountDir,
        PhaseTracker phases,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        phases.Set(ServicingPhase.Cleanup, ServicingPhaseState.Running);
        Report(progress, "WIMUtil_Progress_CleaningUp");
        var resetBase = request.Cleanup.ResetBase ? " /ResetBase" : string.Empty;
        var cleanup = await RunDismAsync($"/Image:\"{mountDir}\" /Cleanup-Image /StartComponentCleanup{resetBase}", progress, cancellationToken).ConfigureAwait(false);
        if (cleanup != 0)
        {
            log.LogWarning($"Component cleanup exited with {cleanup}; the image is saved without it");
        }

        var options = request.Cleanup;
        if (options.DownloadedUpdates)
        {
            ClearFolder(files.CombinePath(mountDir, "Windows", "SoftwareDistribution", "Download"));
        }
        if (options.ServicingLogs)
        {
            ClearFolder(files.CombinePath(mountDir, "Windows", "Logs", "CBS"), "*.cab");
            ClearFolder(files.CombinePath(mountDir, "Windows", "Logs", "DISM"));
        }
        if (options.TempFiles)
        {
            ClearFolder(files.CombinePath(mountDir, "Windows", "Temp"));
        }
        if (options.CrashReports)
        {
            var reports = files.CombinePath(mountDir, "ProgramData", "Microsoft", "Windows", "WER");
            ClearFolder(files.CombinePath(reports, "ReportArchive"));
            ClearFolder(files.CombinePath(reports, "ReportQueue"));
        }

        phases.Set(ServicingPhase.Cleanup, cleanup == 0 ? ServicingPhaseState.Done : ServicingPhaseState.Failed);
    }

    // The image is only mounted, so a file Windows or the servicing stack still holds can refuse; each one is logged and skipped.
    private void ClearFolder(string folder, string pattern = "*")
    {
        if (!files.DirectoryExists(folder))
        {
            return;
        }

        foreach (var file in files.GetFiles(folder, pattern))
        {
            DeleteQuietly(file, () =>
            {
                files.SetFileAttributes(file, FileAttributes.Normal);
                files.DeleteFile(file);
            });
        }

        if (pattern == "*")
        {
            foreach (var subfolder in files.GetDirectories(folder))
            {
                DeleteQuietly(subfolder, () => WorkingFolder.Delete(files, subfolder));
            }
        }
    }

    private void DeleteQuietly(string path, Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogWarning($"Could not delete {path} from the image: {ex.Message}");
        }
    }

    private async Task EnableDotNetAsync(
        OfflineBuildRequest request,
        string mountDir,
        PhaseTracker phases,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var sxs = files.CombinePath(request.WorkingDirectory, "sources", "sxs");
        if (!files.DirectoryExists(sxs))
        {
            log.LogWarning($"{sxs} is missing, so .NET Framework 3.5 is not built into the image");
            phases.Set(ServicingPhase.DotNet, ServicingPhaseState.Skipped);
            return;
        }

        phases.Set(ServicingPhase.DotNet, ServicingPhaseState.Running);
        Report(progress, "WIMUtil_Progress_EnablingDotNet");

        // /LimitAccess keeps DISM off Windows Update, so the media's own sxs folder is the only source.
        var exit = await RunDismAsync($"/Image:\"{mountDir}\" /Enable-Feature /FeatureName:NetFx3 /All /LimitAccess /Source:\"{sxs}\"", progress, cancellationToken).ConfigureAwait(false);
        phases.Set(ServicingPhase.DotNet, InstallerExitCode.IsSuccess(exit) ? ServicingPhaseState.Done : ServicingPhaseState.Failed);
    }

    private async Task ExportImageAsync(
        string workingDirectory,
        ImageFormat outputFormat,
        PhaseTracker phases,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        phases.Set(ServicingPhase.Export, ServicingPhaseState.Running);
        Report(progress, "WIMUtil_Progress_Exporting");
        try
        {
            var result = await wimImages.RewriteAsync(workingDirectory, outputFormat, progress, cancellationToken).ConfigureAwait(false);
            if (result.LeftoverImage is { } leftover)
            {
                log.LogWarning($"{leftover} could not be deleted after the export and would go onto the media");
            }
            phases.Set(ServicingPhase.Export, result.Removed ? ServicingPhaseState.Done : ServicingPhaseState.Failed);
        }
        catch (InsufficientDiskSpaceException ex)
        {
            log.LogWarning($"No room to export the image, so it keeps the space servicing left behind: {ex.Message}");
            phases.Set(ServicingPhase.Export, ServicingPhaseState.Failed);
        }
    }

    // The sources\sxs folder is the only place .NET 3.5 can come from, so it stays unless the image already has it.
    private void RemoveDotNetSource(OfflineBuildRequest request, PhaseTracker phases, IProgress<TaskProgressDetail>? progress)
    {
        if (!request.RemoveDotNetSource)
        {
            return;
        }

        if (request.EnableDotNet35 && phases.StateOf(ServicingPhase.DotNet) != ServicingPhaseState.Done)
        {
            log.LogWarning(".NET Framework 3.5 is not in the image, so the sxs folder stays for setup");
            phases.Set(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Skipped);
            return;
        }

        var sxs = files.CombinePath(request.WorkingDirectory, "sources", "sxs");
        if (!files.DirectoryExists(sxs))
        {
            phases.Set(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Skipped);
            return;
        }

        phases.Set(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Running);
        Report(progress, "WIMUtil_Progress_RemovingDotNetSource");
        try
        {
            WorkingFolder.Delete(files, sxs);
            phases.Set(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Done);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogWarning($"Could not delete {sxs}: {ex.Message}");
            phases.Set(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Failed);
        }
    }

    private async Task<OfflineServicingResult> DiscardAsync(string workingDirectory, string mountDir, bool hasDrivers, bool hasUpdate, PhaseTracker phases)
    {
        await UnmountDiscardingAsync(mountDir).ConfigureAwait(false);
        return await FallBackToSetupAsync(workingDirectory, hasDrivers, hasUpdate, phases).ConfigureAwait(false);
    }

    private async Task<OfflineServicingResult> FallBackToSetupAsync(string workingDirectory, bool hasDrivers, bool hasUpdate, PhaseTracker phases)
    {
        phases.Abandon();
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
                WorkingFolder.Delete(files, DriverStaging.FolderOf(files, workingDirectory));
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

    private sealed record StagedWork(bool HasDrivers, string? Target, string[] Updates)
    {
        public bool HasUpdate { get; init; } = Updates.Length > 0;
    }

    private sealed class PhaseTracker(IReadOnlyList<ServicingPhase> planned, IProgress<ServicingPhaseUpdate>? sink)
    {
        private readonly Dictionary<ServicingPhase, ServicingPhaseState> _states = planned.ToDictionary(p => p, _ => ServicingPhaseState.Pending);

        public ServicingPhaseState? StateOf(ServicingPhase phase) => _states.TryGetValue(phase, out var state) ? state : null;

        // A phase the plan did not list is not shown, so a step the build takes on its own never adds a card.
        public void Set(ServicingPhase phase, ServicingPhaseState state)
        {
            if (!_states.ContainsKey(phase))
            {
                return;
            }
            _states[phase] = state;
            sink?.Report(new ServicingPhaseUpdate(phase, state));
        }

        // A discarded mount undoes every phase inside it; only the one that was running counts as the failure.
        // Removing the .NET source and cutting the image down to one edition touch the media, not the mount, so
        // they keep their own state.
        public void Abandon()
        {
            foreach (var phase in planned.Where(p => p is not (ServicingPhase.RemoveDotNetSource or ServicingPhase.PrepareImage)))
            {
                var state = _states[phase];
                if (state == ServicingPhaseState.Running)
                {
                    Set(phase, ServicingPhaseState.Failed);
                }
                else if (state is ServicingPhaseState.Pending or ServicingPhaseState.Done)
                {
                    Set(phase, ServicingPhaseState.Skipped);
                }
            }
        }

        public IReadOnlyList<ServicingPhaseUpdate> Snapshot() =>
            planned.Select(p => new ServicingPhaseUpdate(p, _states[p])).ToList();
    }
}

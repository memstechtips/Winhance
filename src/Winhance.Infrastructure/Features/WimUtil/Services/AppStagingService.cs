using System.Diagnostics;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Autounattend.Writers;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class AppStagingService(
    IWinGetPackageInstaller winGet,
    IDirectDownloadService downloads,
    IFileSystemService files,
    IVersionService versions,
    ILogService log,
    ILocalizationService localization) : IAppStagingService
{
    public async Task<IReadOnlyList<StagedApp>> StageAsync(
        string mediaFolder,
        SelectionSet set,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outcomes = new List<StagedApp>();
        var appsFolder = OnMedia(mediaFolder, ScriptPaths.UnattendAppsDirectory);
        var ownFolder = OnMedia(mediaFolder, ScriptPaths.UnattendOwnInstallersDirectory);
        var ownInstallers = OwnInstaller.From(set);

        var catalog = ExternalAppDefinitions.GetExternalApps().Items;
        var apps = new List<ItemDefinition>();
        foreach (var choice in set.ExternalApps)
        {
            if (IsDirectChild(appsFolder, choice.Id)
                && (catalog.FirstOrDefault(item => string.Equals(item.Id, choice.Id, StringComparison.Ordinal)) ?? PackageSearchItem.FromId(choice.Id)) is { } app)
                apps.Add(app);
            else
                outcomes.Add(new StagedApp(choice.Name, StagingOutcome.Skipped));
        }

        // The answer file says amd64 where winget and the catalog's download URLs say x64. Never x86: the Winhance
        // installer allows x64-compatible Windows only (Winhance.Installer.iss), and only Winhance runs what is staged.
        var architectures = ArchitectureWriter.SelectedArchitectures(set)
            .Where(name => name != "x86")
            .Select(name => name == "amd64" ? "x64" : name)
            .ToList();

        DeleteSubfoldersExcept(appsFolder, [.. set.ExternalApps.Select(choice => choice.Id), files.GetFileName(ownFolder)]);

        for (var i = 0; i < apps.Count; i++)
        {
            var app = apps[i];
            progress?.Report(new TaskProgressDetail
            {
                StatusText = localization.GetString("Progress_Downloading", app.Name),
                QueueTotal = apps.Count,
                QueueCurrent = i + 1,
                QueueNextItemName = i + 1 < apps.Count ? apps[i + 1].Name : null,
            });

            var appFolder = files.CombinePath(appsFolder, app.Id);
            DeleteSubfoldersExcept(appFolder, architectures);

            var outcome = StagingOutcome.Skipped;
            foreach (var architecture in architectures)
            {
                outcome = Best(outcome, await StageAppAsync(app, architecture, files.CombinePath(appFolder, architecture), progress, cancellationToken).ConfigureAwait(false));
            }

            outcomes.Add(new StagedApp(app.Name, outcome));
            if (files.DirectoryExists(appFolder) && files.GetDirectories(appFolder).Length == 0)
                DeleteFolder(appFolder);
        }

        // An installer no longer at its path on this PC but already on the media from an earlier Generate stays there.
        // Two rows with the same file name share a folder name on the media, so neither copy can be told apart.
        var sharedNames = ownInstallers
            .GroupBy(installer => files.GetFileNameWithoutExtension(installer.FilePath), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var onMediaOnly = ownInstallers
            .Where(installer => !sharedNames.Contains(files.GetFileNameWithoutExtension(installer.FilePath)) && IsOnMediaOnly(installer, ownFolder))
            .ToList();
        if (onMediaOnly.Count == 0)
            DeleteFolder(ownFolder);
        else
            DeleteSubfoldersExcept(ownFolder, [.. onMediaOnly.Select(installer => files.GetFileNameWithoutExtension(installer.FilePath))]);
        foreach (var installer in ownInstallers)
        {
            var outcome = await Task.Run(
                () => onMediaOnly.Contains(installer) ? KeepOwnInstaller(installer, ownFolder) : StageOwnInstaller(installer, ownFolder),
                cancellationToken).ConfigureAwait(false);
            outcomes.Add(new StagedApp(files.GetFileName(installer.FilePath), outcome));
        }

        var winhanceInstaller = OnMedia(mediaFolder, ScriptPaths.UnattendWinhanceInstallerPath);
        var winhanceStaged = await IsUpToDateAsync(winhanceInstaller, cancellationToken).ConfigureAwait(false)
            || await StageWinhanceInstallerAsync(winhanceInstaller, progress, cancellationToken).ConfigureAwait(false)
            || files.FileExists(winhanceInstaller);
        outcomes.Add(new StagedApp("Winhance", winhanceStaged ? StagingOutcome.OnMedia : StagingOutcome.DownloadFailed));

        return outcomes;
    }

    public void RemoveStaged(string mediaFolder) => DeleteFolder(OnMedia(mediaFolder, ScriptPaths.UnattendAppsDirectory));

    private static StagingOutcome Best(StagingOutcome a, StagingOutcome b) => a < b ? a : b;

    // The folder is named for the PC that runs it, not for what is inside: a 32-bit installer in the x64 folder runs
    // under WOW64, and Windows 11 on Arm runs x64 and x86 under emulation. winget's -a x64 never matches a neutral
    // installer, so neutral is asked for by name.
    private static string[] DownloadArchitectures(string architecture) => architecture switch
    {
        "arm64" => ["arm64", "neutral", "x64", "x86"],
        _ => [architecture, "neutral", "x86"],
    };

    private async Task<StagingOutcome> StageAppAsync(
        ItemDefinition app,
        string architecture,
        string folder,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var outcome = StagingOutcome.Skipped;
        try
        {
            if (HoldsOfflineInstaller(folder, acceptPackaged: true))
                return StagingOutcome.OnMedia;

            // An msix installs for the signed-in user alone, so the catalog's download, which installs for everyone,
            // goes first and the msix is fetched again only when that fails.
            var packageId = app.WinGetPackageId is [var id, ..] ? id : null;
            var hasDownload = app.ExternalApp is not null;
            string? packagedArchitecture = null;
            if (packageId != null)
            {
                // Only a missing installer moves on to the next architecture: a failed download would trade a 64-bit
                // app the new PC gets online for a 32-bit one on the media.
                foreach (var downloadArchitecture in DownloadArchitectures(architecture))
                {
                    var attempt = await DownloadAsync(packageId, downloadArchitecture, app.Name, folder, acceptPackaged: !hasDownload, cancellationToken).ConfigureAwait(false);
                    if (attempt == StagingOutcome.OnMedia)
                        return attempt;
                    outcome = Best(outcome, attempt);
                    if (attempt == StagingOutcome.NeedsWinGet && HoldsOfflineInstaller(folder, acceptPackaged: true))
                        packagedArchitecture = downloadArchitecture;
                    if (attempt != StagingOutcome.NoInstallerForArchitecture)
                        break;
                }
            }

            if (hasDownload)
            {
                ResetFolder(folder);
                if (await downloads.DownloadAsync(app, architecture, folder, progress, cancellationToken).ConfigureAwait(false) is not null)
                    return StagingOutcome.OnMedia;
                outcome = Best(outcome, StagingOutcome.DownloadFailed);

                if (packageId != null && packagedArchitecture != null
                    && await DownloadAsync(packageId, packagedArchitecture, app.Name, folder, acceptPackaged: true, cancellationToken).ConfigureAwait(false) == StagingOutcome.OnMedia)
                    return StagingOutcome.OnMedia;
            }
        }
        catch (OperationCanceledException)
        {
            // A download cut off halfway would pass for a staged installer on the next Generate.
            DeleteFolder(folder);
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning($"Could not stage {app.Name} ({architecture}): {ex.Message}");
            outcome = Best(outcome, StagingOutcome.DownloadFailed);
        }

        log.LogInformation($"{app.Name} ({architecture}) is not on the media ({outcome}); the new PC downloads it after setup");
        DeleteFolder(folder);
        return outcome;
    }

    private async Task<StagingOutcome> DownloadAsync(string packageId, string architecture, string name, string folder, bool acceptPackaged, CancellationToken cancellationToken)
    {
        // Machine scope first so one copy installs for everyone; a package whose only installer is per-user
        // has nothing at that scope, so ask again without one.
        var outcome = StagingOutcome.Skipped;
        foreach (var forAllUsers in new[] { true, false })
        {
            ResetFolder(folder);
            var result = await winGet.DownloadPackageAsync(packageId, architecture, forAllUsers, folder, name, cancellationToken).ConfigureAwait(false);
            var attempt = result switch
            {
                { Success: true } => HoldsOfflineInstaller(folder, acceptPackaged) ? StagingOutcome.OnMedia : StagingOutcome.NeedsWinGet,
                { FailureReason: InstallFailureReason.NoApplicableInstallers } => StagingOutcome.NoInstallerForArchitecture,
                _ => StagingOutcome.DownloadFailed,
            };
            if (attempt == StagingOutcome.OnMedia)
                return attempt;
            outcome = Best(outcome, attempt);
        }

        return outcome;
    }

    // A package type Winhance cannot install itself (a font, a zip holding an msix) is left to winget online.
    private bool HoldsOfflineInstaller(string folder, bool acceptPackaged)
    {
        if (WinGetManifest.InstallerIn(files, folder) is null)
            return false;

        return files.GetFiles(folder)
            .Where(WinGetManifest.IsManifest)
            .All(path => WinGetManifest.TryRead(files.ReadAllText(path)) is { } manifest
                && manifest.InstallsWithoutWinGet
                && (acceptPackaged || !manifest.IsPackaged));
    }

    private bool IsOnMediaOnly(OwnInstaller installer, string ownFolder) =>
        !files.FileExists(installer.FilePath)
        && files.FileExists(files.CombinePath(
            files.CombinePath(ownFolder, files.GetFileNameWithoutExtension(installer.FilePath)),
            files.GetFileName(installer.FilePath)));

    private StagingOutcome StageOwnInstaller(OwnInstaller installer, string ownFolder)
    {
        try
        {
            if (!files.FileExists(installer.FilePath))
            {
                log.LogWarning($"Own installer not found: {installer.FilePath}");
                return StagingOutcome.NotFound;
            }

            var name = files.GetFileNameWithoutExtension(installer.FilePath);
            var folder = files.CombinePath(ownFolder, name);
            for (var copy = 2; files.DirectoryExists(folder); copy++)
                folder = files.CombinePath(ownFolder, $"{name} ({copy})");

            files.CreateDirectory(folder);
            files.CopyFile(installer.FilePath, files.CombinePath(folder, files.GetFileName(installer.FilePath)));
            WriteSwitches(installer, folder);

            return StagingOutcome.OnMedia;
        }
        catch (Exception ex)
        {
            log.LogWarning($"Could not stage {installer.FilePath}: {ex.Message}");
            return StagingOutcome.CopyFailed;
        }
    }

    private StagingOutcome KeepOwnInstaller(OwnInstaller installer, string ownFolder)
    {
        try
        {
            WriteSwitches(installer, files.CombinePath(ownFolder, files.GetFileNameWithoutExtension(installer.FilePath)));
            return StagingOutcome.OnMedia;
        }
        catch (Exception ex)
        {
            log.LogWarning($"Could not update the switches of {installer.FilePath} on the media: {ex.Message}");
            return StagingOutcome.CopyFailed;
        }
    }

    // Written in winget's manifest shape so the new PC reads the user's switches with the same reader.
    private void WriteSwitches(OwnInstaller installer, string folder)
    {
        var manifest = files.CombinePath(folder, files.GetFileNameWithoutExtension(installer.FilePath) + ".yaml");
        if (string.IsNullOrWhiteSpace(installer.SilentSwitches))
        {
            if (files.FileExists(manifest))
                files.DeleteFile(manifest);
            return;
        }

        var type = string.Equals(files.GetExtension(installer.FilePath), ".msi", StringComparison.OrdinalIgnoreCase) ? "msi" : "exe";
        files.WriteAllText(manifest, WinGetManifest.Write(type, installer.SilentSwitches.Trim()));
    }

    // A copy on the media stays until GitHub has a newer release, so a dev build put there by hand survives and an old
    // release from an earlier Generate does not. Offline, whatever is there stays. Released installers stamp their
    // FileVersion as 0.0.0.0, so the version is read from ProductVersion.
    private async Task<bool> IsUpToDateAsync(string installer, CancellationToken cancellationToken)
    {
        if (!files.FileExists(installer))
            return false;

        var latest = await versions.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(latest.Version)
            || !latest.IsNewerThan(VersionInfo.FromProductVersion(FileVersionInfo.GetVersionInfo(installer).ProductVersion ?? string.Empty));
    }

    private async Task<bool> StageWinhanceInstallerAsync(string destination, IProgress<TaskProgressDetail>? progress, CancellationToken cancellationToken)
    {
        var tempFolder = files.CombinePath(files.GetTempPath(), $"Winhance_Installer_{Guid.NewGuid():N}");
        try
        {
            var downloaded = await downloads.DownloadFileAsync(VersionService.LatestInstallerUrl, tempFolder, "Winhance", progress, cancellationToken).ConfigureAwait(false);
            if (downloaded is null)
            {
                log.LogWarning("Could not download the Winhance installer; the new PC downloads it after setup");
                return false;
            }

            files.CreateDirectory(files.GetDirectoryName(destination)!);
            if (files.FileExists(destination))
                files.DeleteFile(destination);
            files.MoveFile(downloaded, destination);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning($"Could not stage the Winhance installer; the new PC downloads it after setup: {ex.Message}");
            return false;
        }
        finally
        {
            DeleteFolder(tempFolder);
        }
    }

    // The set may come from a file anyone wrote, and an app's id names its folder on the media.
    internal static bool IsDirectChild(string folder, string name)
    {
        var path = Path.GetFullPath(Path.Combine(folder, name));
        return string.Equals(Path.GetFileName(path), name, StringComparison.Ordinal)
            && string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase);
    }

    private string OnMedia(string mediaFolder, string pathOnSystemDrive) =>
        files.CombinePath(mediaFolder, "sources", "$OEM$", "$1", pathOnSystemDrive[files.GetPathRoot(pathOnSystemDrive).Length..]);

    private void DeleteSubfoldersExcept(string folder, IReadOnlyCollection<string> keep)
    {
        if (!files.DirectoryExists(folder))
            return;

        foreach (var subfolder in files.GetDirectories(folder))
        {
            if (!keep.Contains(files.GetFileName(subfolder), StringComparer.OrdinalIgnoreCase))
                DeleteFolder(subfolder);
        }
    }

    private void ResetFolder(string folder)
    {
        DeleteFolder(folder);
        files.CreateDirectory(folder);
    }

    // A folder Windows will not let go of costs space on the media, never the whole Generate.
    private void DeleteFolder(string folder)
    {
        try
        {
            if (files.DirectoryExists(folder))
                files.DeleteDirectory(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogWarning($"Could not delete {folder}: {ex.Message}");
        }
    }
}

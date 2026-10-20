using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal sealed class StagedAppInstaller(
    IFileSystemService files,
    IWindowsRegistryService registry,
    IProcessExecutor processExecutor,
    IDirectDownloadService directDownloads,
    ILogService log,
    ILocalizationService localization) : IStagedAppInstaller
{
    // Some installers hand off to a child process and return while it still runs, and Windows will not delete a
    // running .exe, so the delete waits and tries again before giving up.
    private static readonly int[] DeleteRetryDelaysMs = [500, 1000, 2000, 4000];

    public StagedInstaller? Find(ItemDefinition app)
    {
        var folder = files.CombinePath(ScriptPaths.UnattendAppsDirectory, app.Id, ArchitectureHelper.GetCurrentArchitecture());
        return WinGetManifest.InstallerIn(files, folder) != null ? new StagedInstaller(app.Name, folder) : null;
    }

    public IReadOnlyList<StagedInstaller> FindAll(string folder)
    {
        if (!files.DirectoryExists(folder))
            return [];

        return files.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(subfolder => WinGetManifest.InstallerIn(files, subfolder) != null)
            .Select(subfolder => new StagedInstaller(files.GetFileName(subfolder), subfolder))
            .ToList();
    }

    public async Task<bool> InstallAsync(StagedInstaller installer, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = WinGetManifest.InstallerIn(files, installer.Folder);
            if (file == null)
                return false;

            progress?.Report(new TaskProgressDetail
            {
                StatusText = localization.GetString("Progress_Installing", installer.Name),
                IsActive = true,
                IsIndeterminate = true
            });

            if (!await RunAsync(installer, file, progress, cancellationToken).ConfigureAwait(false))
                return false;

            if (registry.GetValue(ScriptPaths.UnattendSettingsKey, "KeepStagedInstallers") is not 1)
                await DeleteAsync(installer, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.LogError($"Staged install of {installer.Name} failed: {ex.Message}");
            return false;
        }
    }

    private async Task DeleteAsync(StagedInstaller installer, CancellationToken cancellationToken)
    {
        var folder = FolderToRemove(installer.Folder);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                files.DeleteDirectory(folder, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < DeleteRetryDelaysMs.Length)
            {
                await Task.Delay(DeleteRetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.LogWarning($"Installed {installer.Name} but could not delete {folder}: {ex.Message}");
                return;
            }
        }
    }

    private async Task<bool> RunAsync(StagedInstaller installer, string file, IProgress<TaskProgressDetail>? progress, CancellationToken cancellationToken)
    {
        var manifestPath = files.GetFiles(installer.Folder, "*", SearchOption.TopDirectoryOnly).FirstOrDefault(WinGetManifest.IsManifest);
        var manifest = manifestPath == null
            ? null
            : WinGetManifest.TryRead(await files.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));

        if (manifest == null || manifest.CommandFor(file) is not { } command)
            return await directDownloads.InstallFileAsync(file, installer.Name, progress, cancellationToken).ConfigureAwait(false);

        log.LogInformation($"Installing staged {installer.Name}: {command.FileName} {command.Arguments}");
        var result = await processExecutor.ExecuteAsync(command.FileName, command.Arguments, cancellationToken).ConfigureAwait(false);
        if (manifest.SuccessCodes.Contains(result.ExitCode))
            return true;

        log.LogWarning($"Staged installer for {installer.Name} exited with {result.ExitCode}");
        return false;
    }

    // A catalog app is staged once per ticked architecture under Apps\<id>, so once it installs every copy goes.
    private string FolderToRemove(string folder)
    {
        var app = files.GetDirectoryName(folder);
        return app != null
            && string.Equals(files.GetDirectoryName(app), ScriptPaths.UnattendAppsDirectory, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(app, ScriptPaths.UnattendOwnInstallersDirectory, StringComparison.OrdinalIgnoreCase)
            ? app
            : folder;
    }
}

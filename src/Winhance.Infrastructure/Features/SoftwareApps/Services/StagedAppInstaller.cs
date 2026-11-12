using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Winhance.Infrastructure.Features.Common.Utilities;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal sealed class StagedAppInstaller(
    IFileSystemService files,
    IWindowsRegistryService registry,
    IProcessExecutor processExecutor,
    IDirectDownloadService directDownloads,
    IWindowsUIManagementService windowsUI,
    ILogService log,
    ILocalizationService localization,
    IInstallOptionsService installOptions,
    IInteractiveUserService interactiveUser) : IStagedAppInstaller
{
    // Some installers hand off to a child process and return while it still runs, and Windows will not delete a
    // running .exe, so the delete waits and tries again before giving up.
    private static readonly int[] DeleteRetryDelaysMs = [500, 1000, 2000, 4000];

    // winget names a portable's install folder and uninstall entry <PackageIdentifier>_<source>, so `winget list` and
    // `winget uninstall` treat a portable staged here as one it installed.
    private const string WinGetSource = "Microsoft.Winget.Source_8wekyb3d8bbwe";
    private const string MachineEnvironmentKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
    private const string UserEnvironmentKey = @"HKEY_CURRENT_USER\Environment";
    private const string MachineUninstallKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UserUninstallKey = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    // Values in the uninstall key that let an uninstall tell a portable laid out here from one winget installed.
    private const string PortableMarker = "WinhanceStagedPortable";
    private const string LinksValue = "WinhanceLinks";
    private const string PathEntryValue = "WinhancePathEntry";

    // Where a portable goes for one scope, laid out the way winget does it for that scope.
    private sealed record PortableLocation(string WinGetRoot, string EnvironmentKey, string UninstallKey, string StartMenu);

    public StagedInstaller? Find(ItemDefinition app)
    {
        var folder = files.CombinePath(ScriptPaths.UnattendAppsDirectory, app.Id, ArchitectureHelper.GetCurrentArchitecture());
        return WinGetManifest.InstallerIn(files, folder) != null ? new StagedInstaller(app.Name, folder, app.Id) : null;
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

        if (manifest is { IsPortable: true })
            return await InstallPortableAsync(manifest, file, installer.Name, ScopeFor(installer), cancellationToken).ConfigureAwait(false);

        if (manifest is { IsPackaged: true })
        {
            // PackageManager installs for the signed-in user, who is the one running the staged installs.
            log.LogInformation($"Installing staged {installer.Name} package: {file}");
            await new Windows.Management.Deployment.PackageManager()
                .AddPackageAsync(new Uri(file), null, Windows.Management.Deployment.DeploymentOptions.None)
                .AsTask(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (manifest is { InstallerType: "zip", NestedInstallerFiles.Count: 1 })
        {
            var extracted = files.CombinePath(installer.Folder, "extracted");
            files.ExtractZip(file, extracted);
            file = files.CombinePath(extracted, manifest.NestedInstallerFiles[0].RelativeFilePath);
        }

        if (manifest == null || manifest.CommandFor(file) is not { } command)
            return await directDownloads.InstallFileAsync(file, installer.Name, progress, cancellationToken).ConfigureAwait(false);

        log.LogInformation($"Installing staged {installer.Name}: {command.FileName} {command.Arguments}");
        var result = await processExecutor.ExecuteAsync(command.FileName, command.Arguments, cancellationToken).ConfigureAwait(false);
        if (manifest.SuccessCodes.Contains(result.ExitCode))
            return true;

        log.LogWarning($"Staged installer for {installer.Name} exited with {result.ExitCode}");
        return false;
    }

    private InstallScope ScopeFor(StagedInstaller installer) =>
        (installer.AppId is { } id ? installOptions.EffectiveFor(id) : installOptions.Defaults).ScopeForPortable();

    private PortableLocation LocationFor(InstallScope scope) => scope == InstallScope.User
        ? new(
            files.CombinePath(interactiveUser.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet"),
            UserEnvironmentKey,
            UserUninstallKey,
            interactiveUser.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs))
        : new(
            files.CombinePath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinGet"),
            MachineEnvironmentKey,
            MachineUninstallKey,
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));

    // A portable the way winget lays one out for its scope: the files in WinGet\Packages\<product code>, a symlink per
    // command in WinGet\Links with that folder on the PATH, the uninstall entry winget writes, and a Start menu folder.
    // Everyone: Program Files, the machine PATH, HKLM, the shared Start menu. Only the signed-in user: their LocalAppData,
    // user PATH, HKCU and Start menu. When Windows refuses the symlink, winget puts the install folder itself on PATH instead.
    private async Task<bool> InstallPortableAsync(WinGetManifest manifest, string file, string name, InstallScope scope, CancellationToken cancellationToken)
    {
        var location = LocationFor(scope);
        var productCode = $"{manifest.PackageIdentifier ?? name}_{WinGetSource}";
        var root = files.CombinePath(location.WinGetRoot, "Packages", productCode);
        var links = files.CombinePath(location.WinGetRoot, "Links");
        files.CreateDirectory(root);
        files.CreateDirectory(links);

        List<(string Link, string Target)> commands;
        if (manifest.InstallerType == "zip")
        {
            files.ExtractZip(file, root);
            commands = manifest.NestedInstallerFiles
                .Select(nested => (
                    files.CombinePath(links, nested.PortableCommandAlias is { } alias ? alias + ".exe" : files.GetFileName(nested.RelativeFilePath)),
                    files.CombinePath(root, nested.RelativeFilePath)))
                .ToList();
        }
        else
        {
            var alias = manifest.Command
                ?? (Uri.TryCreate(manifest.InstallerUrl, UriKind.Absolute, out var url) ? files.GetFileNameWithoutExtension(url.AbsolutePath) : null)
                ?? files.GetFileNameWithoutExtension(file);
            commands = [(files.CombinePath(links, alias + ".exe"), files.CombinePath(root, alias + ".exe"))];
            files.CopyFile(file, commands[0].Target, overwrite: true);
        }

        var linked = commands.Select(command => TryLink(command.Link, command.Target)).ToList().TrueForAll(ok => ok);
        var pathEntry = linked ? links : root;
        var pathEntryAdded = AddToPath(location.EnvironmentKey, pathEntry);

        var entry = $@"{location.UninstallKey}\{productCode}";
        var values = new Dictionary<string, string?>
        {
            ["WinGetPackageIdentifier"] = manifest.PackageIdentifier,
            ["WinGetSourceIdentifier"] = WinGetSource,
            ["WinGetInstallerType"] = "portable",
            ["UninstallString"] = $"winget uninstall --product-code {productCode}",
            ["DisplayName"] = manifest.DisplayName ?? name,
            ["DisplayVersion"] = manifest.DisplayVersion,
            ["Publisher"] = manifest.Publisher,
            ["InstallDate"] = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            ["URLInfoAbout"] = manifest.PackageUrl,
            ["HelpLink"] = manifest.SupportUrl,
            ["InstallLocation"] = root,
        };
        if (manifest.InstallerType != "zip")
        {
            var target = commands[0].Target;
            values["TargetFullPath"] = target;
            values["SymlinkFullPath"] = commands[0].Link;
            values["SHA256"] = Convert.ToHexStringLower(SHA256.HashData(await files.ReadAllBytesAsync(target, cancellationToken).ConfigureAwait(false)));
        }

        foreach (var (valueName, value) in values)
        {
            if (value != null)
                registry.SetValue(entry, valueName, value, RegistryValueKind.String);
        }

        registry.SetValue(entry, PortableMarker, 1, RegistryValueKind.DWord);
        registry.SetValue(entry, LinksValue, string.Join('|', commands.Select(command => command.Link)), RegistryValueKind.String);
        if (pathEntryAdded)
            registry.SetValue(entry, PathEntryValue, pathEntry, RegistryValueKind.String);

        registry.SetValue(entry, "InstallDirectoryCreated", 1, RegistryValueKind.DWord);
        registry.SetValue(entry, "InstallDirectoryAddedToPath", linked ? 0 : 1, RegistryValueKind.DWord);
        await CreateStartMenuShortcutsAsync(name, location.StartMenu, commands.Select(command => command.Target)).ConfigureAwait(false);
        log.LogInformation($"Installed staged portable {name} to {root}");
        return true;
    }

    // The folder carries the app's name, which is what an uninstall from Winhance removes it by.
    private async Task CreateStartMenuShortcutsAsync(string name, string startMenu, IEnumerable<string> targets)
    {
        try
        {
            var folder = files.CombinePath(startMenu, name);
            files.CreateDirectory(folder);
            foreach (var target in targets)
            {
                var shortcut = files.CombinePath(folder, files.GetFileNameWithoutExtension(target) + ".lnk");
                var args = PowerShellScriptUtilities.CreateShortcutArguments(shortcut, target, files.GetDirectoryName(target)!, name);
                await processExecutor.ExecuteAsync("powershell", args, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogWarning($"Could not create the Start menu shortcuts for {name}: {ex.Message}");
        }
    }

    private bool TryLink(string link, string target)
    {
        try
        {
            files.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogWarning($"Could not link {link} to {target}: {ex.Message}");
            return false;
        }
    }

    // Read and written back unexpanded, so the %SystemRoot% entries stay variables and the value stays REG_EXPAND_SZ.
    private bool AddToPath(string environmentKey, string folder)
    {
        var path = registry.GetUnexpandedString(environmentKey, "Path") ?? string.Empty;
        if (IsOnPath(path, folder))
            return false;

        var updated = path.Length == 0 || path.EndsWith(';') ? path + folder : $"{path};{folder}";
        registry.SetValue(environmentKey, "Path", updated, RegistryValueKind.ExpandString);
        windowsUI.BroadcastEnvironmentChange();
        return true;
    }

    private void RemoveFromPath(string environmentKey, string folder)
    {
        var path = registry.GetUnexpandedString(environmentKey, "Path") ?? string.Empty;
        if (!IsOnPath(path, folder))
            return;

        var kept = path.Split(';').Where(entry => !IsSameFolder(entry, folder));
        registry.SetValue(environmentKey, "Path", string.Join(';', kept), RegistryValueKind.ExpandString);
        windowsUI.BroadcastEnvironmentChange();
    }

    private static bool IsOnPath(string path, string folder) => path.Split(';').Any(entry => IsSameFolder(entry, folder));

    private static bool IsSameFolder(string entry, string folder) =>
        string.Equals(entry.Trim().TrimEnd('\\'), folder, StringComparison.OrdinalIgnoreCase);

    public Task<OperationResult<bool>?> UninstallPortableAsync(ItemDefinition app, CancellationToken cancellationToken = default)
    {
        var entries = new[] { InstallScope.User, InstallScope.Machine }
            .Select(LocationFor)
            .SelectMany(location => registry.GetSubKeyNames(location.UninstallKey)
                .Select(name => (Location: location, Key: $@"{location.UninstallKey}\{name}")))
            .Where(entry => registry.GetValue(entry.Key, PortableMarker) is 1 && IsEntryOf(app, entry.Key))
            .ToList();
        if (entries.Count == 0)
            return Task.FromResult<OperationResult<bool>?>(null);

        try
        {
            foreach (var (location, key) in entries)
                RemovePortable(location, key);

            return Task.FromResult<OperationResult<bool>?>(OperationResult<bool>.Succeeded(true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogError($"Could not remove the staged portable {app.Name}: {ex.Message}");
            return Task.FromResult<OperationResult<bool>?>(OperationResult<bool>.Failed($"Uninstall failed: {ex.Message}"));
        }
    }

    private bool IsEntryOf(ItemDefinition app, string key) =>
        (registry.GetValue(key, "WinGetPackageIdentifier") is string id && app.WinGetPackageId?.Contains(id, StringComparer.OrdinalIgnoreCase) == true)
        || string.Equals(registry.GetValue(key, "DisplayName") as string, app.Name, StringComparison.OrdinalIgnoreCase);

    // The uninstall key goes last, so a delete that fails leaves the entry for the next attempt.
    private void RemovePortable(PortableLocation location, string key)
    {
        foreach (var link in (registry.GetValue(key, LinksValue) as string ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
            files.DeleteFile(link);

        if (registry.GetValue(key, "InstallLocation") is string root && files.DirectoryExists(root))
            files.DeleteDirectory(root, recursive: true);

        if (registry.GetValue(key, PathEntryValue) is string pathEntry)
            RemoveFromPath(location.EnvironmentKey, pathEntry);

        registry.DeleteKey(key);
        log.LogInformation($"Removed staged portable at {key}");
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

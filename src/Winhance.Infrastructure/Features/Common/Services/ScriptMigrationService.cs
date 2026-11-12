using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class ScriptMigrationService : IScriptMigrationService
{
    private static readonly string[] TaskNames = KeepRemovedModeStore.RemovalTaskNames;

    internal static readonly string[] BloatArrays =
    [
        BloatRemovalScriptGenerator.PackagesArray,
        BloatRemovalScriptGenerator.CapabilitiesArray,
        BloatRemovalScriptGenerator.OptionalFeaturesArray,
    ];

    internal static readonly Lazy<ItemDefinition[]> CatalogItems = new(() =>
        [.. WindowsAppDefinitions.GetWindowsApps().Items
            .Concat(CapabilityDefinitions.GetWindowsCapabilities().Items)
            .Concat(OptionalFeatureDefinitions.GetWindowsOptionalFeatures().Items)]);

    private readonly ILogService _logService;
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly IUserPreferencesService _prefsService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IBackgroundTaskService _backgroundTaskService;

    public ScriptMigrationService(
        ILogService logService,
        IScheduledTaskService scheduledTaskService,
        IUserPreferencesService prefsService,
        IInteractiveUserService interactiveUserService,
        IFileSystemService fileSystemService,
        IBackgroundTaskService backgroundTaskService)
    {
        _logService = logService;
        _scheduledTaskService = scheduledTaskService;
        _prefsService = prefsService;
        _interactiveUserService = interactiveUserService;
        _fileSystemService = fileSystemService;
        _backgroundTaskService = backgroundTaskService;
    }

    public async Task<ScriptMigrationResult> MigrateFromOldPathsAsync()
    {
        var result = await MigrateOldPathsAsync().ConfigureAwait(false);
        await _backgroundTaskService.InitializeAsync().ConfigureAwait(false);
        return result;
    }

    internal static IEnumerable<string> ResolveKeptIds(string taskName, string scriptContent)
    {
        switch (taskName)
        {
            case EdgeRemovalScript.ScriptName:
                yield return "windows-app-edge";
                if (EdgeRemovalScript.RemovesWebView2(scriptContent))
                    yield return "windows-app-edge-webview";
                yield break;
            case OneDriveRemovalScript.ScriptName:
                yield return "windows-app-onedrive";
                yield break;
            case BloatRemovalScriptGenerator.ScriptName:
                var names = BloatArrays
                    .SelectMany(array => BloatRemovalScriptGenerator.ExtractArrayFromScript(scriptContent, array))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var item in CatalogItems.Value.Where(item => item.RemovalScript == null))
                {
                    if (ItemNames(item).Any(names.Contains))
                        yield return item.Id;
                }
                yield break;
        }
    }

    internal static IEnumerable<string> ItemNames(ItemDefinition item) =>
        (item.AppxPackageName ?? []).Concat([item.CapabilityName, item.OptionalFeatureName]).OfType<string>();

    private async Task<ScriptMigrationResult> MigrateOldPathsAsync()
    {
        try
        {
            var alreadyMigrated = await _prefsService.GetPreferenceAsync("ScriptMigrationCompleted", false).ConfigureAwait(false);
            if (alreadyMigrated)
            {
                _logService.Log(LogLevel.Info, "Script migration already completed previously");
                return new ScriptMigrationResult { Success = true };
            }

            var oldScriptsPath = GetOldScriptsPath();

            if (!_fileSystemService.DirectoryExists(oldScriptsPath))
            {
                _logService.Log(LogLevel.Info, "No old script directory found - migration not needed");
                await _prefsService.SetPreferenceAsync("ScriptMigrationCompleted", true).ConfigureAwait(false);
                return new ScriptMigrationResult { Success = true };
            }

            _logService.Log(LogLevel.Info, $"Found old script directory: {oldScriptsPath}");

            var tasksDeleted = await DeleteOldScheduledTasksAsync().ConfigureAwait(false);
            var scriptsRenamed = RenameOldScripts(oldScriptsPath);

            await _prefsService.SetPreferenceAsync("ScriptMigrationCompleted", true).ConfigureAwait(false);

            _logService.Log(LogLevel.Info,
                $"Migration completed: {tasksDeleted} tasks deleted, {scriptsRenamed} scripts renamed");

            return new ScriptMigrationResult
            {
                Success = true,
                MigrationPerformed = true,
                TasksDeleted = tasksDeleted,
                ScriptsRenamed = scriptsRenamed
            };
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error during script migration: {ex.Message}");
            return new ScriptMigrationResult { Success = false };
        }
    }

    private string GetOldScriptsPath()
    {
        var localAppData = _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return _fileSystemService.CombinePath(localAppData, "Winhance", "Scripts");
    }

    private async Task<int> DeleteOldScheduledTasksAsync()
    {
        int deletedCount = 0;

        foreach (var taskName in TaskNames)
        {
            try
            {
                var exists = await _scheduledTaskService.IsTaskRegisteredAsync(taskName).ConfigureAwait(false);
                if (exists)
                {
                    var deleteResult = await _scheduledTaskService.UnregisterScheduledTaskAsync(taskName).ConfigureAwait(false);
                    if (deleteResult.Success)
                    {
                        deletedCount++;
                        _logService.Log(LogLevel.Info, $"Deleted old scheduled task: {taskName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Could not delete task {taskName}: {ex.Message}");
            }
        }

        return deletedCount;
    }

    private int RenameOldScripts(string oldScriptsPath)
    {
        int renamedCount = 0;

        foreach (var scriptName in TaskNames.Select(n => n + ".ps1"))
        {
            try
            {
                var oldScriptPath = _fileSystemService.CombinePath(oldScriptsPath, scriptName);
                if (_fileSystemService.FileExists(oldScriptPath))
                {
                    var newPath = oldScriptPath + ".old";

                    if (_fileSystemService.FileExists(newPath))
                    {
                        _fileSystemService.DeleteFile(newPath);
                    }

                    _fileSystemService.MoveFile(oldScriptPath, newPath);
                    renamedCount++;
                    _logService.Log(LogLevel.Info, $"Renamed old script: {scriptName} -> {scriptName}.old");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Could not rename script {scriptName}: {ex.Message}");
            }
        }

        return renamedCount;
    }
}

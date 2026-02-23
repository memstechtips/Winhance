using System;
using System.IO;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class ScriptMigrationService : IScriptMigrationService
    {
        private readonly ILogService _logService;
        private readonly IScheduledTaskService _scheduledTaskService;
        private readonly IUserPreferencesService _prefsService;
        private readonly IInteractiveUserService _interactiveUserService;
        private readonly IFileSystemService _fileSystemService;

        private static readonly string[] TaskNames = { "BloatRemoval", "EdgeRemoval", "OneDriveRemoval" };
        private static readonly string[] ScriptNames = { "BloatRemoval.ps1", "EdgeRemoval.ps1", "OneDriveRemoval.ps1" };

        public ScriptMigrationService(
            ILogService logService,
            IScheduledTaskService scheduledTaskService,
            IUserPreferencesService prefsService,
            IInteractiveUserService interactiveUserService,
            IFileSystemService fileSystemService)
        {
            _logService = logService;
            _scheduledTaskService = scheduledTaskService;
            _prefsService = prefsService;
            _interactiveUserService = interactiveUserService;
            _fileSystemService = fileSystemService;
        }

        public async Task<ScriptMigrationResult> MigrateFromOldPathsAsync()
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
                        var deleted = await _scheduledTaskService.UnregisterScheduledTaskAsync(taskName).ConfigureAwait(false);
                        if (deleted)
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

            foreach (var scriptName in ScriptNames)
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
}

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

        private static readonly string[] TaskNames = { "BloatRemoval", "EdgeRemoval", "OneDriveRemoval" };
        private static readonly string[] ScriptNames = { "BloatRemoval.ps1", "EdgeRemoval.ps1", "OneDriveRemoval.ps1" };

        public ScriptMigrationService(
            ILogService logService,
            IScheduledTaskService scheduledTaskService,
            IUserPreferencesService prefsService,
            IInteractiveUserService interactiveUserService)
        {
            _logService = logService;
            _scheduledTaskService = scheduledTaskService;
            _prefsService = prefsService;
            _interactiveUserService = interactiveUserService;
        }

        public async Task<ScriptMigrationResult> MigrateFromOldPathsAsync()
        {
            var result = new ScriptMigrationResult { Success = true };

            try
            {
                var alreadyMigrated = await _prefsService.GetPreferenceAsync("ScriptMigrationCompleted", false).ConfigureAwait(false);
                if (alreadyMigrated)
                {
                    _logService.Log(LogLevel.Info, "Script migration already completed previously");
                    return result;
                }

                var oldScriptsPath = GetOldScriptsPath();

                if (!Directory.Exists(oldScriptsPath))
                {
                    _logService.Log(LogLevel.Info, "No old script directory found - migration not needed");
                    await _prefsService.SetPreferenceAsync("ScriptMigrationCompleted", true).ConfigureAwait(false);
                    return result;
                }

                _logService.Log(LogLevel.Info, $"Found old script directory: {oldScriptsPath}");
                result.MigrationPerformed = true;

                result.TasksDeleted = await DeleteOldScheduledTasksAsync().ConfigureAwait(false);
                result.ScriptsRenamed = RenameOldScripts(oldScriptsPath);

                await _prefsService.SetPreferenceAsync("ScriptMigrationCompleted", true).ConfigureAwait(false);

                _logService.Log(LogLevel.Info,
                    $"Migration completed: {result.TasksDeleted} tasks deleted, {result.ScriptsRenamed} scripts renamed");

                return result;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error during script migration: {ex.Message}");
                result.Success = false;
                return result;
            }
        }

        private string GetOldScriptsPath()
        {
            var localAppData = _interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Winhance", "Scripts");
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
                    var oldScriptPath = Path.Combine(oldScriptsPath, scriptName);
                    if (File.Exists(oldScriptPath))
                    {
                        var newPath = oldScriptPath + ".old";

                        if (File.Exists(newPath))
                        {
                            File.Delete(newPath);
                        }

                        File.Move(oldScriptPath, newPath);
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

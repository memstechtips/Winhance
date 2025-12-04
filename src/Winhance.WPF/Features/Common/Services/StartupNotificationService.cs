using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.Services
{
    public class StartupNotificationService : IStartupNotificationService
    {
        private readonly IUserPreferencesService _prefsService;
        private readonly ILogService _logService;
        private readonly ILocalizationService _localizationService;

        public StartupNotificationService(
            IUserPreferencesService prefsService,
            ILogService logService,
            ILocalizationService localizationService)
        {
            _prefsService = prefsService;
            _logService = logService;
            _localizationService = localizationService;
        }

        public async Task ShowBackupNotificationAsync(BackupResult result)
        {
            if (result == null)
                return;

            if (!result.Success)
            {
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    _logService.Log(LogLevel.Info, $"Backup failed: {result.ErrorMessage}");
                }
                return;
            }

            if (!result.RestorePointCreated && !result.RegistryBackupCreated)
                return;

            try
            {
                var messageText = _localizationService.GetString("Startup_Backup_Intro") + "\n\n";

                if (result.SystemRestoreWasDisabled)
                {
                    messageText += _localizationService.GetString("Startup_Backup_RestoreEnabled") + "\n\n";
                }

                messageText += _localizationService.GetString("Startup_Backup_Created") + "\n";

                if (result.RegistryBackupCreated)
                {
                    messageText += _localizationService.GetString("Startup_Backup_RegistryBackups", result.RegistryBackupPaths.Count) + "\n";
                    messageText += _localizationService.GetString("Startup_Backup_RegistryPath") + "\n";
                }

                if (result.RestorePointCreated)
                {
                    messageText += _localizationService.GetString("Startup_Backup_RestorePoint") + "\n";
                }

                messageText += "\n";

                if (result.RestorePointCreated && result.RegistryBackupCreated)
                {
                    messageText += _localizationService.GetString("Startup_Backup_ToRestore") + "\n";
                    messageText += _localizationService.GetString("Startup_Backup_RestoreInstructions_System") + "\n";
                    messageText += _localizationService.GetString("Startup_Backup_RestoreInstructions_Registry") + "\n\n";
                }
                else if (result.RestorePointCreated)
                {
                    messageText += _localizationService.GetString("Startup_Backup_RestoreInstructions_RestoreOnly") + "\n\n";
                }
                else if (result.RegistryBackupCreated)
                {
                    messageText += _localizationService.GetString("Startup_Backup_RestoreInstructions_RegistryOnly") + "\n\n";
                }

                messageText += _localizationService.GetString("Startup_Backup_Note") + "\n";
                messageText += _localizationService.GetString("Startup_Backup_Note_AdminRights");

                var checkboxText = result.RestorePointCreated
                    ? _localizationService.GetString("Startup_Backup_Checkbox_DontCreate")
                    : _localizationService.GetString("Startup_Backup_Checkbox_DontShow");

                var checkboxChecked = CustomDialog.ShowInformationWithCheckbox(
                    _localizationService.GetString("Startup_Backup_Title"),
                    _localizationService.GetString("Startup_Backup_SubTitle"),
                    messageText,
                    checkboxText,
                    _localizationService.GetString("Button_OK"),
                    DialogType.Information,
                    titleBarIcon: "Shield"
                );

                if (checkboxChecked)
                {
                    await _prefsService.SetPreferenceAsync("SkipSystemBackup", true);
                    _logService.Log(LogLevel.Info, "User opted to skip system backup check in future launches");
                }

                _logService.Log(LogLevel.Info, "Backup notification dialog shown to user");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error showing backup notification: {ex.Message}");
            }
        }

        public void ShowMigrationNotification(ScriptMigrationResult result)
        {
            if (result == null || !result.MigrationPerformed)
                return;

            if (!result.Success)
            {
                _logService.Log(LogLevel.Info, "Migration was performed but encountered errors");
                return;
            }

            try
            {
                var messageText = _localizationService.GetString("Startup_Migration_Intro") + "\n\n";

                messageText += _localizationService.GetString("Startup_Migration_WhatChanged") + "\n";
                messageText += _localizationService.GetString("Startup_Migration_OldLocation") + "\n";
                messageText += _localizationService.GetString("Startup_Migration_NewLocation") + "\n";
                messageText += _localizationService.GetString("Startup_Migration_TasksDeleted", result.TasksDeleted) + "\n";
                messageText += _localizationService.GetString("Startup_Migration_ScriptsRenamed", result.ScriptsRenamed) + "\n\n";

                messageText += _localizationService.GetString("Startup_Migration_ImportantInfo") + "\n";
                messageText += _localizationService.GetString("Startup_Migration_AppsStayRemoved") + "\n";
                messageText += _localizationService.GetString("Startup_Migration_MayReinstall") + "\n\n";

                messageText += _localizationService.GetString("Startup_Migration_Recommendation");

                CustomDialog.ShowInformation(
                    _localizationService.GetString("Startup_Migration_Title"),
                    _localizationService.GetString("Startup_Migration_SubTitle"),
                    messageText,
                    _localizationService.GetString("Startup_Migration_OneTime")
                );

                _logService.Log(LogLevel.Info, "Migration notification shown to user");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error showing migration notification: {ex.Message}");
            }
        }
    }
}

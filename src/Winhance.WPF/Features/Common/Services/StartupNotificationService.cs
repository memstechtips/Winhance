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

        public StartupNotificationService(
            IUserPreferencesService prefsService,
            ILogService logService)
        {
            _prefsService = prefsService;
            _logService = logService;
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
                var messageText = "Winhance has protected your system with backups.\n\n";

                if (result.SystemRestoreWasDisabled)
                {
                    messageText += "System Restore was disabled and is now enabled.\n\n";
                }

                messageText += "Created:\n";

                if (result.RegistryBackupCreated)
                {
                    messageText += $"• Registry backups ({result.RegistryBackupPaths.Count} hives) at:\n";
                    messageText += "  C:\\ProgramData\\Winhance\\Backups\n";
                }

                if (result.RestorePointCreated)
                {
                    messageText += "• System Restore Point: 'Winhance Initial Restore Point'\n";
                }

                messageText += "\n";

                if (result.RestorePointCreated && result.RegistryBackupCreated)
                {
                    messageText += "To restore:\n";
                    messageText += "• System: Open 'Create a restore point' in Settings > System Restore\n";
                    messageText += "• Registry: Double-click .reg files in the backup folder\n\n";
                }
                else if (result.RestorePointCreated)
                {
                    messageText += "To restore: Search 'restore point' in Windows Settings\n\n";
                }
                else if (result.RegistryBackupCreated)
                {
                    messageText += "To restore: Double-click .reg files in the backup folder\n\n";
                }

                messageText += "Note: System Restore won't restore removed apps.\n";
                messageText += "Registry backups are one-time and require admin rights.";

                var checkboxText = result.RestorePointCreated
                    ? "Don't create restore points in the future"
                    : "Don't show this again";

                var checkboxChecked = CustomDialog.ShowInformationWithCheckbox(
                    "System Protection Enabled",
                    "Winhance Backup",
                    messageText,
                    checkboxText,
                    "OK",
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
                var messageText = "Winhance has updated the location of Windows App removal scripts.\n\n";

                messageText += "What changed:\n";
                messageText += "• Old location: %LOCALAPPDATA%\\Winhance\\Scripts\n";
                messageText += "• New location: C:\\ProgramData\\Winhance\\Scripts\n";
                messageText += $"• Removed {result.TasksDeleted} old scheduled task(s)\n";
                messageText += $"• Renamed {result.ScriptsRenamed} old script(s) to .old\n\n";

                messageText += "Important Information:\n";
                messageText += "• Windows apps you previously removed will stay removed\n";
                messageText += "• Microsoft may reinstall some apps via Windows Updates\n\n";

                messageText += "We recommend that you re-run app removal using the new version of Winhance as the new scripts are more reliable and work better.";

                CustomDialog.ShowInformation(
                    "Script Location Updated",
                    "Winhance Migration",
                    messageText,
                    "This notification will only appear once"
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

using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services
{
    public class StartupNotificationService : IStartupNotificationService
    {
        private readonly IDialogService _dialogService;
        private readonly IUserPreferencesService _prefsService;
        private readonly ILogService _logService;
        private readonly ILocalizationService _localizationService;

        public StartupNotificationService(
            IDialogService dialogService,
            IUserPreferencesService prefsService,
            ILogService logService,
            ILocalizationService localizationService)
        {
            _dialogService = dialogService;
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

            // Only show dialog when something new was actually created
            if (!result.RestorePointCreated)
                return;

            try
            {
                var messageText = _localizationService.GetString("Startup_Backup_Intro") + "\n\n";

                if (result.SystemRestoreWasDisabled)
                {
                    messageText += _localizationService.GetString("Startup_Backup_RestoreEnabled") + "\n\n";
                }

                messageText += _localizationService.GetString("Startup_Backup_Created") + "\n";
                messageText += _localizationService.GetString("Startup_Backup_RestorePoint") + "\n";
                messageText += _localizationService.GetString("Startup_Backup_ConfigBackup") + "\n\n";

                messageText += _localizationService.GetString("Startup_Backup_ToRestore") + "\n";
                messageText += _localizationService.GetString("Startup_Backup_RestoreInstructions_RestorePoint") + "\n";
                messageText += _localizationService.GetString("Startup_Backup_RestoreInstructions_ConfigBackup") + "\n\n";

                messageText += _localizationService.GetString("Startup_Backup_Note");

                var checkboxText = _localizationService.GetString("Startup_Backup_Checkbox_DontCreate");

                var dialogResult = await _dialogService.ShowConfirmationWithCheckboxAsync(
                    messageText,
                    checkboxText,
                    title: _localizationService.GetString("Startup_Backup_Title"),
                    continueButtonText: _localizationService.GetString("Button_OK"),
                    cancelButtonText: "");

                if (dialogResult.CheckboxChecked)
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

    }
}

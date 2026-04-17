using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class StartupNotificationService : IStartupNotificationService
{
    private readonly IDialogService _dialogService;
    private readonly IUserPreferencesService _prefsService;
    private readonly ISystemBackupService _backupService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;

    public StartupNotificationService(
        IDialogService dialogService,
        IUserPreferencesService prefsService,
        ISystemBackupService backupService,
        ITaskProgressService taskProgressService,
        ILogService logService,
        ILocalizationService localizationService)
    {
        _dialogService = dialogService;
        _prefsService = prefsService;
        _backupService = backupService;
        _taskProgressService = taskProgressService;
        _logService = logService;
        _localizationService = localizationService;
    }

    public async Task ShowFirstLaunchRestoreOfferAsync()
    {
        try
        {
            // Check if we've already offered
            var alreadyOffered = _prefsService.GetPreference(
                UserPreferenceKeys.InitialRestorePointOffered, false);
            if (alreadyOffered)
                return;

            // Mark as offered immediately so we don't show again even if something fails
            await _prefsService.SetPreferenceAsync(
                UserPreferenceKeys.InitialRestorePointOffered, true);

            // Build the consent dialog message
            var message = _localizationService.GetString("Startup_Backup_Intro") + "\n\n"
                + _localizationService.GetString("Startup_Backup_ConfigCreated") + "\n\n"
                + _localizationService.GetString("Startup_Backup_RestoreOffer") + "\n\n"
                + _localizationService.GetString("Startup_Backup_SkipWarning");

            var confirmed = await _dialogService.ShowConfirmationAsync(
                message,
                title: _localizationService.GetString("Startup_Backup_Title"),
                okButtonText: _localizationService.GetString("Startup_Backup_Button_Create"),
                cancelButtonText: _localizationService.GetString("Startup_Backup_Button_Skip"));

            if (confirmed)
            {
                _logService.Log(LogLevel.Info, "User chose to create restore point on first launch");

                // Use TaskProgressService so the main window progress bar shows status
                var cts = _taskProgressService.StartTask(
                    _localizationService.GetString("Progress_CreatingRestorePoint") ?? "Creating system restore point...",
                    isIndeterminate: true);
                var progress = _taskProgressService.CreateDetailedProgress();

                try
                {
                    var result = await _backupService.CreateRestorePointAsync(
                        progress: progress, cancellationToken: cts.Token);

                    _taskProgressService.CompleteTask();

                    if (result.Success && result.RestorePointCreated)
                    {
                        var successMsg = _localizationService.GetString("Startup_Backup_RestoreCreatedSuccess");
                        await _dialogService.ShowInformationAsync(
                            successMsg,
                            _localizationService.GetString("Startup_Backup_Title_Success"));
                    }
                    else
                    {
                        var failMsg = _localizationService.GetString("Startup_Backup_RestoreCreatedFail")
                            + (result.ErrorMessage != null ? $"\n\n{result.ErrorMessage}" : "");
                        await _dialogService.ShowWarningAsync(
                            failMsg,
                            _localizationService.GetString("Startup_Backup_Title_Fail"));
                    }
                }
                catch
                {
                    _taskProgressService.CompleteTask();
                    throw;
                }
            }
            else
            {
                _logService.Log(LogLevel.Info, "User skipped restore point creation on first launch");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error showing first launch restore offer: {ex.Message}");
        }
    }
}

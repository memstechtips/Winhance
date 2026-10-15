using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Services;

public class ApplicationCloseService : IApplicationCloseService
{
    private readonly ILogService _logService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly ISupportAskSchedule _supportAskSchedule;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;

    public Func<Task>? BeforeShutdown { get; set; }

    // Terminates the process. Replaced in tests so the test host survives.
    public Action ShutdownAction { get; set; } = DefaultShutdown;

    private static void DefaultShutdown()
    {
        try
        {
            Application.Current.Exit();
        }
        catch
        {
            Environment.Exit(0);
        }
    }

    public ApplicationCloseService(
        ILogService logService,
        ITaskProgressService taskProgressService,
        ISupportAskSchedule supportAskSchedule,
        IDialogService dialogService,
        ILocalizationService localizationService)
    {
        _logService = logService;
        _taskProgressService = taskProgressService;
        _supportAskSchedule = supportAskSchedule;
        _dialogService = dialogService;
        _localizationService = localizationService;
    }

    public async Task<OperationResult> CheckOperationsAndCloseAsync()
    {
        try
        {
            if (BeforeShutdown != null)
            {
                try
                {
                    await BeforeShutdown.Invoke();
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error running cleanup tasks: {ex.Message}", ex);
                }
            }

            if (_taskProgressService.IsTaskRunning)
            {
                string currentOperation = _taskProgressService.CurrentStatusText
                    ?? _localizationService.GetString("Dialog_CloseWhileRunning_UnknownOperation");

                _logService.LogInformation($"Close requested while operation in progress: {currentOperation}");

                var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
                {
                    Message = _localizationService.GetString("Dialog_CloseWhileRunning_Message", currentOperation),
                    Title = _localizationService.GetString("Dialog_CloseWhileRunning_Title"),
                    ConfirmButtonText = _localizationService.GetString("Dialog_CloseWhileRunning_ConfirmButton"),
                    CancelButtonText = _localizationService.GetString("Button_Cancel"),
                })).Confirmed;

                if (!confirmed)
                {
                    _logService.LogInformation("User cancelled application close due to running operation");
                    return OperationResult.Failed("User cancelled application close");
                }

                _logService.LogInformation("User confirmed close, cancelling operation...");
                _taskProgressService.CancelCurrentTask();
            }

            await CloseApplicationWithSupportDialogAsync();
            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error in CheckOperationsAndCloseAsync: {ex.Message}", ex);

            try
            {
                await CloseApplicationWithSupportDialogAsync();
            }
            catch
            {
                ShutdownAction();
            }
            return OperationResult.Succeeded();
        }
    }

    private async Task CloseApplicationWithSupportDialogAsync()
    {
        try
        {
            if (await ShouldAskForSupportAsync())
            {
                _logService.LogInformation("Showing support dialog");
                await _dialogService.ShowSponsorsDialogAsync(SponsorsDialogMode.Exit);
                await _supportAskSchedule.MarkAskedAsync();
            }
            else
            {
                _logService.LogInformation("Skipping support dialog: asked within the last day");
            }

            _logService.LogInformation("Shutting down application");
            ShutdownAction();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error in CloseApplicationWithSupportDialogAsync: {ex.Message}", ex);
            ShutdownAction();
        }
    }

    // Unreadable preferences must not silence the ask.
    private async Task<bool> ShouldAskForSupportAsync()
    {
        try
        {
            return await _supportAskSchedule.ShouldAskOnExitAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error checking the support ask schedule: {ex.Message}", ex);
            return true;
        }
    }
}

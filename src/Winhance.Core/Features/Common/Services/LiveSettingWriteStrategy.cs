using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services;

// Confirmation lives here rather than in the caller because it is a property of being about to change this
// machine, not of the input shape.
public sealed class LiveSettingWriteStrategy : ISettingWriteStrategy
{
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;

    public LiveSettingWriteStrategy(
        ISettingApplicationService settingApplicationService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ILogService logService)
    {
        _settingApplicationService = settingApplicationService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _logService = logService;
    }

    public async Task<SettingWriteResult> WriteAsync(
        SettingWriteRequest request,
        ISettingWriteProgress progress)
    {
        string settingId = request.SystemRequest.SettingId;

        var (confirmed, checkboxChecked) = await ConfirmAsync(request);
        if (!confirmed)
        {
            _logService.LogDebug($"[LiveWrite] {settingId} ({request.Description}) cancelled at the confirmation prompt");
            return SettingWriteResult.Rejected;
        }

        // Raised only once the prompt is answered: the ring means "the machine is busy", and the
        // machine is not busy while a dialog waits on the user.
        progress.IsApplying = true;
        try
        {
            var systemRequest = request.SystemRequest with
            {
                CheckboxResult = checkboxChecked,
                ApplyRecommended = checkboxChecked && request.CheckboxAlsoAppliesRecommended,
            };

            _logService.Log(LogLevel.Info, $"Applying setting {settingId}: {request.Description}");

            var result = await _settingApplicationService.ApplySettingAsync(systemRequest);
            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{settingId}' apply failed: {result.ErrorMessage}. Reverting UI state.");
                return SettingWriteResult.Rejected;
            }

            _logService.Log(LogLevel.Info, $"Successfully applied setting {settingId}: {request.Description}");
            return new SettingWriteResult
            {
                Outcome = SettingWriteOutcome.Applied,
                ConfirmationCheckboxChecked = checkboxChecked,
            };
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error applying setting {settingId}: {ex.Message}");
            return SettingWriteResult.Rejected;
        }
        finally
        {
            progress.IsApplying = false;
        }
    }

    private async Task<(bool Confirmed, bool CheckboxChecked)> ConfirmAsync(SettingWriteRequest request)
    {
        if (!request.RequiresConfirmation)
            return (true, false);

        string settingId = request.SystemRequest.SettingId;
        var title = _localizationService.GetString($"Setting_{settingId}_ConfirmTitle");
        var message = _localizationService.GetString($"Setting_{settingId}_ConfirmMessage");
        var checkboxText = _localizationService.GetString($"Setting_{settingId}_ConfirmCheckbox");

        // The theme-mode warning names the mode being switched to, so its two strings carry a
        // placeholder the generic path has no value for.
        if (settingId == SettingIds.ThemeModeWindows && request.SystemRequest.Value is int comboBoxIndex)
        {
            var themeMode = comboBoxIndex == 1
                ? _localizationService.GetString("Setting_theme-mode-windows_Option_1")
                : _localizationService.GetString("Setting_theme-mode-windows_Option_0");
            message = message.Replace("{themeMode}", themeMode);
            checkboxText = checkboxText.Replace("{themeMode}", themeMode);
        }

        var response = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = message,
            CheckboxText = checkboxText,
            Title = title,
            ConfirmButtonText = _localizationService.GetString("Button_Continue"),
            CancelButtonText = _localizationService.GetString("Button_Cancel"),
        });

        return (response.Confirmed, response.CheckboxChecked);
    }
}

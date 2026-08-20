using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.Common.Services;

public class WindowsVersionFilterService : IWindowsVersionFilterService
{
    private readonly IUserPreferencesService _preferencesService;
    private readonly IEventBus _eventBus;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;

    public WindowsVersionFilterService(
        IUserPreferencesService preferencesService,
        IEventBus eventBus,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ILogService logService)
    {
        _preferencesService = preferencesService;
        _eventBus = eventBus;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _logService = logService;
    }

    public bool IsFilterEnabled { get; private set; } = true;

    public event EventHandler<bool>? FilterStateChanged;

    public async Task LoadFilterPreferenceAsync()
    {
        try
        {
            IsFilterEnabled = await _preferencesService.GetPreferenceAsync(
                UserPreferenceKeys.EnableWindowsVersionFilter, defaultValue: true);

            _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                $"Loaded Windows version filter preference: {(IsFilterEnabled ? "ON" : "OFF")}");

            FilterStateChanged?.Invoke(this, IsFilterEnabled);
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error,
                $"Failed to load filter preference: {ex.Message}");
        }
    }

    public async Task<bool> ToggleFilterAsync(bool isInReviewMode)
    {
        if (isInReviewMode) return false;

        try
        {
            var dontShowAgain = await _preferencesService.GetPreferenceAsync(
                UserPreferenceKeys.DontShowFilterExplanation, defaultValue: false);

            if (!dontShowAgain)
            {
                var message = _localizationService.GetStringOrDefault("Filter_Dialog_Message", "The Windows Version Filter controls which settings are shown based on your Windows version.\n\nWhen ON: Only settings compatible with your Windows version are shown.\nWhen OFF: All settings are shown, with incompatible ones marked.");
                var checkboxText = _localizationService.GetStringOrDefault("Filter_Dialog_Checkbox", "Don't show this message again");
                var title = _localizationService.GetStringOrDefault("Filter_Dialog_Title", "Windows Version Filter");
                var continueText = _localizationService.GetStringOrDefault("Filter_Dialog_Button_Toggle", "Toggle Filter");
                var cancelText = _localizationService.GetStringOrDefault("Button_Cancel", "Cancel");

                var result = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
                {
                    Message = message,
                    CheckboxText = checkboxText,
                    Title = title,
                    ConfirmButtonText = continueText,
                    CancelButtonText = cancelText,
                });

                if (result.CheckboxChecked)
                {
                    await _preferencesService.SetPreferenceAsync(
                        UserPreferenceKeys.DontShowFilterExplanation, true);
                }

                if (!result.Confirmed) return false;
            }

            IsFilterEnabled = !IsFilterEnabled;

            await _preferencesService.SetPreferenceAsync(
                UserPreferenceKeys.EnableWindowsVersionFilter,
                IsFilterEnabled);

            _eventBus.Publish(new FilterStateChangedEvent(IsFilterEnabled));

            FilterStateChanged?.Invoke(this, IsFilterEnabled);

            _logService.Log(Core.Features.Common.Enums.LogLevel.Info,
                $"Windows version filter toggled to: {(IsFilterEnabled ? "ON" : "OFF")}");

            return true;
        }
        catch (Exception ex)
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Error,
                $"Failed to toggle Windows version filter: {ex.Message}");
            return false;
        }
    }

    public void ForceFilterOn()
    {
        if (!IsFilterEnabled)
        {
            IsFilterEnabled = true;
            _eventBus.Publish(new FilterStateChangedEvent(true));
            FilterStateChanged?.Invoke(this, true);
        }
    }

    public async Task RestoreFilterPreferenceAsync()
    {
        var savedPreference = await _preferencesService.GetPreferenceAsync(
            UserPreferenceKeys.EnableWindowsVersionFilter, defaultValue: true);
        if (IsFilterEnabled != savedPreference)
        {
            IsFilterEnabled = savedPreference;
            _eventBus.Publish(new FilterStateChangedEvent(savedPreference));
            FilterStateChanged?.Invoke(this, savedPreference);
        }
    }
}

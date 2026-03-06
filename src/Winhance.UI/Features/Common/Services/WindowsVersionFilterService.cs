using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Manages the Windows version filter state, persistence, and review mode interactions.
/// Extracted from MainWindowViewModel for testability.
/// </summary>
public class WindowsVersionFilterService : IWindowsVersionFilterService
{
    private readonly IUserPreferencesService _preferencesService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly IEventBus _eventBus;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;

    public WindowsVersionFilterService(
        IUserPreferencesService preferencesService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IEventBus eventBus,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ILogService logService)
    {
        _preferencesService = preferencesService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _eventBus = eventBus;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _logService = logService;
    }

    /// <inheritdoc />
    public bool IsFilterEnabled { get; private set; } = true;

    /// <inheritdoc />
    public event EventHandler<bool>? FilterStateChanged;

    /// <inheritdoc />
    public async Task LoadFilterPreferenceAsync()
    {
        try
        {
            IsFilterEnabled = await _preferencesService.GetPreferenceAsync(
                UserPreferenceKeys.EnableWindowsVersionFilter, defaultValue: true);

            _compatibleSettingsRegistry.SetFilterEnabled(IsFilterEnabled);

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

    /// <inheritdoc />
    public async Task<bool> ToggleFilterAsync(bool isInReviewMode)
    {
        // Don't allow toggling during review mode
        if (isInReviewMode) return false;

        try
        {
            // Check if we should show explanation dialog
            var dontShowAgain = await _preferencesService.GetPreferenceAsync(
                UserPreferenceKeys.DontShowFilterExplanation, defaultValue: false);

            if (!dontShowAgain)
            {
                var message = _localizationService.GetString("Filter_Dialog_Message") ??
                    "The Windows Version Filter controls which settings are shown based on your Windows version.\n\nWhen ON: Only settings compatible with your Windows version are shown.\nWhen OFF: All settings are shown, with incompatible ones marked.";
                var checkboxText = _localizationService.GetString("Filter_Dialog_Checkbox") ?? "Don't show this message again";
                var title = _localizationService.GetString("Filter_Dialog_Title") ?? "Windows Version Filter";
                var continueText = _localizationService.GetString("Filter_Dialog_Button_Toggle") ?? "Toggle Filter";
                var cancelText = _localizationService.GetString("Button_Cancel") ?? "Cancel";

                var result = await _dialogService.ShowConfirmationWithCheckboxAsync(
                    message,
                    checkboxText: checkboxText,
                    title: title,
                    continueButtonText: continueText,
                    cancelButtonText: cancelText);

                if (result.CheckboxChecked)
                {
                    await _preferencesService.SetPreferenceAsync(
                        UserPreferenceKeys.DontShowFilterExplanation, true);
                }

                if (!result.Confirmed) return false;
            }

            // Toggle state
            IsFilterEnabled = !IsFilterEnabled;

            // Persist preference
            await _preferencesService.SetPreferenceAsync(
                UserPreferenceKeys.EnableWindowsVersionFilter,
                IsFilterEnabled);

            // Update registry filter state
            _compatibleSettingsRegistry.SetFilterEnabled(IsFilterEnabled);

            // Publish event for all subscribers (pages/viewmodels) to refresh
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

    /// <inheritdoc />
    public void ForceFilterOn()
    {
        if (!IsFilterEnabled)
        {
            IsFilterEnabled = true;
            _compatibleSettingsRegistry.SetFilterEnabled(true);
            _eventBus.Publish(new FilterStateChangedEvent(true));
            FilterStateChanged?.Invoke(this, true);
        }
    }

    /// <inheritdoc />
    public async Task RestoreFilterPreferenceAsync()
    {
        var savedPreference = await _preferencesService.GetPreferenceAsync(
            UserPreferenceKeys.EnableWindowsVersionFilter, defaultValue: true);
        if (IsFilterEnabled != savedPreference)
        {
            IsFilterEnabled = savedPreference;
            _compatibleSettingsRegistry.SetFilterEnabled(savedPreference);
            _eventBus.Publish(new FilterStateChangedEvent(savedPreference));
            FilterStateChanged?.Invoke(this, savedPreference);
        }
    }
}

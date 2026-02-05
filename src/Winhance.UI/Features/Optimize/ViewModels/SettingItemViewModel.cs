using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class SettingItemViewModel : BaseViewModel
{
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserPreferencesService? _userPreferencesService;
    private bool _isUpdatingFromEvent;

    public ISettingsFeatureViewModel? ParentFeatureViewModel { get; set; }

    public SettingDefinition? SettingDefinition { get; set; }

    [ObservableProperty]
    private string _settingId = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _groupName = string.Empty;

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private string _iconPack = "Material";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string? _warningText;

    [ObservableProperty]
    private InputType _inputType;

    [ObservableProperty]
    private object? _selectedValue;

    [ObservableProperty]
    private ObservableCollection<ComboBoxOption> _comboBoxOptions = new();

    [ObservableProperty]
    private int _numericValue;

    [ObservableProperty]
    private int _minValue;

    [ObservableProperty]
    private int _maxValue = 100;

    [ObservableProperty]
    private string _units = string.Empty;

    public string OnText { get; set; } = "On";
    public string OffText { get; set; } = "Off";
    public string ActionButtonText { get; set; } = "Apply";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isEnabled = true;

    public ObservableCollection<SettingBadgeInfo> Badges { get; } = new();

    [ObservableProperty]
    private bool _hasBadges;

    [ObservableProperty]
    private GridLength _badgeColumnWidth = new GridLength(0);

    [ObservableProperty]
    private string? _compatibilityMessage;

    // Advanced unlock support
    [ObservableProperty]
    private bool _isLocked;

    public bool RequiresAdvancedUnlock => SettingDefinition?.RequiresAdvancedUnlock == true;
    public string ClickToUnlockText => _localizationService.GetString("Common_ClickToUnlock") ?? "Click to unlock";
    public IAsyncRelayCommand UnlockCommand { get; }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    [ObservableProperty]
    private bool _parentIsEnabled = true;

    partial void OnParentIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    public bool EffectiveIsEnabled => IsEnabled && ParentIsEnabled;
    public bool IsToggleType => InputType == InputType.Toggle;
    public bool IsSelectionType => InputType == InputType.Selection;
    public bool IsNumericType => InputType == InputType.NumericRange;
    public bool IsActionType => InputType == InputType.Action;
    public bool IsCheckBoxType => InputType == InputType.CheckBox;
    public bool IsSubSetting => !string.IsNullOrEmpty(SettingDefinition?.ParentSettingId);
    public bool IsPowerPlanSetting => InputType == InputType.Selection &&
        SettingDefinition?.CustomProperties?.ContainsKey("LoadDynamicOptions") == true;

    public IAsyncRelayCommand ExecuteActionCommand { get; }

    public SettingItemViewModel(
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IUserPreferencesService? userPreferencesService = null)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;

        ExecuteActionCommand = new AsyncRelayCommand(HandleActionAsync);
        UnlockCommand = new AsyncRelayCommand(HandleUnlockAsync);
    }

    public void UpdateVisibility(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            IsVisible = true;
            return;
        }

        var lowerSearch = searchText.ToLowerInvariant();
        IsVisible = Name.ToLowerInvariant().Contains(lowerSearch) ||
                   Description.ToLowerInvariant().Contains(lowerSearch) ||
                   (!string.IsNullOrEmpty(GroupName) && GroupName.ToLowerInvariant().Contains(lowerSearch));
    }

    // Updates setting state from external events (bypasses apply logic since change already happened)
    public void UpdateStateFromEvent(bool isEnabled, object? value)
    {
        _isUpdatingFromEvent = true;
        try
        {
            if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
            {
                IsSelected = isEnabled;
            }
            else if (InputType == InputType.Selection && value != null)
            {
                SelectedValue = value;
            }
            else if (InputType == InputType.NumericRange && value is int intValue)
            {
                NumericValue = intValue;
            }
        }
        finally
        {
            _isUpdatingFromEvent = false;
        }
    }

    #region UI Event Handlers

    public void OnToggleSwitchToggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
            _ = HandleToggleAsync(toggle.IsOn);
    }

    public void OnCheckBoxClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            _ = HandleToggleAsync(checkBox.IsChecked == true);
    }

    // Using DropDownClosed instead of SelectionChanged because SelectionChanged fires during initialization
    public void OnComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedValue is { } value)
            _ = HandleValueChangedAsync(value);
    }

    public void ApplySelectionValue(object value)
    {
        LogToFile($"[SettingItemViewModel] ApplySelectionValue called with value={value}, SettingId={SettingId}");
        _ = HandleValueChangedAsync(value);
    }

    private static void LogToFile(string message)
    {
        try
        {
            var logPath = @"C:\Winhance-UI\src\startup-debug.log";
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
    }

    public void OnNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
            _ = HandleValueChangedAsync((int)e.NewValue);
    }

    #endregion

    #region Apply Logic

    private async Task HandleToggleAsync(bool newValue)
    {
        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;

        if (newValue == IsSelected) return;

        try
        {
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(newValue);
            if (!confirmed)
            {
                OnPropertyChanged(nameof(IsSelected));
                return;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Toggling setting: {SettingId} to {newValue}");

            await _settingApplicationService.ApplySettingAsync(SettingId, newValue, checkboxResult: checkboxChecked);

            IsSelected = newValue;
            _logService.Log(LogLevel.Info, $"Successfully toggled setting {SettingId} to {newValue}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error toggling setting {SettingId}: {ex.Message}");
            OnPropertyChanged(nameof(IsSelected));
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task HandleValueChangedAsync(object? value)
    {
        LogToFile($"[SettingItemViewModel] HandleValueChangedAsync called: value={value}, IsApplying={IsApplying}, SettingDefinition={(SettingDefinition == null ? "null" : "not null")}, SelectedValue={SelectedValue}");

        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null || value == null)
        {
            LogToFile($"[SettingItemViewModel] HandleValueChangedAsync early return: IsApplying={IsApplying}, _isUpdatingFromEvent={_isUpdatingFromEvent}, SettingDefinition={(SettingDefinition == null ? "null" : "not null")}, value={(value == null ? "null" : "not null")}");
            return;
        }

        if (Equals(value, SelectedValue))
        {
            LogToFile($"[SettingItemViewModel] HandleValueChangedAsync: value equals SelectedValue, skipping");
            return;
        }

        LogToFile($"[SettingItemViewModel] HandleValueChangedAsync: proceeding with value change");
        try
        {
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(value);
            if (!confirmed)
            {
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(NumericValue));
                return;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Changing value for setting: {SettingId} to {value}");
            LogToFile($"[SettingItemViewModel] Calling ApplySettingAsync for {SettingId} with value={value}");

            await _settingApplicationService.ApplySettingAsync(SettingId, true, value, checkboxResult: checkboxChecked);

            LogToFile($"[SettingItemViewModel] ApplySettingAsync completed for {SettingId}");

            _selectedValue = value;
            OnPropertyChanged(nameof(SelectedValue));

            if (value is int intValue)
                NumericValue = intValue;

            _logService.Log(LogLevel.Info, $"Successfully changed value for setting {SettingId}");
            LogToFile($"[SettingItemViewModel] SelectedValue set to {value} for {SettingId}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing value for setting {SettingId}: {ex.Message}");
            OnPropertyChanged(nameof(SelectedValue));
            OnPropertyChanged(nameof(NumericValue));
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task HandleActionAsync()
    {
        if (IsApplying || SettingDefinition == null) return;

        try
        {
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(null);
            if (!confirmed)
                return;

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Executing action for setting: {SettingId}");

            await _settingApplicationService.ApplySettingAsync(
                SettingId,
                true,
                value: null,
                checkboxResult: checkboxChecked,
                commandString: SettingDefinition.ActionCommand,
                applyRecommended: checkboxChecked);

            _logService.Log(LogLevel.Info, $"Successfully executed action for setting {SettingId}");

            if (checkboxChecked && ParentFeatureViewModel != null)
            {
                _logService.Log(LogLevel.Info, $"Refreshing parent ViewModel after applying recommended settings for {SettingId}");
                await ParentFeatureViewModel.RefreshSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error executing action for setting {SettingId}: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task<(bool confirmed, bool checkboxChecked)> HandleConfirmationIfNeededAsync(object? value)
    {
        if (SettingDefinition == null || !SettingDefinition.RequiresConfirmation)
            return (true, false);

        var title = _localizationService.GetString($"Setting_{SettingId}_ConfirmTitle");
        var message = _localizationService.GetString($"Setting_{SettingId}_ConfirmMessage");
        var checkboxText = _localizationService.GetString($"Setting_{SettingId}_ConfirmCheckbox");

        if (SettingId == "theme-mode-windows" && value is int comboBoxIndex)
        {
            var themeMode = comboBoxIndex == 1
                ? _localizationService.GetString("Setting_theme-mode-windows_Option_1")
                : _localizationService.GetString("Setting_theme-mode-windows_Option_0");
            message = message.Replace("{themeMode}", themeMode);
            checkboxText = checkboxText.Replace("{themeMode}", themeMode);
        }

        var continueText = _localizationService.GetString("Button_Continue");
        var cancelText = _localizationService.GetString("Button_Cancel");

        return await _dialogService.ShowConfirmationWithCheckboxAsync(
            message,
            checkboxText,
            title,
            continueText,
            cancelText);
    }

    #endregion

    #region Advanced Unlock

    private async Task HandleUnlockAsync()
    {
        if (!IsLocked) return;

        var message = _localizationService.GetString("Dialog_AdvancedPowerWarning_Message");
        var checkboxText = _localizationService.GetString("Dialog_AdvancedPowerWarning_DontShowAgain");
        var title = _localizationService.GetString("Dialog_AdvancedPowerWarning_Title");
        var unlockText = _localizationService.GetString("Button_Unlock") ?? "Unlock";
        var cancelText = _localizationService.GetString("Button_Cancel") ?? "Cancel";

        var (confirmed, dontShowAgain) = await _dialogService.ShowConfirmationWithCheckboxAsync(
            message,
            checkboxText,
            title,
            unlockText,
            cancelText);

        if (!confirmed) return;

        IsLocked = false;
        _logService.Log(LogLevel.Info, $"Unlocked advanced setting: {SettingId}");

        if (dontShowAgain && _userPreferencesService != null)
        {
            await _userPreferencesService.SetPreferenceAsync("AdvancedPowerSettingsUnlocked", true);
            _logService.Log(LogLevel.Info, "User permanently unlocked advanced power settings");

            // Unlock all other advanced settings in the same feature
            if (ParentFeatureViewModel != null)
            {
                foreach (var setting in ParentFeatureViewModel.Settings.OfType<SettingItemViewModel>())
                {
                    if (setting.RequiresAdvancedUnlock && setting != this)
                    {
                        setting.IsLocked = false;
                    }
                }
            }
        }
    }

    #endregion

    #region Badge Support

    public void UpdateBadges(bool showCompatibilityBadges, ILocalizationService? localizationService = null)
    {
        Badges.Clear();

        if (showCompatibilityBadges && SettingDefinition != null)
        {
            if (SettingDefinition.CustomProperties.TryGetValue(
                Core.Features.Common.Constants.CustomPropertyKeys.VersionCompatibilityMessage,
                out var compatMessageObj) && compatMessageObj is string compatMessage)
            {
                var hasBuildRanges = SettingDefinition.SupportedBuildRanges?.Count > 0 ||
                                     SettingDefinition.MinimumBuildNumber.HasValue ||
                                     SettingDefinition.MaximumBuildNumber.HasValue;

                var versionText = SettingDefinition.IsWindows11Only ? "11" :
                                  SettingDefinition.IsWindows10Only ? "10" : "10";

                if (hasBuildRanges)
                    Badges.Add(new SettingBadgeInfo(BadgeType.WinBuild, versionText, compatMessage));
                else if (SettingDefinition.IsWindows10Only)
                    Badges.Add(new SettingBadgeInfo(BadgeType.Win10, "10", compatMessage));
                else if (SettingDefinition.IsWindows11Only)
                    Badges.Add(new SettingBadgeInfo(BadgeType.Win11, "11", compatMessage));

                WarningText = compatMessage;
            }
        }
        else
        {
            WarningText = null;
        }

        HasBadges = Badges.Count > 0;
    }

    #endregion
}

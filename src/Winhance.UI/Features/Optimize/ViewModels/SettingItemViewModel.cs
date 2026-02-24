using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Utilities;
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
    private readonly SettingStatusBannerManager _statusBannerManager;
    private readonly TechnicalDetailsManager _technicalDetailsManager;
    private bool _isUpdatingFromEvent;
    private bool _hasChangedThisSession;

    public ISettingsFeatureViewModel? ParentFeatureViewModel { get; set; }

    public SettingDefinition? SettingDefinition { get; set; }

    [ObservableProperty]
    public partial string SettingId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial string GroupName { get; set; }

    [ObservableProperty]
    public partial string Icon { get; set; }

    [ObservableProperty]
    public partial string IconPack { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsApplying { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string? StatusBannerMessage { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity StatusBannerSeverity { get; set; }

    public bool HasStatusBanner => !string.IsNullOrEmpty(StatusBannerMessage);

    partial void OnStatusBannerMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusBanner));
    }

    [ObservableProperty]
    public partial InputType InputType { get; set; }

    [ObservableProperty]
    public partial object? SelectedValue { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ComboBoxOption> ComboBoxOptions { get; set; }

    [ObservableProperty]
    public partial int NumericValue { get; set; }

    [ObservableProperty]
    public partial int AcValue { get; set; }

    [ObservableProperty]
    public partial int DcValue { get; set; }

    [ObservableProperty]
    public partial int AcNumericValue { get; set; }

    [ObservableProperty]
    public partial int DcNumericValue { get; set; }

    [ObservableProperty]
    public partial bool HasBattery { get; set; }

    [ObservableProperty]
    public partial int MinValue { get; set; }

    [ObservableProperty]
    public partial int MaxValue { get; set; }

    [ObservableProperty]
    public partial string Units { get; set; }

    public string OnText { get; set; } = "On";
    public string OffText { get; set; } = "Off";
    public string ActionButtonText { get; set; } = "Apply";

    // Technical Details panel
    [ObservableProperty]
    public partial bool IsTechnicalDetailsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsTechnicalDetailsGloballyVisible { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<TechnicalDetailRow> TechnicalDetails { get; set; }

    public bool HasTechnicalDetails => TechnicalDetails.Count > 0;

    /// <summary>
    /// Controls visibility of the toggle bar: requires data AND global toggle to be on.
    /// </summary>
    public bool ShowTechnicalDetailsBar => HasTechnicalDetails && IsTechnicalDetailsGloballyVisible;

    /// <summary>
    /// Bottom corners rounded only when the expandable content is collapsed;
    /// when expanded, the content panel below carries the rounded corners.
    /// </summary>
    public Microsoft.UI.Xaml.CornerRadius TechnicalDetailsToggleCornerRadius =>
        IsTechnicalDetailsExpanded
            ? new Microsoft.UI.Xaml.CornerRadius(0)
            : new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4);

    partial void OnIsTechnicalDetailsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(TechnicalDetailsToggleCornerRadius));
    }

    partial void OnIsTechnicalDetailsGloballyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTechnicalDetailsBar));
        if (!value) IsTechnicalDetailsExpanded = false;
    }

    public string TechnicalDetailsLabel =>
        _localizationService.GetString("TechnicalDetails_Toggle") ?? "Technical Details";

    public string OpenRegeditTooltip =>
        _localizationService.GetString("TechnicalDetails_OpenRegedit") ?? "Open in Registry Editor";

    public IRelayCommand<string> OpenRegeditCommand { get; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    // Pre-built message for cross-group child settings (built during initialization)
    public string? CrossGroupInfoMessage { get; set; }

    // Advanced unlock support
    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    public bool RequiresAdvancedUnlock => SettingDefinition?.RequiresAdvancedUnlock == true;
    public string ClickToUnlockText => _localizationService.GetString("Common_ClickToUnlock") ?? "Click to unlock";
    public IAsyncRelayCommand UnlockCommand { get; }

    // Review mode properties
    [ObservableProperty]
    public partial bool IsInReviewMode { get; set; }

    [ObservableProperty]
    public partial bool HasReviewDiff { get; set; }

    [ObservableProperty]
    public partial string? ReviewDiffMessage { get; set; }

    [ObservableProperty]
    public partial bool IsReviewApproved { get; set; }

    [ObservableProperty]
    public partial bool IsReviewRejected { get; set; }

    public bool IsReviewDecisionMade => IsReviewApproved || IsReviewRejected;

    partial void OnIsInReviewModeChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    partial void OnIsReviewApprovedChanged(bool value)
    {
        if (value && IsReviewRejected)
            IsReviewRejected = false;

        OnPropertyChanged(nameof(IsReviewDecisionMade));
        // Notify the ConfigReviewService when approval changes
        ReviewApprovalChanged?.Invoke(this, value);
    }

    partial void OnIsReviewRejectedChanged(bool value)
    {
        if (value && IsReviewApproved)
            IsReviewApproved = false;

        OnPropertyChanged(nameof(IsReviewDecisionMade));
        // When rejecting, notify with approved=false
        if (value)
            ReviewApprovalChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Raised when the user changes the review approval state for this setting.
    /// The ConfigReviewService subscribes to this to update its approval counts.
    /// </summary>
    public event EventHandler<bool>? ReviewApprovalChanged;

    /// <summary>
    /// Clears all review mode state including event handlers.
    /// Used when exiting review mode to ensure clean state for subsequent imports.
    /// </summary>
    public void ClearReviewState()
    {
        IsInReviewMode = false;
        HasReviewDiff = false;
        ReviewDiffMessage = null;
        IsReviewApproved = false;
        IsReviewRejected = false;
        ReviewApprovalChanged = null;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    [ObservableProperty]
    public partial bool ParentIsEnabled { get; set; }

    partial void OnParentIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    public bool EffectiveIsEnabled => IsEnabled && ParentIsEnabled && !IsInReviewMode;
    public bool IsToggleType => InputType == InputType.Toggle;
    public bool IsSelectionType => InputType == InputType.Selection;
    public bool IsNumericType => InputType == InputType.NumericRange;
    public bool IsActionType => InputType == InputType.Action;
    public bool IsCheckBoxType => InputType == InputType.CheckBox;
    public bool IsSubSetting => !string.IsNullOrEmpty(SettingDefinition?.ParentSettingId);
    public bool IsPowerPlanSetting => InputType == InputType.Selection &&
        SettingDefinition?.CustomProperties?.ContainsKey("LoadDynamicOptions") == true;

    public bool SupportsSeparateACDC =>
        SettingDefinition?.PowerCfgSettings?.Any(p =>
            p.PowerModeSupport == PowerModeSupport.Separate) == true;

    public string PluggedInText =>
        _localizationService.GetString("PowerStatus_PluggedIn") ?? "Plugged In";
    public string OnBatteryText =>
        _localizationService.GetString("PowerStatus_OnBattery") ?? "On Battery";

    public IAsyncRelayCommand ExecuteActionCommand { get; }

    public SettingItemViewModel(
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IEventBus? eventBus = null,
        IUserPreferencesService? userPreferencesService = null,
        IRegeditLauncher? regeditLauncher = null)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;

        // Initialize partial property defaults
        SettingId = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        GroupName = string.Empty;
        Icon = string.Empty;
        IconPack = "Material";
        Status = string.Empty;
        ComboBoxOptions = new ObservableCollection<ComboBoxOption>();
        MaxValue = 100;
        Units = string.Empty;
        TechnicalDetails = new ObservableCollection<TechnicalDetailRow>();
        IsVisible = true;
        IsEnabled = true;
        ParentIsEnabled = true;

        ExecuteActionCommand = new AsyncRelayCommand(HandleActionAsync);
        UnlockCommand = new AsyncRelayCommand(HandleUnlockAsync);

        _statusBannerManager = new SettingStatusBannerManager(localizationService);
        _technicalDetailsManager = new TechnicalDetailsManager(
            () => SettingId,
            TechnicalDetails,
            () => { OnPropertyChanged(nameof(HasTechnicalDetails)); OnPropertyChanged(nameof(ShowTechnicalDetailsBar)); },
            logService,
            dispatcherService,
            regeditLauncher,
            eventBus);
        OpenRegeditCommand = _technicalDetailsManager.OpenRegeditCommand;
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

    // Updates setting state from a fresh system state read (used during navigation refresh)
    public void UpdateStateFromSystemState(SettingStateResult state)
    {
        if (!state.Success) return;
        _isUpdatingFromEvent = true;
        try
        {
            switch (InputType)
            {
                case InputType.Toggle:
                case InputType.CheckBox:
                    IsSelected = state.IsEnabled;
                    break;
                case InputType.Selection:
                    if (SupportsSeparateACDC && state.RawValues != null &&
                        SettingDefinition?.CustomProperties?.TryGetValue(
                            CustomPropertyKeys.ValueMappings, out var mappingsObj) == true)
                    {
                        var mappings = (Dictionary<int, Dictionary<string, object?>>)mappingsObj;
                        if (state.RawValues.TryGetValue("ACValue", out var acRaw) && acRaw != null)
                            AcValue = FindIndexForPowerCfgValue(mappings, Convert.ToInt32(acRaw));
                        if (state.RawValues.TryGetValue("DCValue", out var dcRaw) && dcRaw != null)
                            DcValue = FindIndexForPowerCfgValue(mappings, Convert.ToInt32(dcRaw));
                    }
                    else if (state.CurrentValue != null)
                    {
                        SelectedValue = state.CurrentValue;
                    }
                    break;
                case InputType.NumericRange:
                    if (SupportsSeparateACDC && state.RawValues != null)
                    {
                        if (state.RawValues.TryGetValue("ACValue", out var acNum) && acNum is int acInt)
                            AcNumericValue = ConvertFromSystemUnits(acInt);
                        if (state.RawValues.TryGetValue("DCValue", out var dcNum) && dcNum is int dcInt)
                            DcNumericValue = ConvertFromSystemUnits(dcInt);
                    }
                    else if (state.CurrentValue is int intValue)
                    {
                        NumericValue = ConvertFromSystemUnits(intValue);
                    }
                    break;
            }
        }
        finally
        {
            _isUpdatingFromEvent = false;
        }
    }

    private static int FindIndexForPowerCfgValue(Dictionary<int, Dictionary<string, object?>> mappings, int targetValue)
    {
        foreach (var mapping in mappings)
        {
            if (mapping.Value.TryGetValue("PowerCfgValue", out var val) && val != null && Convert.ToInt32(val) == targetValue)
                return mapping.Key;
        }
        return 0;
    }

    private int ConvertFromSystemUnits(int systemValue)
    {
        var displayUnits = SettingDefinition?.CustomProperties?.TryGetValue("Units", out var units) == true && units is string unitsStr
            ? unitsStr
            : null;
        return UnitConversionHelper.ConvertFromSystemUnits(systemValue, displayUnits);
    }

    #region UI Event Handlers

    public void OnToggleSwitchToggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
            HandleToggleAsync(toggle.IsOn).FireAndForget(_logService);
    }

    public void OnCheckBoxClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            HandleToggleAsync(checkBox.IsChecked == true).FireAndForget(_logService);
    }

    // Announce ComboBox option changes for screen readers (arrow key navigation on closed ComboBox)
    public void OnComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Only announce when the user is actively interacting (keyboard-focused), not during init
        if (sender is not ComboBox comboBox || comboBox.FocusState == Microsoft.UI.Xaml.FocusState.Unfocused)
            return;

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxOption option)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(comboBox)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(comboBox);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.CurrentThenMostRecent,
                option.DisplayText,
                "ComboBoxSelection");
        }
    }

    // Using DropDownClosed instead of SelectionChanged because SelectionChanged fires during initialization
    public void OnComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedValue is { } value)
            HandleValueChangedAsync(value).FireAndForget(_logService);
    }

    public void ApplySelectionValue(object value)
    {
        _logService.LogDebug($"[SettingItemViewModel] ApplySelectionValue called with value={value}, SettingId={SettingId}");
        HandleValueChangedAsync(value).FireAndForget(_logService);
    }

    public void OnNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
            HandleValueChangedAsync((int)e.NewValue).FireAndForget(_logService);
    }

    public void OnACComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0)
        {
            AcValue = cb.SelectedIndex;
            HandleACDCSelectionChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnDCComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0)
        {
            DcValue = cb.SelectedIndex;
            HandleACDCSelectionChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnACNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
        {
            AcNumericValue = (int)e.NewValue;
            HandleACDCNumericChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnDCNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
        {
            DcNumericValue = (int)e.NewValue;
            HandleACDCNumericChangedAsync().FireAndForget(_logService);
        }
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

            await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = newValue, CheckboxResult = checkboxChecked });

            IsSelected = newValue;
            _hasChangedThisSession = true;
            ShowRestartBannerIfNeeded();
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
        _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync called: value={value}, IsApplying={IsApplying}, SettingDefinition={(SettingDefinition == null ? "null" : "not null")}, SelectedValue={SelectedValue}");

        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null || value == null)
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync early return: IsApplying={IsApplying}, _isUpdatingFromEvent={_isUpdatingFromEvent}, SettingDefinition={(SettingDefinition == null ? "null" : "not null")}, value={(value == null ? "null" : "not null")}");
            return;
        }

        if (Equals(value, SelectedValue))
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync: value equals SelectedValue, skipping");
            return;
        }

        _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync: proceeding with value change");
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
            _logService.LogDebug($"[SettingItemViewModel] Calling ApplySettingAsync for {SettingId} with value={value}");

            await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = value, CheckboxResult = checkboxChecked });

            _logService.LogDebug($"[SettingItemViewModel] ApplySettingAsync completed for {SettingId}");

            SelectedValue = value;

            if (value is int intValue)
                NumericValue = intValue;

            _hasChangedThisSession = true;
            UpdateStatusBanner(value);
            ShowRestartBannerIfNeeded();

            _logService.Log(LogLevel.Info, $"Successfully changed value for setting {SettingId}");
            _logService.LogDebug($"[SettingItemViewModel] SelectedValue set to {value} for {SettingId}");
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

    private async Task HandleACDCSelectionChangedAsync()
    {
        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;

        try
        {
            IsApplying = true;
            var dict = new Dictionary<string, object?> { ["ACValue"] = AcValue, ["DCValue"] = DcValue };
            _logService.Log(LogLevel.Info, $"Changing AC/DC selection for setting: {SettingId} AC={AcValue}, DC={DcValue}");
            await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict });
            _hasChangedThisSession = true;
            ShowRestartBannerIfNeeded();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing AC/DC selection for setting {SettingId}: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task HandleACDCNumericChangedAsync()
    {
        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;

        try
        {
            IsApplying = true;
            var dict = new Dictionary<string, object?> { ["ACValue"] = AcNumericValue, ["DCValue"] = DcNumericValue };
            _logService.Log(LogLevel.Info, $"Changing AC/DC numeric for setting: {SettingId} AC={AcNumericValue}, DC={DcNumericValue}");
            await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict });
            _hasChangedThisSession = true;
            ShowRestartBannerIfNeeded();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing AC/DC numeric for setting {SettingId}: {ex.Message}");
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

            await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest
            {
                SettingId = SettingId,
                Enable = true,
                CheckboxResult = checkboxChecked,
                CommandString = SettingDefinition.ActionCommand,
                ApplyRecommended = checkboxChecked
            });

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

    #region Status Banner

    public void InitializeCompatibilityBanner()
    {
        var banner = _statusBannerManager.GetCompatibilityBanner(SettingDefinition);
        if (banner.HasValue) ApplyBanner(banner.Value);
    }

    public void UpdateStatusBanner(object? value)
    {
        var banner = _statusBannerManager.ComputeBannerForValue(SettingDefinition, value, CrossGroupInfoMessage);
        if (banner.HasValue) ApplyBanner(banner.Value);
    }

    private void ShowRestartBannerIfNeeded()
    {
        var banner = _statusBannerManager.GetRestartBanner(SettingDefinition, _hasChangedThisSession);
        if (banner.HasValue) ApplyBanner(banner.Value);
    }

    private void ApplyBanner(SettingStatusBannerManager.BannerState state)
    {
        StatusBannerMessage = state.Message;
        StatusBannerSeverity = state.Severity;
    }

    #endregion

    #region Technical Details

    public void ToggleTechnicalDetails() => IsTechnicalDetailsExpanded = !IsTechnicalDetailsExpanded;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _technicalDetailsManager.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}

using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
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
    private readonly IEventBus? _eventBus;
    private ISubscriptionToken? _tooltipUpdatedSubscription;
    private bool _isUpdatingFromEvent;
    private bool _hasChangedThisSession;

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
    private string? _statusBannerMessage;

    [ObservableProperty]
    private InfoBarSeverity _statusBannerSeverity = InfoBarSeverity.Informational;

    public bool HasStatusBanner => !string.IsNullOrEmpty(StatusBannerMessage);

    partial void OnStatusBannerMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusBanner));
    }

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

    // Technical Details panel
    [ObservableProperty]
    private bool _isTechnicalDetailsExpanded;

    [ObservableProperty]
    private bool _isTechnicalDetailsGloballyVisible;

    [ObservableProperty]
    private ObservableCollection<TechnicalDetailRow> _technicalDetails = new();

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
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isEnabled = true;

    // Pre-built message for cross-group child settings (built during initialization)
    public string? CrossGroupInfoMessage { get; set; }

    // Advanced unlock support
    [ObservableProperty]
    private bool _isLocked;

    public bool RequiresAdvancedUnlock => SettingDefinition?.RequiresAdvancedUnlock == true;
    public string ClickToUnlockText => _localizationService.GetString("Common_ClickToUnlock") ?? "Click to unlock";
    public IAsyncRelayCommand UnlockCommand { get; }

    // Review mode properties
    [ObservableProperty]
    private bool _isInReviewMode;

    [ObservableProperty]
    private bool _hasReviewDiff;

    [ObservableProperty]
    private string? _reviewDiffMessage;

    [ObservableProperty]
    private bool _isReviewApproved = false;

    [ObservableProperty]
    private bool _isReviewRejected = false;

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
    private bool _parentIsEnabled = true;

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

    public IAsyncRelayCommand ExecuteActionCommand { get; }

    public SettingItemViewModel(
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IEventBus? eventBus = null,
        IUserPreferencesService? userPreferencesService = null)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _eventBus = eventBus;
        _userPreferencesService = userPreferencesService;

        ExecuteActionCommand = new AsyncRelayCommand(HandleActionAsync);
        UnlockCommand = new AsyncRelayCommand(HandleUnlockAsync);
        OpenRegeditCommand = new RelayCommand<string>(OpenRegeditAtPath);

        if (_eventBus != null)
        {
            _tooltipUpdatedSubscription = _eventBus.Subscribe<TooltipUpdatedEvent>(OnTooltipUpdated);
        }
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
                    if (state.CurrentValue != null)
                        SelectedValue = state.CurrentValue;
                    break;
                case InputType.NumericRange:
                    if (state.CurrentValue is int intValue)
                        NumericValue = ConvertFromSystemUnits(intValue);
                    break;
            }
        }
        finally
        {
            _isUpdatingFromEvent = false;
        }
    }

    private int ConvertFromSystemUnits(int systemValue)
    {
        var displayUnits = SettingDefinition?.CustomProperties?.TryGetValue("Units", out var units) == true && units is string unitsStr
            ? unitsStr
            : null;

        return displayUnits?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            "milliseconds" => systemValue * 1000,
            _ => systemValue
        };
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
            _ = HandleValueChangedAsync(value);
    }

    public void ApplySelectionValue(object value)
    {
        _logService.LogDebug($"[SettingItemViewModel] ApplySelectionValue called with value={value}, SettingId={SettingId}");
        _ = HandleValueChangedAsync(value);
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

            await _settingApplicationService.ApplySettingAsync(SettingId, true, value, checkboxResult: checkboxChecked);

            _logService.LogDebug($"[SettingItemViewModel] ApplySettingAsync completed for {SettingId}");

            _selectedValue = value;
            OnPropertyChanged(nameof(SelectedValue));

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

    #region Status Banner Messages

    // Initializes the compatibility banner from SettingDefinition (called once during loading)
    public void InitializeCompatibilityBanner()
    {
        if (SettingDefinition?.CustomProperties?.TryGetValue(
            CustomPropertyKeys.VersionCompatibilityMessage, out var compatMessage) == true &&
            compatMessage is string messageText)
        {
            StatusBannerMessage = messageText;
            StatusBannerSeverity = InfoBarSeverity.Warning;
        }
    }

    // Updates status banner based on selected value, option warnings, or cross-group settings
    public void UpdateStatusBanner(object? value)
    {
        if (SettingDefinition == null || value is not int selectedIndex)
        {
            // Keep existing compatibility banner if present, otherwise clear
            if (SettingDefinition?.CustomProperties?.ContainsKey(CustomPropertyKeys.VersionCompatibilityMessage) != true)
            {
                ClearStatusBanner();
            }
            return;
        }

        // Check for option-specific warnings (e.g., update policy security warnings)
        if (SettingDefinition.CustomProperties?.TryGetValue(CustomPropertyKeys.OptionWarnings, out var warnings) == true &&
            warnings is Dictionary<int, string> warningDict &&
            warningDict.TryGetValue(selectedIndex, out var warning))
        {
            StatusBannerMessage = warning;
            StatusBannerSeverity = InfoBarSeverity.Error;
            return;
        }

        // Check for cross-group child settings info (privacy promotional banner)
        if (SettingDefinition.CustomProperties?.ContainsKey(CustomPropertyKeys.CrossGroupChildSettings) == true)
        {
            UpdateCrossGroupInfoMessage(selectedIndex);
            return;
        }

        // No option-specific warning - check if we should keep compatibility message
        if (SettingDefinition.CustomProperties?.TryGetValue(CustomPropertyKeys.VersionCompatibilityMessage, out var compatMessage) == true &&
            compatMessage is string messageText)
        {
            StatusBannerMessage = messageText;
            StatusBannerSeverity = InfoBarSeverity.Warning;
        }
        else
        {
            ClearStatusBanner();
        }
    }

    // Shows informational message for cross-group child settings when "Custom" is selected
    private void UpdateCrossGroupInfoMessage(int selectedIndex)
    {
        var displayNames = SettingDefinition?.CustomProperties?.TryGetValue(CustomPropertyKeys.ComboBoxDisplayNames, out var names) == true
            ? names as string[]
            : null;

        if (displayNames == null)
        {
            ClearStatusBanner();
            return;
        }

        // Check if "Custom" option is selected (last index or special custom state index)
        var customOptionIndex = displayNames.Length - 1;
        bool isCustomState = selectedIndex == customOptionIndex || selectedIndex == ComboBoxResolver.CUSTOM_STATE_INDEX;

        if (!isCustomState)
        {
            ClearStatusBanner();
            return;
        }

        // Use the pre-built message if available (built during initialization with full grouping)
        if (!string.IsNullOrEmpty(CrossGroupInfoMessage))
        {
            StatusBannerMessage = CrossGroupInfoMessage;
            StatusBannerSeverity = InfoBarSeverity.Warning;
            return;
        }

        // Fallback: just show the header if pre-built message not available
        var header = _localizationService.GetString("Setting_CrossGroupWarning_Header");
        if (!string.IsNullOrEmpty(header))
        {
            StatusBannerMessage = header;
            StatusBannerSeverity = InfoBarSeverity.Warning;
        }
    }

    // Shows restart required banner after a setting that requires restart is changed
    private void ShowRestartBannerIfNeeded()
    {
        if (!_hasChangedThisSession)
            return;

        if (SettingDefinition?.RequiresRestart == true)
        {
            StatusBannerMessage = _localizationService.GetString("Common_RestartRequired");
            StatusBannerSeverity = InfoBarSeverity.Warning;
        }
    }

    private void ClearStatusBanner()
    {
        StatusBannerMessage = null;
        StatusBannerSeverity = InfoBarSeverity.Informational;
    }

    #endregion

    #region Technical Details

    public void ToggleTechnicalDetails() => IsTechnicalDetailsExpanded = !IsTechnicalDetailsExpanded;

    private void OnTooltipUpdated(TooltipUpdatedEvent evt)
    {
        if (evt.SettingId != SettingId) return;
        _dispatcherService.RunOnUIThread(() => UpdateTechnicalDetails(evt.TooltipData));
    }

    private void UpdateTechnicalDetails(SettingTooltipData tooltipData)
    {
        TechnicalDetails.Clear();

        // Registry rows
        foreach (var kvp in tooltipData.IndividualRegistryValues)
        {
            var reg = kvp.Key;
            TechnicalDetails.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.Registry,
                RegistryPath = reg.KeyPath,
                ValueName = reg.ValueName ?? "(Default)",
                ValueType = reg.ValueType.ToString(),
                CurrentValue = kvp.Value ?? "(not set)",
                RecommendedValue = reg.RecommendedValue?.ToString() ?? "",
                OpenRegeditCommand = OpenRegeditCommand,
                RegeditIconSource = RegeditIconProvider.CachedIcon,
                CanOpenRegedit = RegeditLauncher.KeyExists(reg.KeyPath)
            });
        }

        // Scheduled task rows
        foreach (var task in tooltipData.ScheduledTaskSettings)
        {
            TechnicalDetails.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.ScheduledTask,
                TaskPath = task.TaskPath,
                RecommendedState = task.RecommendedState == true ? "Enabled" : "Disabled"
            });
        }

        // Power config rows
        foreach (var pcfg in tooltipData.PowerCfgSettings)
        {
            TechnicalDetails.Add(new TechnicalDetailRow
            {
                RowType = DetailRowType.PowerConfig,
                SubgroupGuid = pcfg.SubgroupGuid,
                SettingGuid = pcfg.SettingGuid,
                SubgroupAlias = pcfg.SubgroupGUIDAlias ?? "",
                SettingAlias = pcfg.SettingGUIDAlias,
                PowerUnits = pcfg.Units ?? "",
                RecommendedAC = pcfg.RecommendedValueAC?.ToString() ?? "",
                RecommendedDC = pcfg.RecommendedValueDC?.ToString() ?? ""
            });
        }

        OnPropertyChanged(nameof(HasTechnicalDetails));
        OnPropertyChanged(nameof(ShowTechnicalDetailsBar));
    }

    private void OpenRegeditAtPath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
            RegeditLauncher.OpenAtPath(path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tooltipUpdatedSubscription?.Dispose();
            _tooltipUpdatedSubscription = null;
        }
        base.Dispose(disposing);
    }

    #endregion
}

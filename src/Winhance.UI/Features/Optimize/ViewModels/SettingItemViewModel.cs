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
using ComboBoxOption = Winhance.Core.Features.Common.Interfaces.ComboBoxOption;
using Winhance.UI.Features.Common.Constants;
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
    private readonly INewBadgeService? _newBadgeService;
    private readonly SettingStatusBannerManager _statusBannerManager;
    private readonly TechnicalDetailsManager _technicalDetailsManager;
    private volatile bool _isUpdatingFromEvent;
    private bool _hasChangedThisSession;
    private object? _pendingValue;

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
        _localizationService.GetString("View_TechnicalDetails") ?? "Technical Details";

    public string OpenRegeditTooltip =>
        _localizationService.GetString("TechnicalDetails_OpenRegedit") ?? "Open in Registry Editor";

    public IRelayCommand<string> OpenRegeditCommand { get; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    // Pre-built message for cross-group child settings (built during initialization)
    public string? CrossGroupInfoMessage { get; set; }

    // New setting badge
    [ObservableProperty]
    public partial bool IsNew { get; set; }

    public string NewBadgeText => _localizationService.GetString("Badge_New") ?? "NEW";
    public string NewBadgeDismissTooltip => _localizationService.GetString("Badge_New_Dismiss") ?? "Dismiss";

    public IRelayCommand DismissNewBadgeCommand { get; private set; } = null!;

    // InfoBadge properties
    [ObservableProperty]
    public partial bool IsInfoBadgeGloballyVisible { get; set; }

    [ObservableProperty]
    public partial SettingBadgeState BadgeState { get; set; }

    /// <summary>
    /// True if the setting has RecommendedValue/DefaultValue data to compare against.
    /// False for settings using NativePowerApiSettings, PowerShellScripts, or RegContents only.
    /// </summary>
    public bool HasBadgeData { get; set; }

    public bool ShowInfoBadge => IsInfoBadgeGloballyVisible && HasBadgeData;

    /// <summary>
    /// Localized short label shown inside the badge pill ("Recommended", "Default", "Custom").
    /// </summary>
    public string BadgeLabel => BadgeState switch
    {
        SettingBadgeState.Recommended => _localizationService?.GetString(StringKeys.InfoBadge.Recommended) ?? "Recommended",
        SettingBadgeState.Default => _localizationService?.GetString(StringKeys.InfoBadge.Default) ?? "Default",
        SettingBadgeState.Custom => _localizationService?.GetString(StringKeys.InfoBadge.Custom) ?? "Custom",
        _ => ""
    };

    /// <summary>
    /// Localized long-form tooltip shown when hovering the badge pill.
    /// </summary>
    public string BadgeTooltip => BadgeState switch
    {
        SettingBadgeState.Recommended => _localizationService?.GetString(StringKeys.InfoBadge.RecommendedTooltip) ?? "The Recommended values are applied for this setting",
        SettingBadgeState.Default => _localizationService?.GetString(StringKeys.InfoBadge.DefaultTooltip) ?? "The Default Windows values are applied for this setting",
        SettingBadgeState.Custom => _localizationService?.GetString(StringKeys.InfoBadge.CustomTooltip) ?? "Custom values are applied for this setting",
        _ => ""
    };

    partial void OnIsInfoBadgeGloballyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInfoBadge));
    }

    partial void OnBadgeStateChanged(SettingBadgeState value)
    {
        OnPropertyChanged(nameof(BadgeLabel));
        OnPropertyChanged(nameof(BadgeTooltip));
    }

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

    // Review action properties (for action settings like wallpaper that appear alongside a diff)
    [ObservableProperty]
    public partial bool HasReviewAction { get; set; }

    [ObservableProperty]
    public partial string? ReviewActionMessage { get; set; }

    [ObservableProperty]
    public partial bool IsReviewActionApproved { get; set; }

    [ObservableProperty]
    public partial bool IsReviewActionRejected { get; set; }

    public bool IsReviewActionDecisionMade => IsReviewActionApproved || IsReviewActionRejected;

    public string ReviewActionGroupName => $"{SettingId}_action";

    /// <summary>
    /// Raised when the user changes the review action approval state.
    /// </summary>
    public event EventHandler<bool>? ReviewActionApprovalChanged;

    partial void OnIsReviewActionApprovedChanged(bool value)
    {
        if (value && IsReviewActionRejected)
            IsReviewActionRejected = false;

        OnPropertyChanged(nameof(IsReviewActionDecisionMade));
        ReviewActionApprovalChanged?.Invoke(this, value);
    }

    partial void OnIsReviewActionRejectedChanged(bool value)
    {
        if (value && IsReviewActionApproved)
            IsReviewActionApproved = false;

        OnPropertyChanged(nameof(IsReviewActionDecisionMade));
        if (value)
            ReviewActionApprovalChanged?.Invoke(this, false);
    }

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
    /// Nulls event handler first to prevent stale notifications during property resets.
    /// </summary>
    public void ClearReviewState()
    {
        // Clear event handler BEFORE resetting properties to prevent
        // OnIsReviewApprovedChanged/OnIsReviewRejectedChanged from
        // invoking stale subscribers during cleanup.
        ReviewApprovalChanged = null;
        ReviewActionApprovalChanged = null;

        IsInReviewMode = false;
        HasReviewDiff = false;
        ReviewDiffMessage = null;
        IsReviewApproved = false;
        IsReviewRejected = false;
        HasReviewAction = false;
        ReviewActionMessage = null;
        IsReviewActionApproved = false;
        IsReviewActionRejected = false;
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

    [ObservableProperty]
    public partial ObservableCollection<SettingItemViewModel>? Children { get; set; }

    public bool IsParentSetting => Children != null && Children.Count > 0;

    [ObservableProperty]
    public partial bool IsExpanderExpanded { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLastChild { get; set; }

    public Microsoft.UI.Xaml.CornerRadius ChildCornerRadius =>
        IsLastChild ? new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4) : new Microsoft.UI.Xaml.CornerRadius(0);

    partial void OnIsLastChildChanged(bool value) => OnPropertyChanged(nameof(ChildCornerRadius));

    public void ToggleExpander(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => IsExpanderExpanded = !IsExpanderExpanded;

    public bool IsPowerPlanSetting => InputType == InputType.Selection &&
        SettingDefinition?.Recommendation?.LoadDynamicOptions == true;

    public bool SupportsSeparateACDC =>
        SettingDefinition?.PowerCfgSettings?.Any(p =>
            p.PowerModeSupport == PowerModeSupport.Separate) == true;

    public string PluggedInText =>
        _localizationService.GetString("PowerStatus_PluggedIn") ?? "Plugged In";
    public string OnBatteryText =>
        _localizationService.GetString("PowerStatus_OnBattery") ?? "On Battery";

    public IAsyncRelayCommand ExecuteActionCommand { get; }

    public SettingItemViewModel(
        SettingItemViewModelConfig config,
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IEventBus? eventBus = null,
        IUserPreferencesService? userPreferencesService = null,
        IRegeditLauncher? regeditLauncher = null,
        INewBadgeService? newBadgeService = null)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _newBadgeService = newBadgeService;

        _localizationService.LanguageChanged += OnLanguageChanged;

        // Unpack config data
        SettingDefinition = config.SettingDefinition;
        ParentFeatureViewModel = config.ParentFeatureViewModel;
        SettingId = config.SettingId;
        Name = config.Name;
        Description = config.Description;
        GroupName = config.GroupName;
        Icon = config.Icon;
        IconPack = config.IconPack;
        InputType = config.InputType;
        IsSelected = config.IsSelected;
        OnText = config.OnText;
        OffText = config.OffText;
        ActionButtonText = config.ActionButtonText;

        // Initialize remaining defaults
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
        DismissNewBadgeCommand = new RelayCommand(() =>
        {
            IsNew = false;
            _newBadgeService?.DismissBadge(SettingId);
        });

        // Check if this setting is new in the current release
        IsNew = _newBadgeService?.IsSettingNew(
            config.SettingDefinition?.AddedInVersion, config.SettingId) == true;

        _statusBannerManager = new SettingStatusBannerManager(localizationService);
        _technicalDetailsManager = new TechnicalDetailsManager(
            () => SettingId,
            newDetails => { TechnicalDetails = newDetails; OnPropertyChanged(nameof(HasTechnicalDetails)); OnPropertyChanged(nameof(ShowTechnicalDetailsBar)); },
            logService,
            dispatcherService,
            regeditLauncher,
            eventBus,
            new TechnicalDetailLabels
            {
                Path = _localizationService.GetString("TechnicalDetails_Path") ?? "Path",
                Value = _localizationService.GetString("TechnicalDetails_Value") ?? "Value",
                Current = _localizationService.GetString("TechnicalDetails_Current") ?? "Current",
                Recommended = _localizationService.GetString("TechnicalDetails_Recommended") ?? "Recommended",
                Default = _localizationService.GetString("TechnicalDetails_DefaultValue") ?? "Default",
                ValueNotExist = _localizationService.GetString("TechnicalDetails_ValueNotExist") ?? "doesn't exist",
                On = _localizationService.GetString("Common_On") ?? "On",
                Off = _localizationService.GetString("Common_Off") ?? "Off"
            });
        OpenRegeditCommand = _technicalDetailsManager.OpenRegeditCommand;

        // Initialize badge data availability and compute initial state
        InitializeHasBadgeData();
        ComputeBadgeState();
    }

    public void UpdateVisibility(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            IsVisible = true;
            return;
        }

        IsVisible = Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(GroupName) && GroupName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
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
            ComputeBadgeState();
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
                        SettingDefinition?.ComboBox?.ValueMappings is { } mappings)
                    {
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
            ComputeBadgeState();
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
        var displayUnits = SettingDefinition?.NumericRange?.Units;
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

            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = newValue, CheckboxResult = checkboxChecked });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' apply failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(IsSelected));
                return;
            }

            IsSelected = newValue;
            _hasChangedThisSession = true;
            ComputeBadgeState();
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

        if (_isUpdatingFromEvent || SettingDefinition == null || value == null)
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync early return: _isUpdatingFromEvent={_isUpdatingFromEvent}, SettingDefinition={(SettingDefinition == null ? "null" : "not null")}, value={(value == null ? "null" : "not null")}");
            return;
        }

        // Queue the value if another apply is in progress instead of dropping it
        if (IsApplying)
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync: queuing pending value {value} for {SettingId}");
            _pendingValue = value;
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

            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = value, CheckboxResult = checkboxChecked });

            _logService.LogDebug($"[SettingItemViewModel] ApplySettingAsync completed for {SettingId}");

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' value change failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(NumericValue));
                return;
            }

            SelectedValue = value;

            if (value is int intValue)
            {
                NumericValue = intValue;

                // Remove the Custom option once the user picks a defined value
                if (intValue != ComboBoxConstants.CustomStateIndex)
                {
                    var customOption = ComboBoxOptions.FirstOrDefault(
                        o => o.Value is int v && v == ComboBoxConstants.CustomStateIndex);
                    if (customOption != null)
                        ComboBoxOptions.Remove(customOption);
                }
            }

            _hasChangedThisSession = true;
            ComputeBadgeState();
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
            await ProcessPendingValueAsync();
        }
    }

    /// <summary>
    /// If a value change was queued while a previous apply was in progress,
    /// drain and apply it now.
    /// </summary>
    private async Task ProcessPendingValueAsync()
    {
        var pending = _pendingValue;
        _pendingValue = null;

        if (pending != null && !Equals(pending, SelectedValue))
        {
            _logService.LogDebug($"[SettingItemViewModel] Processing pending value {pending} for {SettingId}");
            await HandleValueChangedAsync(pending);
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
            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' AC/DC selection failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(AcValue));
                OnPropertyChanged(nameof(DcValue));
                return;
            }

            _hasChangedThisSession = true;
            ComputeBadgeState();
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
            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' AC/DC numeric failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(AcNumericValue));
                OnPropertyChanged(nameof(DcNumericValue));
                return;
            }

            _hasChangedThisSession = true;
            ComputeBadgeState();
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

        if (SettingId == SettingIds.ThemeModeWindows && value is int comboBoxIndex)
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

    #region InfoBadge State Computation

    /// <summary>
    /// Computes the badge state by comparing the current UI state against
    /// recommended and default values from the SettingDefinition.
    /// </summary>
    public void ComputeBadgeState()
    {
        if (!HasBadgeData || SettingDefinition == null)
            return;

        bool matchesRecommended = true;
        bool matchesDefault = true;

        // Check RegistrySettings
        foreach (var reg in SettingDefinition.RegistrySettings)
        {
            // For Selection settings, Recommended/Default live on ComboBoxMetadata.Options[i],
            // so reg.RecommendedValue/DefaultValue are expected to be null — don't skip.
            if (reg.RecommendedValue == null && reg.DefaultValue == null
                && InputType != InputType.Selection)
                continue;

            var (currentMatchesRecommended, currentMatchesDefault) = EvaluateRegistrySetting(reg);
            if (!currentMatchesRecommended) matchesRecommended = false;
            if (!currentMatchesDefault) matchesDefault = false;
        }

        // Check ScheduledTaskSettings
        foreach (var task in SettingDefinition.ScheduledTaskSettings)
        {
            if (task.RecommendedState.HasValue)
            {
                // For tasks, recommended typically means disabled (IsSelected=true means the optimization is on,
                // which disables the task). The RecommendedState represents whether the task should be enabled.
                bool currentTaskEnabled = !IsSelected; // Toggle ON = task disabled
                if (currentTaskEnabled != task.RecommendedState.Value)
                    matchesRecommended = false;
            }

            if (task.DefaultState.HasValue)
            {
                bool currentTaskEnabled = !IsSelected;
                if (currentTaskEnabled != task.DefaultState.Value)
                    matchesDefault = false;
            }
        }

        // Check PowerCfgSettings
        if (SettingDefinition.PowerCfgSettings != null)
        {
            foreach (var pcfg in SettingDefinition.PowerCfgSettings)
            {
                if (pcfg.PowerModeSupport == PowerModeSupport.Separate)
                {
                    if (InputType == InputType.Selection)
                    {
                        // AC/DC selection - compare indices against recommended/default PowerCfg values
                        if (pcfg.RecommendedValueAC.HasValue || pcfg.RecommendedValueDC.HasValue)
                        {
                            if (pcfg.RecommendedValueAC.HasValue && !PowerCfgIndexMatchesValue(AcValue, pcfg.RecommendedValueAC.Value))
                                matchesRecommended = false;
                            if (pcfg.RecommendedValueDC.HasValue && !PowerCfgIndexMatchesValue(DcValue, pcfg.RecommendedValueDC.Value))
                                matchesRecommended = false;
                        }
                        if (pcfg.DefaultValueAC.HasValue || pcfg.DefaultValueDC.HasValue)
                        {
                            if (pcfg.DefaultValueAC.HasValue && !PowerCfgIndexMatchesValue(AcValue, pcfg.DefaultValueAC.Value))
                                matchesDefault = false;
                            if (pcfg.DefaultValueDC.HasValue && !PowerCfgIndexMatchesValue(DcValue, pcfg.DefaultValueDC.Value))
                                matchesDefault = false;
                        }
                    }
                    else if (InputType == InputType.NumericRange)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && AcNumericValue != pcfg.RecommendedValueAC.Value)
                            matchesRecommended = false;
                        if (pcfg.RecommendedValueDC.HasValue && DcNumericValue != pcfg.RecommendedValueDC.Value)
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && AcNumericValue != pcfg.DefaultValueAC.Value)
                            matchesDefault = false;
                        if (pcfg.DefaultValueDC.HasValue && DcNumericValue != pcfg.DefaultValueDC.Value)
                            matchesDefault = false;
                    }
                }
                else
                {
                    // Non-separate AC/DC: use the AC value as the single value
                    if (InputType == InputType.NumericRange)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && NumericValue != pcfg.RecommendedValueAC.Value)
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && NumericValue != pcfg.DefaultValueAC.Value)
                            matchesDefault = false;
                    }
                    else if (InputType == InputType.Selection)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && SelectedValue is int selVal && selVal != pcfg.RecommendedValueAC.Value)
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && SelectedValue is int selVal2 && selVal2 != pcfg.DefaultValueAC.Value)
                            matchesDefault = false;
                    }
                }
            }
        }

        if (matchesRecommended)
            BadgeState = SettingBadgeState.Recommended;
        else if (matchesDefault)
            BadgeState = SettingBadgeState.Default;
        else
            BadgeState = SettingBadgeState.Custom;
    }

    private (bool matchesRecommended, bool matchesDefault) EvaluateRegistrySetting(RegistrySetting reg)
    {
        bool matchesRecommended = true;
        bool matchesDefault = true;

        if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
        {
            // Determine what value is currently "set" based on IsSelected
            // IsSelected=true means EnabledValue is active, false means DisabledValue is active
            if (reg.RecommendedValue != null)
            {
                bool recommendedIsEnabled = IsValueInArray(reg.RecommendedValue, reg.EnabledValue);
                bool recommendedIsDisabled = IsValueInArray(reg.RecommendedValue, reg.DisabledValue);

                if (recommendedIsEnabled)
                    matchesRecommended = IsSelected;
                else if (recommendedIsDisabled)
                    matchesRecommended = !IsSelected;
                else
                    matchesRecommended = false;
            }
            else
            {
                matchesRecommended = false;
            }

            if (reg.DefaultValue != null)
            {
                bool defaultIsEnabled = IsValueInArray(reg.DefaultValue, reg.EnabledValue);
                bool defaultIsDisabled = IsValueInArray(reg.DefaultValue, reg.DisabledValue);

                if (defaultIsEnabled)
                    matchesDefault = IsSelected;
                else if (defaultIsDisabled)
                    matchesDefault = !IsSelected;
                else
                    matchesDefault = false;
            }
            else
            {
                matchesDefault = false;
            }
        }
        else if (InputType == InputType.Selection)
        {
            // Recommended/Default for Selection lives on ComboBoxMetadata.Options[i].
            // SelectedValue is the option index; compare indices directly.
            // If one option has both flags, matches for both are set — the caller's
            // if/else-if chain picks Recommended first (tiebreak: Recommended wins).
            var options = SettingDefinition.ComboBox?.Options;
            if (options != null && SelectedValue is int currentIndex)
            {
                int? recommendedIndex = null;
                int? defaultIndex = null;
                for (int i = 0; i < options.Count; i++)
                {
                    if (recommendedIndex == null && options[i].IsRecommended) recommendedIndex = i;
                    if (defaultIndex == null && options[i].IsDefault) defaultIndex = i;
                }
                matchesRecommended = recommendedIndex.HasValue && currentIndex == recommendedIndex.Value;
                matchesDefault = defaultIndex.HasValue && currentIndex == defaultIndex.Value;
            }
            else
            {
                matchesRecommended = false;
                matchesDefault = false;
            }
        }
        else if (InputType == InputType.NumericRange)
        {
            if (reg.RecommendedValue != null)
                matchesRecommended = ValuesEqual(NumericValue, reg.RecommendedValue);
            else
                matchesRecommended = false;

            if (reg.DefaultValue != null)
                matchesDefault = ValuesEqual(NumericValue, reg.DefaultValue);
            else
                matchesDefault = false;
        }

        return (matchesRecommended, matchesDefault);
    }

    private static bool IsValueInArray(object value, object?[]? array)
    {
        if (array == null) return false;
        return array.Any(v => ValuesEqual(value, v));
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (Equals(a, b)) return true;

        // Handle numeric type mismatches (int vs long, etc.)
        try
        {
            var aVal = Convert.ToInt64(a);
            var bVal = Convert.ToInt64(b);
            return aVal == bVal;
        }
        catch
        {
            return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool PowerCfgIndexMatchesValue(int index, int targetPowerCfgValue)
    {
        var mappings = SettingDefinition?.ComboBox?.ValueMappings;
        if (mappings == null) return false;

        if (mappings.TryGetValue(index, out var mapping) &&
            mapping.TryGetValue("PowerCfgValue", out var val) && val != null)
        {
            return Convert.ToInt32(val) == targetPowerCfgValue;
        }
        return false;
    }

    /// <summary>
    /// Initializes HasBadgeData based on whether the definition has comparable
    /// recommended/default data in RegistrySettings, ScheduledTaskSettings, or PowerCfgSettings.
    /// </summary>
    private void InitializeHasBadgeData()
    {
        if (SettingDefinition == null)
        {
            HasBadgeData = false;
            return;
        }

        // Check RegistrySettings for RecommendedValue or DefaultValue
        bool hasRegistryData = SettingDefinition.RegistrySettings.Any(r =>
            r.RecommendedValue != null || r.DefaultValue != null);

        // Selection settings carry Recommended/Default on ComboBoxMetadata.Options[i] rather than on
        // RegistrySetting, so consider ComboBox options as badge-worthy data too.
        bool hasSelectionOptionData = SettingDefinition.InputType == InputType.Selection
            && SettingDefinition.ComboBox?.Options?.Any(o => o.IsRecommended || o.IsDefault) == true;

        // Check ScheduledTaskSettings for RecommendedState or DefaultState
        bool hasTaskData = SettingDefinition.ScheduledTaskSettings.Any(t =>
            t.RecommendedState.HasValue || t.DefaultState.HasValue);

        // Check PowerCfgSettings for RecommendedValueAC or DefaultValueAC
        bool hasPowerCfgData = SettingDefinition.PowerCfgSettings?.Any(p =>
            p.RecommendedValueAC.HasValue || p.DefaultValueAC.HasValue) == true;

        HasBadgeData = hasRegistryData || hasSelectionOptionData || hasTaskData || hasPowerCfgData;
    }

    #endregion

    #region Technical Details

    public void ToggleTechnicalDetails() => IsTechnicalDetailsExpanded = !IsTechnicalDetailsExpanded;

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(BadgeLabel));
        OnPropertyChanged(nameof(BadgeTooltip));
        OnPropertyChanged(nameof(NewBadgeText));
        OnPropertyChanged(nameof(NewBadgeDismissTooltip));
        OnPropertyChanged(nameof(TechnicalDetailsLabel));
        OnPropertyChanged(nameof(OpenRegeditTooltip));
        OnPropertyChanged(nameof(ClickToUnlockText));
        OnPropertyChanged(nameof(PluggedInText));
        OnPropertyChanged(nameof(OnBatteryText));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _technicalDetailsManager.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}

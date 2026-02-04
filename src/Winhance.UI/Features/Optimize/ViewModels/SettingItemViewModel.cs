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

/// <summary>
/// ViewModel for individual settings displayed in optimization pages.
/// </summary>
public partial class SettingItemViewModel : BaseViewModel
{
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Reference to the parent feature ViewModel.
    /// </summary>
    public ISettingsFeatureViewModel? ParentFeatureViewModel { get; set; }

    /// <summary>
    /// The setting definition from Core.
    /// </summary>
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

    /// <summary>
    /// Localized "On" text for toggle switches.
    /// </summary>
    public string OnText { get; set; } = "On";

    /// <summary>
    /// Localized "Off" text for toggle switches.
    /// </summary>
    public string OffText { get; set; } = "Off";

    /// <summary>
    /// Localized "Apply" text for action buttons.
    /// </summary>
    public string ActionButtonText { get; set; } = "Apply";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// Collection of badges to display on this setting.
    /// </summary>
    public ObservableCollection<SettingBadgeInfo> Badges { get; } = new();

    /// <summary>
    /// Indicates whether this setting has any badges to display.
    /// </summary>
    [ObservableProperty]
    private bool _hasBadges;

    /// <summary>
    /// Width of the badge column. Set by parent ViewModel to ensure uniform layout.
    /// When any setting on the page has badges, all settings reserve space for the badge column.
    /// </summary>
    [ObservableProperty]
    private GridLength _badgeColumnWidth = new GridLength(0);

    /// <summary>
    /// The compatibility message for this setting (e.g., "Windows 11 Only").
    /// </summary>
    [ObservableProperty]
    private string? _compatibilityMessage;

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

    /// <summary>
    /// Indicates whether the setting is effectively enabled (both self and parent).
    /// </summary>
    public bool EffectiveIsEnabled => IsEnabled && ParentIsEnabled;

    /// <summary>
    /// Indicates whether this is a toggle-type setting.
    /// </summary>
    public bool IsToggleType => InputType == InputType.Toggle;

    /// <summary>
    /// Indicates whether this is a selection-type setting.
    /// </summary>
    public bool IsSelectionType => InputType == InputType.Selection;

    /// <summary>
    /// Indicates whether this is a numeric-type setting.
    /// </summary>
    public bool IsNumericType => InputType == InputType.NumericRange;

    /// <summary>
    /// Indicates whether this is an action-type setting (button).
    /// </summary>
    public bool IsActionType => InputType == InputType.Action;

    /// <summary>
    /// Indicates whether this is a checkbox-type setting.
    /// </summary>
    public bool IsCheckBoxType => InputType == InputType.CheckBox;

    /// <summary>
    /// Indicates whether this is a sub-setting (has a parent).
    /// </summary>
    public bool IsSubSetting => !string.IsNullOrEmpty(SettingDefinition?.ParentSettingId);

    public IAsyncRelayCommand ExecuteActionCommand { get; }

    public SettingItemViewModel(
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;

        ExecuteActionCommand = new AsyncRelayCommand(HandleActionAsync);
    }

    /// <summary>
    /// Updates visibility based on search text.
    /// Searches in Name, Description, and GroupName.
    /// </summary>
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

    #region UI Event Handlers (bound via x:Bind)

    /// <summary>
    /// Handles ToggleSwitch.Toggled event - bound via x:Bind in XAML.
    /// </summary>
    public void OnToggleSwitchToggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            _ = HandleToggleAsync(toggle.IsOn);
        }
    }

    /// <summary>
    /// Handles CheckBox.Click event - bound via x:Bind in XAML.
    /// </summary>
    public void OnCheckBoxClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            _ = HandleToggleAsync(checkBox.IsChecked == true);
        }
    }

    /// <summary>
    /// Handles ComboBox.DropDownClosed event - bound via x:Bind in XAML.
    /// Using DropDownClosed instead of SelectionChanged because SelectionChanged
    /// fires during control initialization, but DropDownClosed only fires on user interaction.
    /// </summary>
    public void OnComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedValue is { } value)
        {
            _ = HandleValueChangedAsync(value);
        }
    }

    /// <summary>
    /// Handles NumberBox.ValueChanged event - bound via x:Bind in XAML.
    /// </summary>
    public void OnNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
        {
            _ = HandleValueChangedAsync((int)e.NewValue);
        }
    }

    #endregion

    #region Apply Logic

    private async Task HandleToggleAsync(bool newValue)
    {
        if (IsApplying || SettingDefinition == null) return;

        // Skip if value hasn't actually changed
        if (newValue == IsSelected) return;

        try
        {
            // Check for confirmation if required
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(newValue);
            if (!confirmed)
            {
                // User cancelled - revert UI
                OnPropertyChanged(nameof(IsSelected));
                return;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Toggling setting: {SettingId} to {newValue}");

            await _settingApplicationService.ApplySettingAsync(
                SettingId,
                newValue,
                checkboxResult: checkboxChecked);

            // Update the ViewModel property after successful apply
            IsSelected = newValue;
            _logService.Log(LogLevel.Info, $"Successfully toggled setting {SettingId} to {newValue}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error toggling setting {SettingId}: {ex.Message}");
            // The UI control already shows the new value, but apply failed
            // We need to revert the UI - force a property change notification
            OnPropertyChanged(nameof(IsSelected));
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task HandleValueChangedAsync(object? value)
    {
        if (IsApplying || SettingDefinition == null || value == null) return;

        // Skip if value hasn't actually changed
        if (Equals(value, SelectedValue)) return;

        try
        {
            // Check for confirmation if required
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(value);
            if (!confirmed)
            {
                // User cancelled - revert UI
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(NumericValue));
                return;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Changing value for setting: {SettingId} to {value}");

            await _settingApplicationService.ApplySettingAsync(
                SettingId,
                true,
                value,
                checkboxResult: checkboxChecked);

            // Update the ViewModel property after successful apply
            SelectedValue = value;
            if (value is int intValue)
            {
                NumericValue = intValue;
            }
            _logService.Log(LogLevel.Info, $"Successfully changed value for setting {SettingId}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing value for setting {SettingId}: {ex.Message}");
            // Force UI to refresh to the old value
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
            // Check for confirmation if required
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(null);
            if (!confirmed)
            {
                return;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Executing action for setting: {SettingId}");

            // Pass the ActionCommand from the setting definition
            await _settingApplicationService.ApplySettingAsync(
                SettingId,
                true,
                value: null,
                checkboxResult: checkboxChecked,
                commandString: SettingDefinition.ActionCommand,
                applyRecommended: checkboxChecked);

            _logService.Log(LogLevel.Info, $"Successfully executed action for setting {SettingId}");

            // If recommended settings were applied, refresh the parent to update UI
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

    /// <summary>
    /// Handles confirmation dialog if the setting requires it.
    /// </summary>
    /// <param name="value">The value being applied (used for placeholder replacement)</param>
    /// <returns>Tuple of (confirmed, checkboxChecked)</returns>
    private async Task<(bool confirmed, bool checkboxChecked)> HandleConfirmationIfNeededAsync(object? value)
    {
        if (SettingDefinition == null || !SettingDefinition.RequiresConfirmation)
        {
            return (true, false);
        }

        // Get localized confirmation strings using convention-based keys
        var title = _localizationService.GetString($"Setting_{SettingId}_ConfirmTitle");
        var message = _localizationService.GetString($"Setting_{SettingId}_ConfirmMessage");
        var checkboxText = _localizationService.GetString($"Setting_{SettingId}_ConfirmCheckbox");

        // Replace {themeMode} placeholder for theme-mode-windows setting
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

    #region Badge Support

    /// <summary>
    /// Updates the badges collection based on the filter state and setting compatibility.
    /// Badges only appear when the filter is OFF and the setting has a compatibility message.
    /// Shows Windows icon with version number (10 or 11) and tooltip with full details.
    /// </summary>
    /// <param name="showCompatibilityBadges">True when filter is OFF and badges should be shown.</param>
    /// <param name="localizationService">Service for getting localized tooltip text.</param>
    public void UpdateBadges(bool showCompatibilityBadges, ILocalizationService? localizationService = null)
    {
        Badges.Clear();

        if (showCompatibilityBadges && SettingDefinition != null)
        {
            // Check for compatibility message from the registry (set by WindowsCompatibilityFilter)
            // This message is only present on settings that are incompatible with current OS
            if (SettingDefinition.CustomProperties.TryGetValue(
                Core.Features.Common.Constants.CustomPropertyKeys.VersionCompatibilityMessage,
                out var compatMessageObj) && compatMessageObj is string compatMessage)
            {
                // Determine badge type and text based on SettingDefinition properties
                // Build range issues get orange styling, version-only issues get version-specific styling
                var hasBuildRanges = SettingDefinition.SupportedBuildRanges?.Count > 0 ||
                                     SettingDefinition.MinimumBuildNumber.HasValue ||
                                     SettingDefinition.MaximumBuildNumber.HasValue;

                // Determine which Windows version this setting is for
                var versionText = SettingDefinition.IsWindows11Only ? "11" :
                                  SettingDefinition.IsWindows10Only ? "10" : "10";

                if (hasBuildRanges)
                {
                    // Build range issue - use orange/alert styling with the version number
                    Badges.Add(new SettingBadgeInfo(BadgeType.WinBuild, versionText, compatMessage));
                }
                else if (SettingDefinition.IsWindows10Only)
                {
                    // Windows 10 only - blue styling
                    Badges.Add(new SettingBadgeInfo(BadgeType.Win10, "10", compatMessage));
                }
                else if (SettingDefinition.IsWindows11Only)
                {
                    // Windows 11 only - cyan styling
                    Badges.Add(new SettingBadgeInfo(BadgeType.Win11, "11", compatMessage));
                }

                // Also set the warning text for display purposes
                WarningText = compatMessage;
            }
        }
        else
        {
            // Clear warning text when filter is ON (only compatible settings shown)
            WarningText = null;
        }

        HasBadges = Badges.Count > 0;
    }

    #endregion
}

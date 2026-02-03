using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
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

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isEnabled = true;

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
        IDispatcherService dispatcherService)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;

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
            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Toggling setting: {SettingId} to {newValue}");

            await _settingApplicationService.ApplySettingAsync(
                SettingId,
                newValue);

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
            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Changing value for setting: {SettingId} to {value}");

            await _settingApplicationService.ApplySettingAsync(
                SettingId,
                true,
                value);

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
            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Executing action for setting: {SettingId}");

            await _settingApplicationService.ApplySettingAsync(
                SettingId,
                true);

            _logService.Log(LogLevel.Info, $"Successfully executed action for setting {SettingId}");
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

    #endregion
}

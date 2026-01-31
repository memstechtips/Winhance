using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private bool _isInitializing = true;

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
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (_isInitializing) return;
        _ = HandleToggleAsync();
    }

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

    partial void OnSelectedValueChanged(object? value)
    {
        if (_isInitializing) return;
        _ = HandleValueChangedAsync(value);
    }

    [ObservableProperty]
    private ObservableCollection<ComboBoxOption> _comboBoxOptions = new();

    [ObservableProperty]
    private int _numericValue;

    partial void OnNumericValueChanged(int value)
    {
        if (_isInitializing) return;
        _ = HandleNumericValueChangedAsync(value);
    }

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
    /// Indicates whether this is a sub-setting (has a parent).
    /// </summary>
    public bool IsSubSetting => !string.IsNullOrEmpty(SettingDefinition?.ParentSettingId);

    public IAsyncRelayCommand ToggleCommand { get; }
    public IAsyncRelayCommand<object?> ValueChangedCommand { get; }

    public SettingItemViewModel(
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;

        ToggleCommand = new AsyncRelayCommand(HandleToggleAsync);
        ValueChangedCommand = new AsyncRelayCommand<object?>(HandleValueChangedAsync);
    }

    /// <summary>
    /// Marks initialization as complete, enabling change handlers.
    /// </summary>
    public void CompleteInitialization()
    {
        _isInitializing = false;
    }

    /// <summary>
    /// Updates visibility based on search text.
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
                   Description.ToLowerInvariant().Contains(lowerSearch);
    }

    private async Task HandleToggleAsync()
    {
        if (IsApplying || SettingDefinition == null) return;

        try
        {
            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Toggling setting: {SettingId} to {IsSelected}");

            await _settingApplicationService.ApplySettingAsync(
                SettingId,
                IsSelected);

            _logService.Log(LogLevel.Info, $"Successfully toggled setting {SettingId} to {IsSelected}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error toggling setting {SettingId}: {ex.Message}");
            // Revert the toggle on failure
            _isInitializing = true;
            IsSelected = !IsSelected;
            _isInitializing = false;
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task HandleValueChangedAsync(object? value)
    {
        if (IsApplying || SettingDefinition == null) return;

        try
        {
            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Changing value for setting: {SettingId} to {value}");

            await _settingApplicationService.ApplySettingAsync(
                SettingId,
                true,
                value);

            _logService.Log(LogLevel.Info, $"Successfully changed value for setting {SettingId}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing value for setting {SettingId}: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task HandleNumericValueChangedAsync(int value)
    {
        await HandleValueChangedAsync(value);
    }
}

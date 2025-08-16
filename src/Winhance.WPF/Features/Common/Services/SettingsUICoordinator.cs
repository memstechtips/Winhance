using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Registry;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.WPF.Features.Common.Events;
using Winhance.WPF.Features.Common.Helpers;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services;

/// <summary>
/// Pure UI coordinator for managing setting display state.
/// Focuses solely on UI concerns: loading, filtering, grouping, and state management.
/// Business logic has been moved to ISettingApplicationService.
/// </summary>
public class SettingsUICoordinator
    : EventHandlerBase,
        ISettingsUICoordinator,
        INotifyPropertyChanged,
        IDisposable
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    private readonly ITooltipDataService _tooltipDataService;
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ISearchService _searchService;
    private readonly ISettingsDelegateAssignmentService _delegateAssignmentService;
    private readonly ISettingsConfirmationService _confirmationService;
    private readonly ILogService _logService;
    private readonly Dictionary<string, ApplicationSetting> _settingModels = new();
    private bool _isLoading;
    private string _categoryName = string.Empty;
    private string _searchText = string.Empty;

    /// <summary>
    /// Event that fires when a property changes
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets a property value and raises the PropertyChanged event if the value has changed
    /// </summary>
    /// <typeparam name="T">The type of the property</typeparam>
    /// <param name="field">The backing field to update</param>
    /// <param name="value">The new value</param>
    /// <param name="propertyName">The name of the property (automatically determined by the compiler)</param>
    /// <returns>True if the value changed, false otherwise</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null
    )
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises the PropertyChanged event
    /// </summary>
    /// <param name="propertyName">The name of the property that changed</param>
    protected void OnPropertyChanged(
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null
    )
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string InstanceId => _instanceId;

    private readonly IEnumerable<IComboBoxValueResolver> _comboBoxResolvers;

    public SettingsUICoordinator(
        ITooltipDataService tooltipDataService,
        ISettingApplicationService settingApplicationService,
        ISearchService searchService,
        ISettingsDelegateAssignmentService delegateAssignmentService,
        ISettingsConfirmationService confirmationService,
        ILogService logService,
        IEventBus eventBus,
        IEnumerable<IComboBoxValueResolver> comboBoxResolvers
    )
        : base(eventBus, logService)
    {
        _tooltipDataService =
            tooltipDataService ?? throw new ArgumentNullException(nameof(tooltipDataService));
        _settingApplicationService =
            settingApplicationService
            ?? throw new ArgumentNullException(nameof(settingApplicationService));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _delegateAssignmentService = delegateAssignmentService ?? throw new ArgumentNullException(nameof(delegateAssignmentService));
        _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _comboBoxResolvers =
            comboBoxResolvers ?? throw new ArgumentNullException(nameof(comboBoxResolvers));
    }

    public ObservableCollection<SettingUIItem> Settings { get; } = new();
    public ObservableCollection<SettingGroup> SettingGroups { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterSettings(value);
            }
        }
    }

    public bool HasVisibleSettings => Settings.Any(s => s.IsVisible);

    private readonly HashSet<string> _initializedFeatures = new();

    /// <summary>
    /// Resolves the display value for ComboBox controls using domain resolvers.
    /// </summary>
    private async Task<string?> ResolveComboBoxDisplayValueAsync(
        SettingUIItem setting,
        object? currentValue
    )
    {
        if (setting.ControlType != ControlType.ComboBox || currentValue == null)
            return currentValue?.ToString();

        try
        {
            // Find the appropriate resolver for this setting
            var settingModel = _settingModels.GetValueOrDefault(setting.Id);
            if (settingModel == null)
                return currentValue.ToString();

            var resolver = _comboBoxResolvers.FirstOrDefault(r => r.CanResolve(settingModel));
            if (resolver == null)
                return currentValue.ToString();

            // Get the current index from the resolver
            var currentIndex = await resolver.ResolveCurrentIndexAsync(settingModel);
            if (
                currentIndex.HasValue
                && currentIndex.Value >= 0
                && currentIndex.Value < setting.ComboBoxOptions.Count
            )
            {
                return setting.ComboBoxOptions[currentIndex.Value];
            }

            return currentValue.ToString();
        }
        catch (Exception ex)
        {
            _logService.Log(
                LogLevel.Warning,
                $"Failed to resolve ComboBox display value for {setting.Id}: {ex.Message}"
            );
            return currentValue.ToString();
        }
    }

    /// <summary>
    /// Loads settings using existing SettingUIMapper for Core ↔ WPF mapping.
    /// Now uses ISettingApplicationService for all business logic.
    /// </summary>
    public async Task LoadSettingsAsync<T>(Func<Task<IEnumerable<T>>> settingsLoader)
        where T : ApplicationSetting
    {
        try
        {
            // Generate feature key to track initialization state using a more unique identifier
            var featureKey = $"{typeof(T).Name}_{CategoryName}_{_instanceId}";

            // CRITICAL FIX: Prevent multiple initialization cycles (SOLID compliance)
            if (_initializedFeatures.Contains(featureKey))
            {
                _logService.Log(
                    LogLevel.Debug,
                    $"Feature '{featureKey}' already initialized. Refreshing data only (preserving delegates)."
                );
                await RefreshFeatureDataAsync(settingsLoader);
                return;
            }

            _logService.Log(
                LogLevel.Info,
                $"Initializing feature '{featureKey}' for the first time"
            );

            IsLoading = true;
            var settings = await settingsLoader();
            var settingsList = settings.ToList();

            var uiItems = SettingUIMapper.ToUIItems(settings).ToList(); // CRITICAL: Materialize to prevent double execution
            var groups = SettingUIMapper.ToGroupedUIItems(uiItems);

            // Only clear during initial initialization, not refresh
            Settings.Clear();
            SettingGroups.Clear();
            _settingModels.Clear();

            // Store setting models for tooltip refresh
            foreach (var setting in settingsList)
            {
                _settingModels[setting.Id] = setting;
            }

            foreach (var item in uiItems)
            {
                // DELEGATE to specialized service for delegate assignment (SOLID compliance)
                _delegateAssignmentService.AssignDelegates(
                    item, 
                    HandleSettingChangeAsync, 
                    HandleSettingValueChangeAsync
                );

                // Subscribe to IsVisible property changes to update HasVisibleSettings
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(SettingUIItem.IsVisible))
                    {
                        OnPropertyChanged(nameof(HasVisibleSettings));
                    }
                };

                Settings.Add(item);
            }

            foreach (var group in groups)
            {
                SettingGroups.Add(group);
            }

            // Get tooltip data and update UI items with individual registry values
            try
            {
                var tooltipData = await _tooltipDataService.GetTooltipDataAsync(settingsList);

                foreach (var item in Settings)
                {
                    if (tooltipData.TryGetValue(item.Id, out var itemTooltipData))
                    {
                        SettingUIMapper.UpdateTooltipData(item, itemTooltipData);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log tooltip data error but don't fail the entire load operation
            }

            // Mark feature as initialized to prevent future destructive reloads
            _initializedFeatures.Add(featureKey);
            _logService.Log(
                LogLevel.Info,
                $"Feature '{featureKey}' initialization completed successfully"
            );

            OnPropertyChanged(nameof(HasVisibleSettings));
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refreshes feature data without destroying delegates or UI state.
    /// Follows SRP by handling only data refresh operations.
    /// </summary>
    private async Task RefreshFeatureDataAsync<T>(Func<Task<IEnumerable<T>>> settingsLoader)
        where T : ApplicationSetting
    {
        try
        {
            _logService.Log(LogLevel.Debug, "Refreshing feature data while preserving delegates");
            IsLoading = true;

            var settings = await settingsLoader();
            var settingsList = settings.ToList();

            // Update _settingModels for tooltip refresh without clearing delegates
            _settingModels.Clear();
            foreach (var setting in settingsList)
            {
                _settingModels[setting.Id] = setting;
            }

            // Update existing UI items with current system values (preserve delegates)
            foreach (var setting in settingsList)
            {
                var existingUIItem = Settings.FirstOrDefault(ui => ui.Id == setting.Id);
                if (existingUIItem != null)
                {
                    // CRITICAL FIX: Use UpdateFromRefreshedApplicationSetting for refresh operations
                    // This ensures NumericUpDown constraints (MaxValue/MinValue) are updated BEFORE setting values
                    // preventing the "Turn off hard disk" 100-minute capping bug during navigation refresh
                    SettingUIMapper.UpdateFromRefreshedApplicationSetting(existingUIItem, setting);
                }
            }

            // Refresh tooltips with current registry data
            try
            {
                var tooltipData = await _tooltipDataService.GetTooltipDataAsync(settingsList);
                foreach (var item in Settings)
                {
                    if (tooltipData.TryGetValue(item.Id, out var itemTooltipData))
                    {
                        SettingUIMapper.UpdateTooltipData(item, itemTooltipData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Error refreshing tooltip data: {ex.Message}");
            }

            _logService.Log(LogLevel.Debug, "Feature data refresh completed successfully");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Filters settings by delegating to ISearchService (SOLID compliance).
    /// </summary>
    public void FilterSettings(string searchText)
    {
        // DELEGATE to existing ISearchService instead of reimplementing search logic
        var filteredItems = _searchService.FilterItems(Settings, searchText);
        
        // Update visibility based on filtered results
        foreach (var setting in Settings)
        {
            setting.IsVisible = filteredItems.Contains(setting);
        }

        // Update group visibility using existing SettingGroup.UpdateVisibility() method
        foreach (var group in SettingGroups)
        {
            group.UpdateVisibility();
        }

        OnPropertyChanged(nameof(HasVisibleSettings));
    }

    /// <summary>
    /// Selects all settings using existing SettingGroup.SelectAll() method
    /// </summary>
    public void SelectAllSettings(bool selected)
    {
        // Use existing SettingGroup.SelectAll() method
        foreach (var group in SettingGroups)
        {
            group.SelectAll(selected);
        }
    }

    /// <summary>
    /// Refreshes individual setting status using existing SettingUIItem.UpdateUIStateFromSystem()
    /// </summary>
    public async Task RefreshSettingStatusAsync(
        string settingId,
        Func<
            string,
            Task<(bool isEnabled, object? value, RegistrySettingStatus status)>
        > statusChecker
    )
    {
        var setting = Settings.FirstOrDefault(s => s.Id == settingId);
        if (setting == null)
            return;

        try
        {
            setting.IsApplying = true;
            var (isEnabled, value, status) = await statusChecker(settingId);

            // Use existing SettingUIItem.UpdateUIStateFromSystem() method
            setting.UpdateUIStateFromSystem(isEnabled, value, status, value);
        }
        finally
        {
            setting.IsApplying = false;
        }
    }

    public void ClearSettings()
    {
        Settings.Clear();
        SettingGroups.Clear();
        OnPropertyChanged(nameof(HasVisibleSettings));
    }

    public int GetSelectedCount()
    {
        return Settings.Count(s => s.IsSelected);
    }

    public IEnumerable<SettingUIItem> GetSelectedSettings()
    {
        return Settings.Where(s => s.IsSelected);
    }

    /// <summary>
    /// Handles setting value changes by delegating to specialized services (SOLID compliance).
    /// </summary>
    /// <param name="settingId">The ID of the setting being changed.</param>
    /// <param name="value">The new value for the setting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleSettingValueChangeAsync(string settingId, object? value)
    {
        _logService.Log(
            LogLevel.Info,
            $"HandleSettingValueChangeAsync called for setting: {settingId}, value: {value}"
        );

        var setting = Settings.FirstOrDefault(s => s.Id == settingId);
        if (setting == null)
        {
            _logService.Log(LogLevel.Warning, $"Setting not found: {settingId}");
            return;
        }

        try
        {
            setting.IsApplying = true;
            setting.Status = RegistrySettingStatus.Unknown;
            setting.StatusMessage = "Applying...";

            // DELEGATE confirmation handling to specialized service (SOLID compliance)
            if (_settingModels.TryGetValue(settingId, out var applicationSetting))
            {
                var (confirmed, checkboxChecked) = await _confirmationService.HandleConfirmationAsync(
                    settingId, 
                    value, 
                    applicationSetting
                );

                if (!confirmed)
                {
                    throw new OperationCanceledException("User cancelled the operation");
                }

                // DELEGATE to existing ISettingApplicationService (SOLID compliance)
                await _settingApplicationService.ApplySettingAsync(settingId, checkboxChecked, value);
            }
            else
            {
                // DELEGATE to existing ISettingApplicationService (SOLID compliance)
                await _settingApplicationService.ApplySettingAsync(settingId, true, value);
            }

            setting.Status = RegistrySettingStatus.Applied;
            setting.StatusMessage = "Applied";

            // Update the UI state to reflect the new value only after successful application
            setting.UpdateUIStateFromSystem(true, value, RegistrySettingStatus.Applied, value);

            // CRITICAL: Refresh tooltip data immediately after successful application
            await RefreshTooltipForSettingAsync(settingId);
        }
        catch (OperationCanceledException)
        {
            _logService.Log(
                LogLevel.Info,
                $"Setting change cancelled by user for setting: {settingId}"
            );

            await HandleSettingApplicationError(setting, settingId, "Cancelled", RegistrySettingStatus.NotApplied);
        }
        catch (Exception ex)
        {
            _logService.Log(
                LogLevel.Error,
                $"Error applying value change for setting: {settingId}: {ex.Message}"
            );

            await HandleSettingApplicationError(setting, settingId, $"Error: {ex.Message}", RegistrySettingStatus.Error);
        }
        finally
        {
            setting.IsApplying = false;
        }
    }


    /// <summary>
    /// Helper method to handle setting application errors consistently.
    /// </summary>
    private async Task HandleSettingApplicationError(SettingUIItem setting, string settingId, string errorMessage, RegistrySettingStatus status)
    {
        setting.Status = status;
        setting.StatusMessage = errorMessage;

        // Refresh the UI state from the actual system state
        var currentState = await _settingApplicationService.GetSettingStateAsync(settingId);
        if (currentState.Success)
        {
            // Resolve ComboBox display value for proper UI rollback
            object? displayValue = await ResolveComboBoxDisplayValueAsync(
                setting,
                currentState.CurrentValue
            );

            _logService.Log(
                LogLevel.Debug,
                $"Rollback: Setting {settingId} to display value '{displayValue}' (system value: {currentState.CurrentValue})"
            );
            setting.UpdateUIStateFromSystem(
                currentState.IsEnabled,
                displayValue,
                currentState.Status,
                currentState.CurrentValue
            );
        }
    }


    /// <summary>
    /// Handles setting changes by delegating to ISettingApplicationService (SOLID compliance).
    /// </summary>
    /// <param name="settingId">The ID of the setting being changed.</param>
    /// <param name="enable">Whether to enable or disable the setting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleSettingChangeAsync(string settingId, bool enable)
    {
        _logService.Log(
            LogLevel.Info,
            $"HandleSettingChangeAsync called for setting: {settingId}, enable: {enable}"
        );

        var setting = Settings.FirstOrDefault(s => s.Id == settingId);
        if (setting == null)
        {
            _logService.Log(LogLevel.Warning, $"Setting not found: {settingId}");
            return;
        }

        try
        {
            setting.IsApplying = true;
            setting.Status = RegistrySettingStatus.Unknown;
            setting.StatusMessage = "Applying...";

            // DELEGATE to existing ISettingApplicationService (SOLID compliance)
            await _settingApplicationService.ApplySettingAsync(settingId, enable);

            setting.Status = RegistrySettingStatus.Applied;
            setting.StatusMessage = "Applied";

            // Update the UI state to reflect the new value only after successful application
            setting.UpdateUIStateFromSystem(enable, null, RegistrySettingStatus.Applied, null);

            // CRITICAL: Refresh tooltip data immediately after successful application
            await RefreshTooltipForSettingAsync(settingId);
        }
        catch (Exception ex)
        {
            _logService.Log(
                LogLevel.Error,
                $"Error applying setting change: {settingId}: {ex.Message}"
            );

            await HandleSettingApplicationError(setting, settingId, $"Error: {ex.Message}", RegistrySettingStatus.Error);
        }
        finally
        {
            setting.IsApplying = false;
        }
    }

    /// <summary>
    /// Refreshes tooltip data for a specific setting after successful application.
    /// Follows SRP by handling only UI tooltip refresh operations.
    /// </summary>
    /// <param name="settingId">The ID of the setting to refresh tooltip data for</param>
    private async Task RefreshTooltipForSettingAsync(string settingId)
    {
        try
        {
            if (!_settingModels.TryGetValue(settingId, out var applicationSetting))
            {
                _logService.Log(LogLevel.Warning, $"Cannot refresh tooltip for unknown setting: {settingId}");
                return;
            }

            _logService.Log(LogLevel.Debug, $"Refreshing tooltip data for setting: {settingId}");

            // DIRECT call to ITooltipDataService for immediate, reliable refresh
            var refreshedTooltipData = await _tooltipDataService.RefreshTooltipDataAsync(settingId, applicationSetting);
            
            if (refreshedTooltipData != null)
            {
                // Find the UI item and update its tooltip data
                var uiItem = Settings.FirstOrDefault(s => s.Id == settingId);
                if (uiItem != null)
                {
                    SettingUIMapper.UpdateTooltipData(uiItem, refreshedTooltipData);
                    _logService.Log(LogLevel.Debug, $"Successfully refreshed tooltip for setting: {settingId}");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error refreshing tooltip for setting {settingId}: {ex.Message}");
        }
    }

    public override void Dispose()
    {
        base.Dispose(); // This will unsubscribe all event handlers
    }
}

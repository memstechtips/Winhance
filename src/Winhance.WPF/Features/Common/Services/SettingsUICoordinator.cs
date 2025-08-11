using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Registry;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Events;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Helpers;

namespace Winhance.WPF.Features.Common.Services;

/// <summary>
/// Pure UI coordinator for managing setting display state.
/// Focuses solely on UI concerns: loading, filtering, grouping, and state management.
/// Business logic has been moved to ISettingApplicationService.
/// </summary>
public class SettingsUICoordinator : EventHandlerBase, ISettingsUICoordinator, INotifyPropertyChanged, IDisposable
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    private readonly SettingTooltipDataService _tooltipDataService;
    private readonly ISettingApplicationService _settingApplicationService;
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
    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
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
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string InstanceId => _instanceId;

    public SettingsUICoordinator(
        SettingTooltipDataService tooltipDataService, 
        ISettingApplicationService settingApplicationService,
        ILogService logService, 
        IEventBus eventBus)
        : base(eventBus, logService)
    {
        _tooltipDataService = tooltipDataService ?? throw new ArgumentNullException(nameof(tooltipDataService));
        _settingApplicationService = settingApplicationService ?? throw new ArgumentNullException(nameof(settingApplicationService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        SubscribeToRegistryEvents();
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
    /// Loads settings using existing SettingUIMapper for Core ↔ WPF mapping.
    /// Now uses ISettingApplicationService for all business logic.
    /// </summary>
    public async Task LoadSettingsAsync<T>(Func<Task<IEnumerable<T>>> settingsLoader) where T : ApplicationSetting
    {
        try
        {
            // Generate feature key to track initialization state using a more unique identifier
            var featureKey = $"{typeof(T).Name}_{CategoryName}_{_instanceId}";
            
            // CRITICAL FIX: Prevent multiple initialization cycles (SOLID compliance)
            if (_initializedFeatures.Contains(featureKey))
            {
                _logService.Log(LogLevel.Debug, $"Feature '{featureKey}' already initialized. Refreshing data only (preserving delegates).");
                await RefreshFeatureDataAsync(settingsLoader);
                return;
            }
            
            _logService.Log(LogLevel.Info, $"Initializing feature '{featureKey}' for the first time");
            
            // Debug cross-feature loading
            try
            {
                /* Debug logging removed */
            }
            catch { /* Ignore debug file errors */ }
            
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
                // CRITICAL FIX: Assign delegates BEFORE adding to collection to prevent race conditions
                switch (item.ControlType)
                {
                    case ControlType.BinaryToggle:
                        item.OnSettingChanged = async (isEnabled) => 
                        {
                            await HandleSettingChangeAsync(item.Id, isEnabled);
                        };
                        _logService.Log(LogLevel.Debug, $"Binary toggle delegate assigned for setting '{item.Id}'");
                        break;
                        
                    case ControlType.ComboBox:
                    case ControlType.NumericUpDown:
                    case ControlType.Slider:
                        item.OnSettingValueChanged = async (value) => 
                        {
                            _logService.Log(LogLevel.Debug, $"OnSettingValueChanged delegate invoked for '{item.Id}', value: {value}");
                            
                            // Debug to desktop file
                            if (item.ControlType == ControlType.ComboBox)
                            {
                                /* Debug logging removed */
                            }
                            
                            await HandleSettingValueChangeAsync(item.Id, value);
                        };
                        _logService.Log(LogLevel.Debug, $"{item.ControlType} delegate assigned for setting '{item.Id}'");
                        
                        /* Debug logging removed */
                        break;
                        
                    default:
                        _logService.Log(LogLevel.Warning, $"Unknown control type '{item.ControlType}' for setting '{item.Id}'. No delegates assigned.");
                        break;
                }
                
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
            _logService.Log(LogLevel.Info, $"Feature '{featureKey}' initialization completed successfully");
            
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
    private async Task RefreshFeatureDataAsync<T>(Func<Task<IEnumerable<T>>> settingsLoader) where T : ApplicationSetting
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
                    // Use existing SettingUIMapper to properly update UI state from system
                    // This preserves delegates while updating the UI with current system state
                    SettingUIMapper.UpdateFromSystemState(existingUIItem, setting.IsEnabled, setting.CurrentValue, RegistrySettingStatus.Applied);
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
    /// Filters settings using existing ISearchable implementation in SettingUIItem
    /// </summary>
    public void FilterSettings(string searchText)
    {
        
        int visibleCount = 0;
        int totalCount = 0;
        
        // Use existing ISearchable.MatchesSearch() method in SettingUIItem
        foreach (var setting in Settings)
        {
            totalCount++;
            var wasVisible = setting.IsVisible;
            var matches = setting.MatchesSearch(searchText);
            setting.IsVisible = matches;
            
            if (matches)
            {
                visibleCount++;
            }
            
            if (wasVisible != matches)
            {
            }
        }
        
        
        // Use existing SettingGroup.UpdateVisibility() method
        foreach (var group in SettingGroups)
        {
            group.UpdateVisibility();
        }
        
        var hasVisibleSettings = Settings.Any(s => s.IsVisible);
        
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
    public async Task RefreshSettingStatusAsync(string settingId, Func<string, Task<(bool isEnabled, object? value, RegistrySettingStatus status)>> statusChecker)
    {
        var setting = Settings.FirstOrDefault(s => s.Id == settingId);
        if (setting == null) return;

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
    /// Handles setting changes by delegating to the Application Service
    /// </summary>
    /// <param name="settingId">The ID of the setting that changed</param>
    /// <param name="isEnabled">Whether the setting should be enabled</param>
    public async Task HandleSettingChangeAsync(string settingId, bool isEnabled)
    {
        _logService.Log(LogLevel.Info, $"HandleSettingChangeAsync called for setting: {settingId}, isEnabled: {isEnabled}");
        
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

            // Delegate to Application Service
            await _settingApplicationService.ApplySettingAsync(settingId, isEnabled);

            setting.Status = RegistrySettingStatus.Applied;
            setting.StatusMessage = "Applied";
            
            // Update the UI state to reflect the new state
            setting.UpdateUIStateFromSystem(isEnabled, setting.SelectedValue, RegistrySettingStatus.Applied, setting.CurrentValue);
            
            // Refresh tooltip data to show updated registry values
            if (_settingModels.TryGetValue(settingId, out var applicationSetting))
            {
                await RefreshTooltipDataAsync(applicationSetting);
            }
        }
        catch (Exception ex)
        {
            setting.Status = RegistrySettingStatus.Error;
            setting.StatusMessage = $"Error: {ex.Message}";
            
            // Revert UI state on error
            setting.IsSelected = !isEnabled;
        }
        finally
        {
            setting.IsApplying = false;
        }
    }

    /// <summary>
    /// Handles setting value changes by applying the setting and updating UI state.
    /// </summary>
    /// <param name="settingId">The ID of the setting being changed.</param>
    /// <param name="value">The new value for the setting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleSettingValueChangeAsync(string settingId, object? value)
    {
        _logService.Log(LogLevel.Info, $"HandleSettingValueChangeAsync called for setting: {settingId}, value: {value}");

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

            _logService.Log(LogLevel.Debug, $"About to call Application Service for setting: {settingId}, value: {value}");
            
            // Delegate to Application Service
            await _settingApplicationService.ApplySettingAsync(settingId, true, value);

            _logService.Log(LogLevel.Debug, $"Application Service completed successfully for {settingId}");

            setting.Status = RegistrySettingStatus.Applied;
            setting.StatusMessage = "Applied";
            
            // Update the UI state to reflect the new value
            setting.UpdateUIStateFromSystem(true, value, RegistrySettingStatus.Applied, value);
            
            // Refresh tooltip data to show updated registry values
            if (_settingModels.TryGetValue(settingId, out var applicationSetting))
            {
                await RefreshTooltipDataAsync(applicationSetting);
            }
            
        }
        catch (Exception ex)
        {
            setting.Status = RegistrySettingStatus.Error;
            setting.StatusMessage = $"Error: {ex.Message}";
            
            _logService.Log(LogLevel.Error, $"Error applying value change for setting: {settingId}: {ex.Message}");
            
            // Revert UI state on error - reload original value
            var originalValue = setting.CurrentValue;
            setting.SelectedValue = originalValue;
        }
        finally
        {
            setting.IsApplying = false;
        }
    }

    private string GetStatusMessage(RegistrySettingStatus status)
    {
        return status switch
        {
            RegistrySettingStatus.Applied => "Applied",
            RegistrySettingStatus.NotApplied => "Not Applied",
            RegistrySettingStatus.Modified => "Modified",
            RegistrySettingStatus.Error => "Error",
            RegistrySettingStatus.Unknown => "Unknown",
            _ => "Unknown"
        };
    }
    
    /// <summary>
    /// Subscribes to registry change events for reactive updates
    /// </summary>
    private void SubscribeToRegistryEvents()
    {
        Subscribe<RegistryValueChangedEvent>(OnRegistryValueChanged);
        Subscribe<TooltipDataRefreshedEvent>(OnTooltipDataRefreshed);
    }

    /// <summary>
    /// Handles registry value change events
    /// </summary>
    /// <param name="event">The registry change event</param>
    private async void OnRegistryValueChanged(RegistryValueChangedEvent @event)
    {
        try
        {
            // Removed excessive logging for registry change events

            // Find the affected setting and refresh its tooltip data
            var affectedSetting = _settingModels.Values
                .FirstOrDefault(s => 
                    s.RegistrySettings != null && s.RegistrySettings.Any(rs =>
                        rs.Hive == @event.RegistrySetting.Hive &&
                        rs.SubKey == @event.RegistrySetting.SubKey &&
                        rs.Name == @event.RegistrySetting.Name));

            if (affectedSetting != null)
            {
                await RefreshTooltipDataAsync(affectedSetting);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error handling registry change: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles tooltip data refreshed events
    /// </summary>
    /// <param name="event">The tooltip refresh event</param>
    private async void OnTooltipDataRefreshed(TooltipDataRefreshedEvent @event)
    {
        try
        {
            

            // Find the affected settings and refresh their tooltip data
            var affectedSettings = _settingModels.Values
                .Where(s => 
                    s.RegistrySettings != null && s.RegistrySettings.Any(rs =>
                        rs.Hive == @event.Hive &&
                        rs.SubKey == @event.SubKey &&
                        rs.Name == @event.Name));

            foreach (var setting in affectedSettings)
            {
                // Removed excessive logging for tooltip refresh
                await RefreshTooltipDataAsync(setting);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error handling tooltip refresh event: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes tooltip data reactively when registry changes occur
    /// </summary>
    /// <param name="setting">The setting to refresh tooltip data for</param>
    private async Task RefreshTooltipDataAsync(ApplicationSetting setting)
    {
        try
        {
            _logService.Log(LogLevel.Info, $"Reactively refreshing tooltip data for {setting.Id}");

            // Get fresh tooltip data (caches are automatically invalidated by registry events)
            var tooltipData = await _tooltipDataService.GetTooltipDataAsync(new[] { setting });
            
            if (tooltipData.TryGetValue(setting.Id, out var itemTooltipData))
            {
                // Update UI with fresh data
                var uiSetting = Settings.FirstOrDefault(s => s.Id == setting.Id);
                if (uiSetting != null)
                {
                    SettingUIMapper.UpdateTooltipData(uiSetting, itemTooltipData);
                    _logService.Log(LogLevel.Info, $"Successfully updated tooltip data for {setting.Id}");
                }
            }
            else
            {
                
            }
    }
    catch (Exception ex)
    {
        _logService.Log(LogLevel.Error, $"Error handling registry change: {ex.Message}");
    }
}

public override void Dispose()
{
    base.Dispose(); // This will unsubscribe all event handlers
}
}

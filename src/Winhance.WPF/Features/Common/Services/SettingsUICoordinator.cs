using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Registry;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.Events;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.Services.Configuration;
using Winhance.WPF.Features.Common.Helpers;

namespace Winhance.WPF.Features.Common.Services;

/// <summary>
/// Coordinates UI state management using existing components:
/// - SettingUIItem for individual setting state
/// - SettingGroup for grouping and bulk operations  
/// - SettingUIMapper for Core ↔ WPF mapping
/// - ISearchable (in SettingUIItem) for filtering
/// </summary>
public class SettingsUICoordinator : EventHandlerBase, ISettingsUICoordinator, INotifyPropertyChanged, IDisposable
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    private readonly SettingTooltipDataService _tooltipDataService;
    private readonly ILogService _logService;
    private readonly IRegistryService _registryService;
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

    public SettingsUICoordinator(SettingTooltipDataService tooltipDataService, ILogService logService, IRegistryService registryService, IEventBus eventBus)
        : base(eventBus, logService)
    {
        _tooltipDataService = tooltipDataService ?? throw new ArgumentNullException(nameof(tooltipDataService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));

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

    /// <summary>
    /// Loads settings using existing SettingUIMapper for Core ↔ WPF mapping
    /// </summary>
    public async Task LoadSettingsAsync<T>(Func<Task<IEnumerable<T>>> settingsLoader, Func<string, bool, Task>? settingChangeHandler = null, Func<string, object?, Task>? settingValueChangeHandler = null) where T : ApplicationSetting
    {
        IsLoading = true;
        try
        {
            var settings = await settingsLoader();
            var settingsList = settings.ToList();

            var uiItems = SettingUIMapper.ToUIItems(settings);
            var groups = SettingUIMapper.ToGroupedUIItems(settings);

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
                _logService.Log(LogLevel.Info, $"Setting up UI item: {item.Id}, settingChangeHandler: {settingChangeHandler != null}");
                
                // Wire up the setting change handler if provided
                if (settingChangeHandler != null)
                {
                    _logService.Log(LogLevel.Info, $"Wiring up OnSettingChanged delegate for {item.Id}");
                    item.OnSettingChanged = async (isEnabled) => 
                    {
                        _logService.Log(LogLevel.Info, $"OnSettingChanged delegate called for {item.Id}, isEnabled: {isEnabled}");
                        await HandleSettingChangeAsync(item.Id, isEnabled, settingChangeHandler);
                    };
                }
                else
                {
                    _logService.Log(LogLevel.Warning, $"No settingChangeHandler provided for {item.Id}");
                }

                // Wire up the setting value change handler if provided
                if (settingValueChangeHandler != null)
                {
                    item.OnSettingValueChanged = async (value) => 
                    {
                        await HandleSettingValueChangeAsync(item.Id, value, settingValueChangeHandler);
                    };
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
                System.Diagnostics.Debug.WriteLine($"[SettingsUICoordinator] Error loading tooltip data: {ex.Message}");
            }
            
            OnPropertyChanged(nameof(HasVisibleSettings));
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
        _logService?.Log(LogLevel.Debug, $"[SettingsUICoordinator-{_instanceId}] FilterSettings called with: '{searchText}', Total settings: {Settings.Count}");
        
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
                _logService?.Log(LogLevel.Debug, $"[SettingsUICoordinator-{_instanceId}] Setting '{setting.Name}' matches search '{searchText}'");
            }
            
            if (wasVisible != matches)
            {
                _logService?.Log(LogLevel.Debug, $"[SettingsUICoordinator-{_instanceId}] Setting '{setting.Name}' visibility changed from {wasVisible} to {matches}");
            }
        }
        
        _logService?.Log(LogLevel.Debug, $"[SettingsUICoordinator-{_instanceId}] After filtering: {visibleCount}/{totalCount} settings visible");
        
        // Use existing SettingGroup.UpdateVisibility() method
        foreach (var group in SettingGroups)
        {
            group.UpdateVisibility();
        }
        
        var hasVisibleSettings = Settings.Any(s => s.IsVisible);
        _logService?.Log(LogLevel.Debug, $"[SettingsUICoordinator-{_instanceId}] HasVisibleSettings: {hasVisibleSettings}");
        
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
    /// Handles setting changes by delegating to the appropriate service
    /// </summary>
    /// <param name="settingId">The ID of the setting that changed</param>
    /// <param name="isEnabled">Whether the setting should be enabled</param>
    /// <param name="applySettingAsync">The async function to apply the setting</param>
    public async Task HandleSettingChangeAsync(string settingId, bool isEnabled, Func<string, bool, Task> applySettingAsync)
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

            await applySettingAsync(settingId, isEnabled);

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
    /// <param name="settingValueChangeHandler">The handler function to apply the setting value change.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleSettingValueChangeAsync(string settingId, object? value, Func<string, object?, Task> settingValueChangeHandler)
    {
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

            _logService.Log(LogLevel.Debug, $"Handling value change for setting: {settingId}, new value: {value ?? "null"}");
            
            await settingValueChangeHandler(settingId, value);

            setting.Status = RegistrySettingStatus.Applied;
            setting.StatusMessage = "Applied";
            
            // Update the UI state to reflect the new value
            setting.UpdateUIStateFromSystem(true, value, RegistrySettingStatus.Applied, value);
            
            // Refresh tooltip data to show updated registry values
            if (_settingModels.TryGetValue(settingId, out var applicationSetting))
            {
                await RefreshTooltipDataAsync(applicationSetting);
            }
            
            _logService.Log(LogLevel.Debug, $"Successfully applied value change for setting: {settingId}");
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
            _logService.Log(
                LogLevel.Info, 
                $"SettingsUICoordinator received registry change for {@event.ValuePath}"
            );

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
            _logService.Log(
                LogLevel.Debug, 
                $"Tooltip data refreshed for registry path: {@event.RegistryPath}"
            );

            // Find the affected settings and refresh their tooltip data
            var affectedSettings = _settingModels.Values
                .Where(s => 
                    s.RegistrySettings != null && s.RegistrySettings.Any(rs =>
                        rs.Hive == @event.Hive &&
                        rs.SubKey == @event.SubKey &&
                        rs.Name == @event.Name));

            foreach (var setting in affectedSettings)
            {
                _logService.Log(LogLevel.Info, $"Refreshing tooltip for setting {setting.Id} due to registry change");
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
                _logService.Log(LogLevel.Debug, "No tooltip data found for setting during refresh");
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

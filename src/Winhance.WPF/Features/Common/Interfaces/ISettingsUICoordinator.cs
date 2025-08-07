using System.Collections.ObjectModel;
using System.ComponentModel;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Interfaces;

/// <summary>
/// Coordinates UI state management for settings using existing components.
/// Uses composition pattern with SettingUIItem, SettingGroup, and SettingUIMapper.
/// </summary>
public interface ISettingsUICoordinator : INotifyPropertyChanged
{
    // Collections (using existing SettingUIItem and SettingGroup)
    ObservableCollection<SettingUIItem> Settings { get; }
    ObservableCollection<SettingGroup> SettingGroups { get; }
    
    // UI State Properties
    bool IsLoading { get; set; }
    string CategoryName { get; set; }
    string SearchText { get; set; }
    bool HasVisibleSettings { get; }
    
    /// <summary>
    /// Unique identifier for this coordinator instance (for debugging)
    /// </summary>
    string InstanceId { get; }
    
    // Core Operations (delegates to existing components)
    Task LoadSettingsAsync<T>(Func<Task<IEnumerable<T>>> settingsLoader, Func<string, bool, Task>? settingChangeHandler = null, Func<string, object?, Task>? settingValueChangeHandler = null) where T : ApplicationSetting;
    void FilterSettings(string searchText);
    void SelectAllSettings(bool selected);
    Task RefreshSettingStatusAsync(string settingId, Func<string, Task<(bool isEnabled, object? value, RegistrySettingStatus status)>> statusChecker);
    
    /// <summary>
    /// Handles a setting change by applying the setting and updating UI state.
    /// </summary>
    /// <param name="settingId">The ID of the setting being changed.</param>
    /// <param name="enable">Whether the setting should be enabled.</param>
    /// <param name="settingChangeHandler">The handler function to apply the setting change.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleSettingChangeAsync(string settingId, bool enable, Func<string, bool, Task> settingChangeHandler);

    /// <summary>
    /// Handles a setting value change by applying the setting and updating UI state.
    /// </summary>
    /// <param name="settingId">The ID of the setting being changed.</param>
    /// <param name="value">The new value for the setting.</param>
    /// <param name="settingValueChangeHandler">The handler function to apply the setting value change.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleSettingValueChangeAsync(string settingId, object? value, Func<string, object?, Task> settingValueChangeHandler);
    
    // Utility Methods
    void ClearSettings();
    int GetSelectedCount();
    IEnumerable<SettingUIItem> GetSelectedSettings();
}

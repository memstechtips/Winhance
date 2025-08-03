using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Generic service interface for handling application settings business logic.
    /// Works with any type of ApplicationSetting (CustomizationSetting, OptimizationSetting, etc.).
    /// Separates business logic from UI concerns following clean architecture principles.
    /// </summary>
    public interface IApplicationSettingsService
    {
        /// <summary>
        /// Registers a setting in the service cache for efficient lookups.
        /// This should be called by ViewModels during initialization.
        /// </summary>
        /// <param name="setting">The setting to register.</param>
        void RegisterSetting(ApplicationSetting setting);

        /// <summary>
        /// Registers multiple settings in the service cache.
        /// </summary>
        /// <param name="settings">The settings to register.</param>
        void RegisterSettings(IEnumerable<ApplicationSetting> settings);

        /// <summary>
        /// Checks if a specific setting is currently enabled in the system.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <returns>True if the setting is enabled, false otherwise.</returns>
        Task<bool> IsSettingEnabledAsync(string settingId);

        /// <summary>
        /// Applies a setting with the specified enable state and optional value.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <param name="enable">Whether to enable or disable the setting.</param>
        /// <param name="value">Optional value for settings that require specific values (e.g., ComboBox selections).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplySettingAsync(string settingId, bool enable, object? value = null);

        /// <summary>
        /// Gets the current value of a specific setting from the system.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <returns>The current value of the setting, or null if not set.</returns>
        Task<object?> GetSettingValueAsync(string settingId);

        /// <summary>
        /// Applies multiple settings in a batch operation for better performance.
        /// </summary>
        /// <param name="settings">Dictionary of setting IDs mapped to their enable state and values.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplyMultipleSettingsAsync(Dictionary<string, (bool enable, object? value)> settings);

        /// <summary>
        /// Gets the enabled state of multiple settings in a batch operation.
        /// </summary>
        /// <param name="settingIds">Collection of setting IDs to check.</param>
        /// <returns>Dictionary mapping setting IDs to their enabled state.</returns>
        Task<Dictionary<string, bool>> GetMultipleSettingsStateAsync(IEnumerable<string> settingIds);

        /// <summary>
        /// Gets the current values of multiple settings in a batch operation.
        /// </summary>
        /// <param name="settingIds">Collection of setting IDs to get values for.</param>
        /// <returns>Dictionary mapping setting IDs to their current values.</returns>
        Task<Dictionary<string, object?>> GetMultipleSettingsValuesAsync(IEnumerable<string> settingIds);

        /// <summary>
        /// Restores a setting to its default value.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RestoreSettingToDefaultAsync(string settingId);

        /// <summary>
        /// Restores multiple settings to their default values.
        /// </summary>
        /// <param name="settingIds">Collection of setting IDs to restore.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RestoreMultipleSettingsToDefaultAsync(IEnumerable<string> settingIds);

        /// <summary>
        /// Refreshes the status of a setting by re-reading from the system.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RefreshSettingStatusAsync(string settingId);

        /// <summary>
        /// Event raised when a setting's status changes.
        /// </summary>
        event EventHandler<SettingStatusChangedEventArgs>? SettingStatusChanged;

        #region Category-Specific Methods

        /// <summary>
        /// Gets all gaming and performance optimization settings.
        /// </summary>
        /// <returns>Collection of gaming and performance settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetGamingAndPerformanceSettingsAsync();

        /// <summary>
        /// Gets all start menu customization settings.
        /// </summary>
        /// <returns>Collection of start menu settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetStartMenuSettingsAsync();

        /// <summary>
        /// Gets all taskbar customization settings.
        /// </summary>
        /// <returns>Collection of taskbar settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetTaskbarSettingsAsync();

        /// <summary>
        /// Gets all explorer customization settings.
        /// </summary>
        /// <returns>Collection of explorer settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetExplorerSettingsAsync();

        /// <summary>
        /// Gets all theme customization settings.
        /// </summary>
        /// <returns>Collection of theme settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetThemeSettingsAsync();

        /// <summary>
        /// Gets all explorer optimization settings.
        /// </summary>
        /// <returns>Collection of explorer optimization settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetExplorerOptimizationSettingsAsync();

        /// <summary>
        /// Gets all privacy optimization settings.
        /// </summary>
        /// <returns>Collection of privacy optimization settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetPrivacyOptimizationSettingsAsync();

        /// <summary>
        /// Gets all update optimization settings.
        /// </summary>
        /// <returns>Collection of update optimization settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetUpdateOptimizationSettingsAsync();

        /// <summary>
        /// Gets all power optimization settings.
        /// </summary>
        /// <returns>Collection of power optimization settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetPowerOptimizationSettingsAsync();

        /// <summary>
        /// Gets all advanced power setting groups with their settings.
        /// </summary>
        /// <returns>Collection of advanced power setting groups.</returns>
        Task<IEnumerable<object>> GetAdvancedPowerSettingGroupsAsync();

        /// <summary>
        /// Gets all available power plans.
        /// </summary>
        /// <returns>Collection of available power plans.</returns>
        Task<IEnumerable<object>> GetAvailablePowerPlansAsync();

        /// <summary>
        /// Gets the currently active power plan.
        /// </summary>
        /// <returns>The active power plan, or null if none found.</returns>
        Task<object?> GetActivePowerPlanAsync();

        /// <summary>
        /// Applies an advanced power setting value.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <param name="acValue">The AC power value.</param>
        /// <param name="dcValue">The DC power value.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplyAdvancedPowerSettingAsync(string powerPlanGuid, string subgroupGuid, string settingGuid, int acValue, int dcValue);

        /// <summary>
        /// Checks system capabilities for power management (battery, lid detection).
        /// </summary>
        /// <returns>Dictionary with capability information.</returns>
        Task<Dictionary<string, bool>> CheckPowerSystemCapabilitiesAsync();

        /// <summary>
        /// Gets all Windows security optimization settings.
        /// </summary>
        /// <returns>Collection of Windows security optimization settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetWindowsSecurityOptimizationSettingsAsync();

        /// <summary>
        /// Gets all notification optimization settings.
        /// </summary>
        /// <returns>Collection of notification optimization settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetNotificationOptimizationSettingsAsync();

        /// <summary>
        /// Gets all sound optimization settings.
        /// </summary>
        /// <returns>Collection of sound optimization settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetSoundOptimizationSettingsAsync();

        /// <summary>
        /// Executes taskbar cleanup operation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExecuteTaskbarCleanupAsync();

        /// <summary>
        /// Executes explorer action asynchronously.
        /// </summary>
        /// <param name="actionId">The action identifier.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ExecuteExplorerActionAsync(string actionId);

        /// <summary>
        /// Gets all Windows theme customization settings.
        /// </summary>
        /// <returns>Collection of Windows theme settings.</returns>
        Task<IEnumerable<ApplicationSetting>> GetWindowsThemeSettingsAsync();

        /// <summary>
        /// Gets the current theme state from the system.
        /// </summary>
        /// <returns>The current theme state.</returns>
        Task<string> GetCurrentThemeStateAsync();

        #endregion
    }

    /// <summary>
    /// Event arguments for setting status change notifications.
    /// </summary>
    public class SettingStatusChangedEventArgs : EventArgs
    {
        public string SettingId { get; }
        public bool IsEnabled { get; }
        public object? CurrentValue { get; }

        public SettingStatusChangedEventArgs(string settingId, bool isEnabled, object? currentValue)
        {
            SettingId = settingId;
            IsEnabled = isEnabled;
            CurrentValue = currentValue;
        }
    }
}

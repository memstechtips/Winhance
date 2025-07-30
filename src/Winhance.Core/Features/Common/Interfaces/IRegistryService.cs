using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Provides access to the Windows registry.
    /// </summary>
    public interface IRegistryService
    {
        /// <summary>
        /// Sets a value in the registry.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <param name="valueName">The name of the value to set.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="valueKind">The type of the value.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        bool SetValue(string keyPath, string valueName, object value, Microsoft.Win32.RegistryValueKind valueKind);

        /// <summary>
        /// Gets a value from the registry.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <param name="valueName">The name of the value to get.</param>
        /// <returns>The value from the registry, or null if it doesn't exist.</returns>
        object? GetValue(string keyPath, string valueName);

        /// <summary>
        /// Deletes a value from the registry.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <param name="valueName">The name of the value to delete.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        bool DeleteValue(string keyPath, string valueName);

        /// <summary>
        /// Deletes a value from the registry using hive and subkey.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="subKey">The registry subkey.</param>
        /// <param name="valueName">The name of the value to delete.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> DeleteValue(RegistryHive hive, string subKey, string valueName);

        /// <summary>
        /// Exports a registry key to a string.
        /// </summary>
        /// <param name="keyPath">The registry key path to export.</param>
        /// <param name="includeSubKeys">Whether to include subkeys in the export.</param>
        /// <returns>The exported registry key as a string.</returns>
        Task<string> ExportKey(string keyPath, bool includeSubKeys);

        /// <summary>
        /// Gets the status of a registry setting.
        /// </summary>
        /// <param name="setting">The registry setting to check.</param>
        /// <returns>The status of the registry setting.</returns>
        Task<RegistrySettingStatus> GetSettingStatusAsync(RegistrySetting setting);

        /// <summary>
        /// Gets the current value of a registry setting.
        /// </summary>
        /// <param name="setting">The registry setting to check.</param>
        /// <returns>The current value of the registry setting, or null if it doesn't exist.</returns>
        Task<object?> GetCurrentValueAsync(RegistrySetting setting);

        /// <summary>
        /// Applies a registry setting with proper Group Policy handling.
        /// </summary>
        /// <param name="setting">The registry setting to apply.</param>
        /// <param name="enable">Whether to enable or disable the setting.</param>
        /// <returns>True if the setting was applied successfully; otherwise, false.</returns>
        Task<bool> ApplySettingAsync(RegistrySetting setting, bool enable);

        /// <summary>
        /// Determines whether a registry key exists.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        bool KeyExists(string keyPath);

        /// <summary>
        /// Creates a registry key.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        bool CreateKey(string keyPath);

        /// <summary>
        /// Creates a registry key if it doesn't exist.
        /// </summary>
        /// <param name="keyPath">The full path to the registry key.</param>
        /// <returns>True if the key exists or was created successfully; otherwise, false.</returns>
        bool CreateKeyIfNotExists(string keyPath);

        /// <summary>
        /// Determines whether a registry value exists.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <param name="valueName">The name of the value to check.</param>
        /// <returns>True if the value exists; otherwise, false.</returns>
        bool ValueExists(string keyPath, string valueName);

        /// <summary>
        /// Deletes a registry key and all its values.
        /// </summary>
        /// <param name="keyPath">The full path to the registry key to delete.</param>
        /// <returns>True if the key was successfully deleted, false otherwise.</returns>
        bool DeleteKey(string keyPath);

        /// <summary>
        /// Deletes a registry key and all its values.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="subKey">The subkey path.</param>
        /// <returns>True if the key was successfully deleted, false otherwise.</returns>
        Task<bool> DeleteKey(RegistryHive hive, string subKey);

        /// <summary>
        /// Tests a registry setting.
        /// </summary>
        /// <param name="keyPath">The registry key path.</param>
        /// <param name="valueName">The name of the value to test.</param>
        /// <param name="desiredValue">The desired value.</param>
        /// <returns>The status of the registry setting.</returns>
        RegistrySettingStatus TestRegistrySetting(string keyPath, string valueName, object desiredValue);

        /// <summary>
        /// Backs up the Windows registry.
        /// </summary>
        /// <param name="backupPath">The path where the backup should be stored.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> BackupRegistry(string backupPath);

        /// <summary>
        /// Restores the Windows registry from a backup.
        /// </summary>
        /// <param name="backupPath">The path to the backup file.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> RestoreRegistry(string backupPath);

        /// <summary>
        /// Applies customization settings to the registry.
        /// </summary>
        /// <param name="settings">The settings to apply.</param>
        /// <returns>True if all settings were applied successfully; otherwise, false.</returns>
        Task<bool> ApplyCustomizations(List<RegistrySetting> settings);

        /// <summary>
        /// Restores customization settings to their default values.
        /// </summary>
        /// <param name="settings">The settings to restore.</param>
        /// <returns>True if all settings were restored successfully; otherwise, false.</returns>
        Task<bool> RestoreCustomizationDefaults(List<RegistrySetting> settings);

        /// <summary>
        /// Applies power plan settings to the registry.
        /// </summary>
        /// <param name="settings">The settings to apply.</param>
        /// <returns>True if all settings were applied successfully; otherwise, false.</returns>
        Task<bool> ApplyPowerPlanSettings(List<PowerCfgSetting> settings);

        /// <summary>
        /// Restores power plan settings to their default values.
        /// </summary>
        /// <returns>True if all settings were restored successfully; otherwise, false.</returns>
        Task<bool> RestoreDefaultPowerSettings();

        /// <summary>
        /// Gets the status of linked registry settings.
        /// </summary>
        /// <param name="linkedSettings">The linked registry settings to check.</param>
        /// <returns>The combined status of the linked registry settings.</returns>
        Task<RegistrySettingStatus> GetLinkedSettingsStatusAsync(LinkedRegistrySettings linkedSettings);

        /// <summary>
        /// Applies linked registry settings.
        /// </summary>
        /// <param name="linkedSettings">The linked registry settings to apply.</param>
        /// <param name="enable">Whether to enable or disable the settings.</param>
        /// <returns>True if all settings were applied successfully; otherwise, false.</returns>
        Task<bool> ApplyLinkedSettingsAsync(LinkedRegistrySettings linkedSettings, bool enable);

        /// <summary>
        /// Clears all registry caches to ensure fresh reads
        /// </summary>
        void ClearRegistryCaches();

        /// <summary>
        /// Gets the status of an optimization setting that may contain multiple registry settings.
        /// </summary>
        /// <param name="setting">The optimization setting to check.</param>
        /// <returns>The combined status of the optimization setting.</returns>
        Task<RegistrySettingStatus> GetOptimizationSettingStatusAsync(Winhance.Core.Features.Optimize.Models.OptimizationSetting setting);

        /// <summary>
        /// Applies an optimization setting that may contain multiple registry settings.
        /// </summary>
        /// <param name="setting">The optimization setting to apply.</param>
        /// <param name="enable">Whether to enable or disable the setting.</param>
        /// <returns>True if the setting was applied successfully; otherwise, false.</returns>
        Task<bool> ApplyOptimizationSettingAsync(Winhance.Core.Features.Optimize.Models.OptimizationSetting setting, bool enable);
    }
}

using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for managing settings across different modules for cross-module dependencies.
    /// </summary>
    public interface IGlobalSettingsRegistry
    {
        /// <summary>
        /// Registers settings from a module.
        /// </summary>
        /// <param name="moduleName">The name of the module (e.g., "StartMenuCustomizations", "WindowsThemeSettings")</param>
        /// <param name="settings">The settings to register</param>
        void RegisterSettings(string moduleName, IEnumerable<ISettingItem> settings);

        /// <summary>
        /// Gets a setting by ID from any module.
        /// </summary>
        /// <param name="settingId">The ID of the setting</param>
        /// <param name="moduleName">Optional module name to search in. If null, searches all modules.</param>
        /// <returns>The setting if found, null otherwise</returns>
        ISettingItem? GetSetting(string settingId, string? moduleName = null);

        /// <summary>
        /// Gets all settings from a specific module.
        /// </summary>
        /// <param name="moduleName">The name of the module</param>
        /// <returns>All settings from the module</returns>
        IEnumerable<ISettingItem> GetModuleSettings(string moduleName);

        /// <summary>
        /// Gets all settings from all modules.
        /// </summary>
        /// <returns>All registered settings</returns>
        IEnumerable<ISettingItem> GetAllSettings();

        /// <summary>
        /// Unregisters all settings from a module.
        /// </summary>
        /// <param name="moduleName">The name of the module</param>
        void UnregisterModule(string moduleName);

        /// <summary>
        /// Clears all registered settings.
        /// </summary>
        void Clear();
    }
}

using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Service for managing settings across different modules for cross-module dependencies.
/// </summary>
public interface IGlobalSettingsRegistry
{
    /// <summary>
    /// Registers settings from a module.
    /// </summary>
    /// <param name="moduleName">The name of the module (e.g., "StartMenuCustomizations", "WindowsThemeCustomizations")</param>
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
    /// Gets all settings from all modules.
    /// </summary>
    /// <returns>All registered settings</returns>
    IEnumerable<ISettingItem> GetAllSettings();

    /// <summary>
    /// Registers a single setting from a module, preserving existing settings.
    /// Used by SettingApplicationService to register settings on-demand during application.
    /// </summary>
    /// <param name="moduleName">The name of the module</param>
    /// <param name="setting">The setting to register</param>
    void RegisterSetting(string moduleName, ISettingItem setting);

}

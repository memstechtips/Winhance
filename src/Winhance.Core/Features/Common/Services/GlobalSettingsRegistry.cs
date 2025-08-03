using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Services
{
    /// <summary>
    /// Service for managing settings across different modules for cross-module dependencies.
    /// Thread-safe implementation using ConcurrentDictionary.
    /// </summary>
    public class GlobalSettingsRegistry : IGlobalSettingsRegistry
    {
        private readonly ConcurrentDictionary<string, List<ISettingItem>> _moduleSettings;
        private readonly ILogService _logService;

        public GlobalSettingsRegistry(ILogService logService)
        {
            _moduleSettings = new ConcurrentDictionary<string, List<ISettingItem>>();
            _logService = logService;
        }

        /// <summary>
        /// Registers settings from a module.
        /// </summary>
        /// <param name="moduleName">The name of the module (e.g., "StartMenuCustomizations", "WindowsThemeSettings")</param>
        /// <param name="settings">The settings to register</param>
        public void RegisterSettings(string moduleName, IEnumerable<ISettingItem> settings)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                _logService.Log(LogLevel.Warning, "Cannot register settings for null or empty module name");
                return;
            }

            var settingsList = settings?.ToList() ?? new List<ISettingItem>();
            _moduleSettings.AddOrUpdate(moduleName, settingsList, (key, oldValue) => settingsList);
            
            _logService.Log(LogLevel.Info, $"Registered {settingsList.Count} settings for module '{moduleName}'");
        }

        /// <summary>
        /// Gets a setting by ID from any module.
        /// </summary>
        /// <param name="settingId">The ID of the setting</param>
        /// <param name="moduleName">Optional module name to search in. If null, searches all modules.</param>
        /// <returns>The setting if found, null otherwise</returns>
        public ISettingItem? GetSetting(string settingId, string? moduleName = null)
        {
            if (string.IsNullOrEmpty(settingId))
            {
                _logService.Log(LogLevel.Warning, "Cannot get setting for null or empty setting ID");
                return null;
            }

            if (!string.IsNullOrEmpty(moduleName))
            {
                // Search in specific module
                if (_moduleSettings.TryGetValue(moduleName, out var moduleSettingsList))
                {
                    var setting = moduleSettingsList.FirstOrDefault(s => s.Id == settingId);
                    if (setting != null)
                    {
                        _logService.Log(LogLevel.Debug, $"Found setting '{settingId}' in module '{moduleName}'");
                        return setting;
                    }
                }
                _logService.Log(LogLevel.Debug, $"Setting '{settingId}' not found in module '{moduleName}'");
                return null;
            }

            // Search in all modules
            foreach (var kvp in _moduleSettings)
            {
                var setting = kvp.Value.FirstOrDefault(s => s.Id == settingId);
                if (setting != null)
                {
                    _logService.Log(LogLevel.Debug, $"Found setting '{settingId}' in module '{kvp.Key}'");
                    return setting;
                }
            }

            _logService.Log(LogLevel.Debug, $"Setting '{settingId}' not found in any module");
            return null;
        }

        /// <summary>
        /// Gets all settings from a specific module.
        /// </summary>
        /// <param name="moduleName">The name of the module</param>
        /// <returns>All settings from the module</returns>
        public IEnumerable<ISettingItem> GetModuleSettings(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                _logService.Log(LogLevel.Warning, "Cannot get settings for null or empty module name");
                return Enumerable.Empty<ISettingItem>();
            }

            if (_moduleSettings.TryGetValue(moduleName, out var settings))
            {
                return settings;
            }

            return Enumerable.Empty<ISettingItem>();
        }

        /// <summary>
        /// Gets all settings from all modules.
        /// </summary>
        /// <returns>All registered settings</returns>
        public IEnumerable<ISettingItem> GetAllSettings()
        {
            return _moduleSettings.Values.SelectMany(settings => settings);
        }

        /// <summary>
        /// Unregisters all settings from a module.
        /// </summary>
        /// <param name="moduleName">The name of the module</param>
        public void UnregisterModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                _logService.Log(LogLevel.Warning, "Cannot unregister null or empty module name");
                return;
            }

            if (_moduleSettings.TryRemove(moduleName, out var removedSettings))
            {
                _logService.Log(LogLevel.Info, $"Unregistered {removedSettings.Count} settings from module '{moduleName}'");
            }
            else
            {
                _logService.Log(LogLevel.Debug, $"Module '{moduleName}' was not registered");
            }
        }

        /// <summary>
        /// Clears all registered settings.
        /// </summary>
        public void Clear()
        {
            var moduleCount = _moduleSettings.Count;
            _moduleSettings.Clear();
            _logService.Log(LogLevel.Info, $"Cleared all settings from {moduleCount} modules");
        }
    }
}

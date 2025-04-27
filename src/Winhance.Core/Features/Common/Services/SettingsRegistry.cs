using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Services
{
    /// <summary>
    /// Registry for settings.
    /// </summary>
    public class SettingsRegistry : ISettingsRegistry
    {
        private readonly ILogService _logService;
        private readonly List<ISettingItem> _settings = new List<ISettingItem>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsRegistry"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public SettingsRegistry(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Registers a setting in the registry.
        /// </summary>
        /// <param name="setting">The setting to register.</param>
        public void RegisterSetting(ISettingItem setting)
        {
            if (setting == null)
            {
                _logService.Log(LogLevel.Warning, "Cannot register null setting");
                return;
            }

            if (string.IsNullOrEmpty(setting.Id))
            {
                _logService.Log(LogLevel.Warning, "Cannot register setting with null or empty ID");
                return;
            }

            lock (_settings)
            {
                // Check if the setting is already registered
                if (_settings.Any(s => s.Id == setting.Id))
                {
                    _logService.Log(LogLevel.Info, $"Setting with ID '{setting.Id}' is already registered");
                    return;
                }

                _settings.Add(setting);
                _logService.Log(LogLevel.Info, $"Registered setting {setting.Id} in global settings collection");
            }
        }

        /// <summary>
        /// Gets a setting by its ID.
        /// </summary>
        /// <param name="id">The ID of the setting to get.</param>
        /// <returns>The setting if found, otherwise null.</returns>
        public ISettingItem? GetSettingById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logService.Log(LogLevel.Warning, "Cannot get setting with null or empty ID");
                return null;
            }

            lock (_settings)
            {
                return _settings.FirstOrDefault(s => s.Id == id);
            }
        }

        /// <summary>
        /// Gets all settings in the registry.
        /// </summary>
        /// <returns>A list of all settings.</returns>
        public List<ISettingItem> GetAllSettings()
        {
            lock (_settings)
            {
                return new List<ISettingItem>(_settings);
            }
        }

        /// <summary>
        /// Gets all settings of a specific type.
        /// </summary>
        /// <typeparam name="T">The type of settings to get.</typeparam>
        /// <returns>A list of settings of the specified type.</returns>
        public List<ISettingItem> GetSettingsByType<T>() where T : ISettingItem
        {
            lock (_settings)
            {
                return _settings.OfType<T>().Cast<ISettingItem>().ToList();
            }
        }
    }
}

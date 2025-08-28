using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Customize.Services
{
    /// <summary>
    /// Service implementation for managing Taskbar customization settings.
    /// Handles taskbar appearance, behavior, and cleanup operations.
    /// Maintains exact same method signatures and behavior for compatibility.
    /// </summary>
    public class TaskbarService : IDomainService
    {
        private readonly  SettingControlHandler _controlHandler;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly ILogService _logService;
        private readonly ICommandService _commandService;
        private readonly IWindowsRegistryService _registryService;

        public string DomainName => FeatureIds.Taskbar;

        public TaskbarService(
             SettingControlHandler controlHandler,
            ISystemSettingsDiscoveryService discoveryService,
            ILogService logService,
            ICommandService commandService,
            IWindowsRegistryService windowsRegistryService
        )
        {
            _controlHandler = controlHandler ?? throw new ArgumentNullException(nameof(controlHandler));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _commandService =
                commandService ?? throw new ArgumentNullException(nameof(commandService));
            _registryService =
                windowsRegistryService ?? throw new ArgumentNullException(nameof(windowsRegistryService));
        }

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Taskbar settings");

                var group = TaskbarCustomizations.GetTaskbarCustomizations();
                return await _discoveryService.GetSettingsWithSystemStateAsync(
                    group.Settings,
                    DomainName
                );
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Taskbar settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }

        /// <summary>
        /// Applies a setting using the controlHandler pattern.
        /// </summary>
        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            var settings = await GetRawSettingsAsync();
            var setting = settings.FirstOrDefault(s => s.Id == settingId);
            if (setting == null)
                throw new ArgumentException($"Setting '{settingId}' not found");

            switch (setting.InputType)
            {
                case SettingInputType.Toggle:
                    await _controlHandler.ApplyBinaryToggleAsync(setting, enable);
                    break;
                case SettingInputType.Selection when value is int index:
                    await _controlHandler.ApplyComboBoxIndexAsync(setting, index);
                    break;
                case SettingInputType.NumericRange when value != null:
                    await _controlHandler.ApplyNumericUpDownAsync(setting, value);
                    break;
                default:
                    throw new NotSupportedException($"Input type '{setting.InputType}' not supported");
            }
        }

        /// <summary>
        /// Checks if a setting is enabled using direct registry operations.
        /// </summary>
        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            try
            {
                var settings = await GetRawSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null || setting.RegistrySettings == null || !setting.RegistrySettings.Any())
                {
                    return false;
                }

                // Check the primary registry setting
                var primarySetting = setting.RegistrySettings.FirstOrDefault(rs => rs.IsPrimary) 
                                   ?? setting.RegistrySettings.First();
                
                var currentValue = _registryService.GetValue(primarySetting.KeyPath, primarySetting.ValueName);
                
                if (currentValue == null)
                {
                    return primarySetting.AbsenceMeansEnabled;
                }

                // Compare against enabled value
                return Equals(currentValue, primarySetting.EnabledValue);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking taskbar setting '{settingId}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current value of a setting using direct registry operations.
        /// </summary>
        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            try
            {
                var settings = await GetRawSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null || setting.RegistrySettings == null || !setting.RegistrySettings.Any())
                {
                    return null;
                }

                // Get value from primary registry setting
                var primarySetting = setting.RegistrySettings.FirstOrDefault(rs => rs.IsPrimary) 
                                   ?? setting.RegistrySettings.First();
                
                return _registryService.GetValue(primarySetting.KeyPath, primarySetting.ValueName) ?? primarySetting.DefaultValue;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting taskbar setting value '{settingId}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Removes all pinned items from the Taskbar by deleting the Taskband registry key.
        /// This action cannot be undone.
        /// </summary>
        public async Task CleanTaskbarAsync()
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    "Starting Taskbar cleanup - deleting Taskband registry key"
                );

                bool success = _registryService.DeleteKey(
                    "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Taskband"
                );

                if (success)
                {
                    _logService.Log(
                        LogLevel.Success,
                        "Successfully deleted Taskband registry key - Taskbar cleaned"
                    );
                }
                else
                {
                    _logService.Log(
                        LogLevel.Warning,
                        "Failed to delete Taskband registry key - may not exist or access denied"
                    );
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error during Taskbar cleanup: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets raw setting configurations without expensive system state discovery.
        /// This method returns only the setting metadata (registry paths, values, dependencies)
        /// without resolving current system state, ComboBox options, or current registry values.
        /// Use this for performance-critical operations where only configuration data is needed.
        /// </summary>
        public async Task<IEnumerable<SettingDefinition>> GetRawSettingsAsync()
        {
            var group = TaskbarCustomizations.GetTaskbarCustomizations();
            return await Task.FromResult(group.Settings);
        }
    }
}

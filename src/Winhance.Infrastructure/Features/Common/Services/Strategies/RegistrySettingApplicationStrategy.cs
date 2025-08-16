using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services.Strategies
{
    /// <summary>
    /// Strategy for applying settings that use registry operations.
    /// </summary>
    public class RegistrySettingApplicationStrategy : ISettingApplicationStrategy
    {
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;
        private readonly ISystemSettingsDiscoveryService _systemSettingsDiscoveryService;
        private readonly IComboBoxValueResolver? _comboBoxResolver;

        public RegistrySettingApplicationStrategy(
            IRegistryService registryService,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService,
            IComboBoxValueResolver? comboBoxResolver = null)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemSettingsDiscoveryService = systemSettingsDiscoveryService ?? throw new ArgumentNullException(nameof(systemSettingsDiscoveryService));
            _comboBoxResolver = comboBoxResolver;
        }

        public bool CanHandle(ApplicationSetting setting)
        {
            return setting.RegistrySettings?.Count > 0;
        }

        /// <summary>
        /// Applies a binary toggle setting.
        /// </summary>
        public virtual async Task ApplyBinaryToggleAsync(ApplicationSetting setting, bool enable)
        {
            // Apply registry settings (both Optimize and Customize use these)
            if (setting.RegistrySettings?.Count > 0)
            {
                foreach (var registrySetting in setting.RegistrySettings)
                {
                    await _registryService.ApplySettingAsync(registrySetting, enable);
                }
            }
        }

        /// <summary>
        /// Applies a ComboBox setting using the centralized resolver pattern.
        /// </summary>
        public virtual async Task ApplyComboBoxIndexAsync(ApplicationSetting setting, int comboBoxIndex)
        {
            try
            {
                _logService.Log(LogLevel.Info, 
                    $"Applying ComboBox setting '{setting.Id}' with index {comboBoxIndex} using centralized resolution");

                if (_comboBoxResolver == null)
                {
                    throw new InvalidOperationException(
                        $"Setting '{setting.Id}' has ComboBox settings but no ComboBox resolver was provided");
                }

                if (!_comboBoxResolver.CanResolve(setting))
                {
                    throw new InvalidOperationException(
                        $"ComboBox setting '{setting.Id}' cannot be resolved by the domain's ComboBox resolver");
                }

                await _comboBoxResolver.ApplyIndexAsync(setting, comboBoxIndex);
                
                _logService.Log(LogLevel.Info, 
                    $"Successfully applied ComboBox setting '{setting.Id}' with index {comboBoxIndex}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, 
                    $"Error applying ComboBox setting '{setting.Id}' with index {comboBoxIndex}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Applies a numeric up/down setting with a specific value.
        /// </summary>
        public virtual async Task ApplyNumericUpDownAsync(ApplicationSetting setting, object value)
        {
            try
            {
                // Handle different numeric types
                var numericValue = value switch
                {
                    int intVal => intVal,
                    double doubleVal => (int)doubleVal,
                    float floatVal => (int)floatVal,
                    string stringVal when int.TryParse(stringVal, out int parsed) => parsed,
                    _ => throw new ArgumentException($"Cannot convert '{value}' to numeric value for setting '{setting.Id}'")
                };

                if (setting.RegistrySettings?.Count > 0)
                {
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        _registryService.SetValue(
                            $"{registrySetting.Hive}\\{registrySetting.SubKey}",
                            registrySetting.Name,
                            numericValue,
                            RegistryValueKind.DWord
                        );
                    }
                }

                _logService.Log(LogLevel.Info, $"Applied numeric setting '{setting.Id}' with value: {numericValue}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying numeric setting '{setting.Id}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the current status of a setting.
        /// </summary>
        public virtual async Task<bool> GetSettingStatusAsync(string settingId, IEnumerable<ApplicationSetting> settings)
        {
            try
            {
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting != null)
                {
                    // Use system settings discovery for accurate state detection
                    var state = await _systemSettingsDiscoveryService.GetCurrentSettingsStateAsync(new[] { setting });
                    return state.TryGetValue(settingId, out var isEnabled) ? isEnabled : false;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error checking setting '{settingId}': {ex.Message}"
                );
                return false;
            }
        }

        /// <summary>
        /// Gets the current value of a setting.
        /// </summary>
        public virtual async Task<object?> GetSettingValueAsync(string settingId, IEnumerable<ApplicationSetting> settings)
        {
            try
            {
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting?.RegistrySettings?.Count > 0)
                {
                    return await _registryService.GetCurrentValueAsync(setting.RegistrySettings[0]);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error getting setting value '{settingId}': {ex.Message}"
                );
                return null;
            }
        }
    }
}

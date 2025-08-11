using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Base service for system settings manipulation shared between Optimize and Customize features.
    /// Handles common patterns for registry and command-based settings across all domains.
    /// Follows DRY principle by centralizing common implementation logic.
    /// </summary>
    public abstract class BaseSystemSettingsService : IDomainService
    {
        protected readonly IRegistryService _registryService;
        protected readonly ICommandService _commandService;
        protected readonly ILogService _logService;
        protected readonly ISystemSettingsDiscoveryService _systemSettingsDiscoveryService;

        /// <summary>
        /// Gets the domain name for this service (must be implemented by derived classes).
        /// </summary>
        public abstract string DomainName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseSystemSettingsService"/> class.
        /// </summary>
        /// <param name="registryService">The registry service for registry manipulations.</param>
        /// <param name="commandService">The command service for command-based settings.</param>
        /// <param name="logService">The log service for logging operations.</param>
        /// <param name="systemSettingsDiscoveryService">The system settings discovery service.</param>
        protected BaseSystemSettingsService(
            IRegistryService registryService,
            ICommandService commandService,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemSettingsDiscoveryService = systemSettingsDiscoveryService ?? throw new ArgumentNullException(nameof(systemSettingsDiscoveryService));
        }

        /// <summary>
        /// Gets all settings for this domain (must be implemented by derived classes).
        /// </summary>
        /// <returns>Collection of application settings for this domain.</returns>
        public abstract Task<IEnumerable<ApplicationSetting>> GetSettingsAsync();

        /// <summary>
        /// Applies a setting with the specified value. Handles all control types uniformly.
        /// </summary>
        /// <param name="settingId">The ID of the setting to apply.</param>
        /// <param name="enable">Whether to enable or disable the setting.</param>
        /// <param name="value">The value to apply (used for ComboBox and NumericUpDown controls).</param>
        public virtual async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying {DomainName} setting '{settingId}': enable={enable}, value={value}"
                );

                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException(
                        $"Setting '{settingId}' not found in {DomainName} domain"
                    );
                }

                // Apply setting based on control type
                switch (setting.ControlType)
                {
                    case ControlType.BinaryToggle:
                        await ApplyBinaryToggleAsync(setting, enable);
                        break;
                    case ControlType.ComboBox:
                        if (value != null)
                        {
                            await ApplyComboBoxAsync(setting, value);
                        }
                        else
                        {
                            // Fallback to toggle behavior if no value provided
                            await ApplyBinaryToggleAsync(setting, enable);
                        }
                        break;
                    case ControlType.NumericUpDown:
                        if (value != null)
                        {
                            await ApplyNumericUpDownAsync(setting, value);
                        }
                        else
                        {
                            throw new ArgumentException(
                                $"NumericUpDown setting '{settingId}' requires a value"
                            );
                        }
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Control type '{setting.ControlType}' is not supported"
                        );
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Successfully applied {DomainName} setting '{settingId}'"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying {DomainName} setting '{settingId}': {ex.Message}"
                );
                throw;
            }
        }

        /// <summary>
        /// Applies a binary toggle setting (used by both Optimize and Customize domains).
        /// </summary>
        /// <param name="setting">The setting to apply.</param>
        /// <param name="enable">Whether to enable or disable the setting.</param>
        protected virtual async Task ApplyBinaryToggleAsync(ApplicationSetting setting, bool enable)
        {
            // Apply registry settings (both Optimize and Customize use these)
            if (setting.RegistrySettings?.Count > 0)
            {
                foreach (var registrySetting in setting.RegistrySettings)
                {
                    await _registryService.ApplySettingAsync(registrySetting, enable);
                }
            }

            // Apply command settings (primarily Optimize, but Customize can safely ignore)
            if (setting.CommandSettings?.Count > 0)
            {
                foreach (var commandSetting in setting.CommandSettings)
                {
                    await _commandService.ApplyCommandSettingsAsync(new[] { commandSetting }, enable);
                }
            }
        }

        /// <summary>
        /// Applies a combobox setting with a specific value (used by both domains).
        /// </summary>
        /// <param name="setting">The setting to apply.</param>
        /// <param name="value">The value to set.</param>
        protected virtual async Task ApplyComboBoxAsync(ApplicationSetting setting, object value)
        {
            try
            {
                int intValue = Convert.ToInt32(value);

                // Check if this is a complex ComboBox with value mappings
                if (setting.CustomProperties?.TryGetValue("ValueMappings", out var mappingsObj) == true &&
                    mappingsObj is Dictionary<int, Dictionary<string, int>> valueMappings)
                {
                    await ApplyComplexComboBoxAsync(setting, intValue, valueMappings);
                }
                else
                {
                    // Standard ComboBox - apply the same value to all registry settings
                    if (setting.RegistrySettings?.Count > 0)
                    {
                        foreach (var registrySetting in setting.RegistrySettings)
                        {
                            _registryService.SetValue(
                                $"{registrySetting.Hive}\\{registrySetting.SubKey}",
                                registrySetting.Name,
                                intValue,
                                RegistryValueKind.DWord
                            );
                        }
                    }
                }

                _logService.Log(LogLevel.Info, $"Applied combobox setting '{setting.Id}' with value: {intValue}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying combobox setting '{setting.Id}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Applies a complex combobox setting with value mappings for different registry settings.
        /// </summary>
        /// <param name="setting">The setting to apply.</param>
        /// <param name="selectedValue">The selected ComboBox value.</param>
        /// <param name="valueMappings">The value mappings dictionary.</param>
        protected virtual async Task ApplyComplexComboBoxAsync(
            ApplicationSetting setting, 
            int selectedValue, 
            Dictionary<int, Dictionary<string, int>> valueMappings)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying complex ComboBox setting with value: {selectedValue}");

                // Get the registry values for the selected level
                if (valueMappings.TryGetValue(selectedValue, out var registryValues))
                {
                    // Apply each registry value
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        if (registryValues.TryGetValue(registrySetting.Name, out var regValue))
                        {
                            _logService.Log(LogLevel.Info, $"Setting registry value {registrySetting.Name} to {regValue}");
                            
                            _registryService.SetValue(
                                $"{registrySetting.Hive}\\{registrySetting.SubKey}",
                                registrySetting.Name,
                                regValue,
                                RegistryValueKind.DWord
                            );
                        }
                    }
                }
                else
                {
                    throw new ArgumentException($"No registry value mappings found for ComboBox value {selectedValue}");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying complex ComboBox setting: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Applies a numeric up/down setting with a specific value (primarily used by Power Optimize).
        /// </summary>
        /// <param name="setting">The setting to apply.</param>
        /// <param name="value">The numeric value to set.</param>
        protected virtual async Task ApplyNumericUpDownAsync(ApplicationSetting setting, object value)
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

                // Apply command settings if present (for power settings)
                if (setting.CommandSettings?.Count > 0)
                {
                    foreach (var commandSetting in setting.CommandSettings)
                    {
                        // For numeric settings, we typically enable the command with the numeric value
                        // The actual command should contain the value or be constructed appropriately
                        await _commandService.ApplyCommandSettingsAsync(new[] { commandSetting }, true);
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
        /// Gets the current status of a setting (enabled/disabled).
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <returns>True if the setting is enabled, false otherwise.</returns>
        public virtual async Task<bool> GetSettingStatusAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
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
                    $"Error checking {DomainName} setting '{settingId}': {ex.Message}"
                );
                return false;
            }
        }

        /// <summary>
        /// Gets the current value of a setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting to get the value for.</param>
        /// <returns>The current value of the setting, or null if not found.</returns>
        public virtual async Task<object?> GetSettingValueAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
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
                    $"Error getting {DomainName} setting value '{settingId}': {ex.Message}"
                );
                return null;
            }
        }

        /// <summary>
        /// Checks if a setting is currently enabled.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <returns>True if the setting is enabled, false otherwise.</returns>
        public virtual async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
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
                    $"Error checking if setting '{settingId}' is enabled: {ex.Message}"
                );
                return false;
            }
        }

        /// <summary>
        /// Helper method to load settings with their current system state.
        /// Common pattern used by all domain services.
        /// </summary>
        /// <param name="originalSettings">The original settings from the domain models.</param>
        /// <returns>Settings updated with current system state.</returns>
        protected async Task<IEnumerable<ApplicationSetting>> GetSettingsWithSystemStateAsync(
            IEnumerable<OptimizationSetting> originalSettings)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Loading {DomainName} settings with system state");
                
                var settings = originalSettings.ToList();

                // Initialize settings with their actual system state
                var systemStates = await _systemSettingsDiscoveryService.GetCurrentSettingsStateAndValuesAsync(settings);
        
                // Create new settings with updated IsInitiallyEnabled values
                var updatedSettings = new List<ApplicationSetting>();
        
                foreach (var originalSetting in settings)
                {
                    if (systemStates.TryGetValue(originalSetting.Id, out var state))
                    {
                        // Create a new instance with the same properties but updated IsInitiallyEnabled
                        var updatedSetting = originalSetting with 
                        { 
                            IsInitiallyEnabled = state.IsEnabled,
                            CurrentValue = state.CurrentValue,
                            IsEnabled = state.IsEnabled // Use actual system state for IsEnabled as well
                        };
        
                        updatedSettings.Add(updatedSetting);
                    }
                    else
                    {
                        _logService.Log(LogLevel.Warning, 
                            $"No system state found for setting '{originalSetting.Id}', using defaults");
                        updatedSettings.Add(originalSetting);
                    }
                }

                return updatedSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading {DomainName} settings: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }
    }
}
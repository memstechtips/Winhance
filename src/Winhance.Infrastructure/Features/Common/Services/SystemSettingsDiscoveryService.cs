using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for discovering current system settings state without side effects.
    /// Coordinates between RegistryService and CommandService to query system state during initialization.
    /// </summary>
    public class SystemSettingsDiscoveryService : ISystemSettingsDiscoveryService
    {
        private readonly IRegistryService _registryService;
        private readonly ICommandService _commandService;
        private readonly IComboBoxDiscoveryService _comboBoxDiscoveryService;
        private readonly ILogService _logService;

        public SystemSettingsDiscoveryService(
            IRegistryService registryService,
            ICommandService commandService,
            IComboBoxDiscoveryService comboBoxDiscoveryService,
            ILogService logService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _comboBoxDiscoveryService = comboBoxDiscoveryService ?? throw new ArgumentNullException(nameof(comboBoxDiscoveryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, bool>> GetCurrentSettingsStateAsync(IEnumerable<ApplicationSetting> settings)
        {
            var results = new Dictionary<string, bool>();

            if (settings == null)
            {
                _logService.LogWarning("GetCurrentSettingsStateAsync called with null settings");
                return results;
            }

            // Only log once at the beginning of settings discovery
            _logService.Log(LogLevel.Info, "Starting system settings state discovery");

            foreach (var setting in settings)
            {
                try
                {
                    bool isEnabled = false;

                    // Check registry-based settings
                    if (setting.RegistrySettings != null && setting.RegistrySettings.Count > 0)
                    {
                        if (setting.RegistrySettings.Count == 1)
                        {
                            // Single registry setting
                            var status = await _registryService.GetSettingStatusAsync(setting.RegistrySettings[0]);
                            isEnabled = status == RegistrySettingStatus.Applied;
                        }
                        else
                        {
                            // Multiple registry settings - use linked settings logic
                            var linkedSettings = setting.CreateLinkedRegistrySettings();
                            var status = await _registryService.GetLinkedSettingsStatusAsync(linkedSettings);
                            isEnabled = status == RegistrySettingStatus.Applied;
                        }
                    }
                    // Check command-based settings
                    else if (setting.CommandSettings?.Any() == true)
                    {
                        // For command settings, we need to determine if they're currently applied
                        // This might require checking specific registry keys or system state
                        // For now, we'll assume they're not applied (can be enhanced later)
                        isEnabled = false;
                    }
                    else
                    {
                        _logService.LogWarning($"Setting '{setting.Id}' has no registry or command configuration");
                        isEnabled = false;
                    }

                    results[setting.Id] = isEnabled;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error checking state for setting '{setting.Id}': {ex.Message}");
                    results[setting.Id] = false; // Default to disabled on error
                }
            }

            _logService.Log(LogLevel.Info, $"System settings state discovery completed. Checked {results.Count} settings");
            return results;
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object?>> GetCurrentSettingsValuesAsync(IEnumerable<ApplicationSetting> settings)
        {
            var results = new Dictionary<string, object?>();

            if (settings == null)
            {
                _logService.LogWarning("GetCurrentSettingsValuesAsync called with null settings");
                return results;
            }

            // Only log once at the beginning of values discovery
            _logService.Log(LogLevel.Info, "Starting system settings values discovery");

            foreach (var setting in settings)
            {
                try
                {
                    object? currentValue = null;

                    // Handle ComboBox settings using improved architecture
                    if (setting.ControlType == ControlType.ComboBox)
                    {
                        _logService.Log(LogLevel.Info, $"[INFO] Processing ComboBox setting '{setting.Id}' using domain resolvers");
                        currentValue = await _comboBoxDiscoveryService.ResolveCurrentIndexAsync(setting);
                        _logService.Log(LogLevel.Info, $"[INFO] ComboBox setting '{setting.Id}' current value discovered: {currentValue}");
                    }

                    else
                    {
                        // For Toggle settings, the value is typically the enabled/disabled state
                        currentValue = null;
                    }

                    results[setting.Id] = currentValue;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error getting value for setting '{setting.Id}': {ex.Message}");
                    results[setting.Id] = null; // Default to null on error
                }
            }

            _logService.Log(LogLevel.Info, $"System settings values discovery completed. Checked {results.Count} settings");
            return results;
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, (bool IsEnabled, object? CurrentValue)>> GetCurrentSettingsStateAndValuesAsync(IEnumerable<ApplicationSetting> settings)
        {
            var results = new Dictionary<string, (bool IsEnabled, object? CurrentValue)>();

            if (settings == null)
            {
                _logService.LogWarning("GetCurrentSettingsStateAndValuesAsync called with null settings");
                return results;
            }

            // Only log once at the beginning of combined discovery
            _logService.Log(LogLevel.Info, "Starting combined system settings state and values discovery");

            // Get both state and values in parallel for efficiency
            var stateTask = GetCurrentSettingsStateAsync(settings);
            var valuesTask = GetCurrentSettingsValuesAsync(settings);

            await Task.WhenAll(stateTask, valuesTask);

            var states = await stateTask;
            var values = await valuesTask;

            // Combine the results
            foreach (var setting in settings)
            {
                var isEnabled = states.TryGetValue(setting.Id, out var enabled) ? enabled : false;
                var currentValue = values.TryGetValue(setting.Id, out var value) ? value : null;

                results[setting.Id] = (isEnabled, currentValue);
            }

            _logService.Log(LogLevel.Info, $"Combined system settings discovery completed. Processed {results.Count} settings");
            return results;
        }




        /// <inheritdoc/>
        public async Task<Dictionary<string, Dictionary<RegistrySetting, object?>>> GetIndividualRegistryValuesAsync(IEnumerable<ApplicationSetting> settings)
        {
            var results = new Dictionary<string, Dictionary<RegistrySetting, object?>>();

            if (settings == null)
            {
                _logService.LogWarning("GetIndividualRegistryValuesAsync called with null settings");
                return results;
            }

            _logService.Log(LogLevel.Info, "Starting individual registry values discovery for tooltips");

            foreach (var setting in settings)
            {
                try
                {
                    var individualValues = new Dictionary<RegistrySetting, object?>();

                    // Get individual values for each registry setting
                    if (setting.RegistrySettings != null && setting.RegistrySettings.Count > 0)
                    {
                        foreach (var registrySetting in setting.RegistrySettings)
                        {
                            var currentValue = await GetRegistryValueAsync(registrySetting);
                            individualValues[registrySetting] = currentValue;
                            
                            _logService.Log(LogLevel.Debug, 
                                $"Individual registry value for '{setting.Id}' - '{registrySetting.Name}': {currentValue}");
                        }
                    }

                    results[setting.Id] = individualValues;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error getting individual registry values for setting '{setting.Id}': {ex.Message}");
                    results[setting.Id] = new Dictionary<RegistrySetting, object?>();
                }
            }

            _logService.Log(LogLevel.Info, $"Individual registry values discovery completed. Processed {results.Count} settings");
            return results;
        }

        /// <summary>
        /// Helper method to get the current value from a registry setting.
        /// </summary>
        /// <param name="registrySetting">The registry setting to read</param>
        /// <returns>The current registry value</returns>
        private async Task<object?> GetRegistryValueAsync(RegistrySetting registrySetting)
        {
            try
            {
                // Use RegistryService to get the current value
                var currentValue = await _registryService.GetCurrentValueAsync(registrySetting);
                
                // Removed excessive debug logging for registry value retrieval
                
                return currentValue;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error reading registry value for '{registrySetting.Name}': {ex.Message}");
                return null;
            }
        }
    }
}

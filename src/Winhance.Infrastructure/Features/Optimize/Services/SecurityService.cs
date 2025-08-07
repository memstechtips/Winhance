using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service implementation for managing Windows security optimization settings.
    /// Handles UAC, Windows Defender, and security-related optimizations.
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private readonly IRegistryService _registryService;
        private readonly ICommandService _commandService;
        private readonly ILogService _logService;
        private readonly ISystemSettingsDiscoveryService _systemSettingsDiscoveryService;

        public string DomainName => "Security";

        public SecurityService(
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

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Security settings with system state");
                
                var optimizations = WindowsSecurityOptimizations.GetWindowsSecurityOptimizations();
                var settings = optimizations.Settings.ToList();

                // Initialize settings with their actual system state
                var systemStates = await _systemSettingsDiscoveryService.GetCurrentSettingsStateAndValuesAsync(settings);

                // Create new settings with updated IsInitiallyEnabled values
                var updatedSettings = new List<ApplicationSetting>();
                foreach (var originalSetting in settings)
                {
                    if (systemStates.TryGetValue(originalSetting.Id, out var state))
                    {
                        var updatedSetting = originalSetting with
                        {
                            IsInitiallyEnabled = state.IsEnabled,
                            CurrentValue = state.CurrentValue,
                            IsEnabled = true // Settings are always enabled for interaction
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
                    $"Error loading Security settings: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(
                    LogLevel.Debug,
                    $"[DEBUG] ApplySettingAsync called: settingId='{settingId}', enable={enable}, value={value} (type: {value?.GetType().Name})"
                );

                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    _logService.Log(LogLevel.Debug, $"[DEBUG] Setting '{settingId}' not found in Security domain");
                    throw new ArgumentException(
                        $"Setting '{settingId}' not found in Security domain"
                    );
                }

                _logService.Log(LogLevel.Debug, $"[DEBUG] Found setting: Id='{setting.Id}', Name='{setting.Name}', ControlType={setting.ControlType}");

                // Handle combobox settings with specific values
                if (setting.ControlType == ControlType.ComboBox && value != null)
                {
                    _logService.Log(LogLevel.Debug, $"[DEBUG] Detected combobox setting, calling ApplyComboBoxSettingAsync");
                    await ApplyComboBoxSettingAsync(setting, value);
                }
                else
                {
                    _logService.Log(LogLevel.Debug, $"[DEBUG] Handling as toggle setting (non-combobox or null value)");
                    // Handle toggle settings with boolean enable/disable
                    if (setting.RegistrySettings?.Count > 0)
                    {
                        foreach (var registrySetting in setting.RegistrySettings)
                        {
                            await _registryService.ApplySettingAsync(registrySetting, enable);
                        }
                    }

                    if (setting.CommandSettings?.Count > 0)
                    {
                        foreach (var commandSetting in setting.CommandSettings)
                        {
                            await _commandService.ApplyCommandSettingsAsync(new[] { commandSetting }, enable);
                        }
                    }
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Successfully applied Security setting '{settingId}'"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying Security setting '{settingId}': {ex.Message}"
                );
                throw;
            }
        }

        public async Task<bool> GetSettingStatusAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException(
                        $"Setting '{settingId}' not found in Security domain"
                    );
                }

                if (setting.RegistrySettings?.Count > 0)
                {
                    // Check if the first registry setting is enabled
                    var registrySetting = setting.RegistrySettings.First();
                    var status = await _registryService.GetSettingStatusAsync(registrySetting);
                    return status == RegistrySettingStatus.Applied;
                }

                if (setting.CommandSettings?.Count > 0)
                {
                    // For command settings, we'll return false as default
                    return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error getting status for Security setting '{settingId}': {ex.Message}"
                );
                return false;
            }
        }

        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException(
                        $"Setting '{settingId}' not found in Security domain"
                    );
                }

                if (setting.RegistrySettings?.Count > 0)
                {
                    var registrySetting = setting.RegistrySettings.First();
                    return await _registryService.GetCurrentValueAsync(registrySetting);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error getting value for Security setting '{settingId}': {ex.Message}"
                );
                return null;
            }
        }

        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            return await GetSettingStatusAsync(settingId);
        }

        private async Task ApplyComboBoxSettingAsync(ApplicationSetting setting, object value)
        {
            try
            {
                int intValue = Convert.ToInt32(value);
                _logService.Log(LogLevel.Debug, $"[DEBUG] ApplyComboBoxSettingAsync called: setting.Id='{setting.Id}', value={intValue}");

                // Log CustomProperties for debugging
                if (setting.CustomProperties != null)
                {
                    _logService.Log(LogLevel.Debug, $"[DEBUG] Setting has {setting.CustomProperties.Count} custom properties");
                    foreach (var prop in setting.CustomProperties)
                    {
                        _logService.Log(LogLevel.Debug, $"[DEBUG] CustomProperty: {prop.Key} = {prop.Value}");
                    }
                }
                else
                {
                    _logService.Log(LogLevel.Debug, $"[DEBUG] Setting has no custom properties");
                }

                // Handle combobox settings with value mappings
                if (setting.CustomProperties?.ContainsKey("ValueMappings") == true)
                {
                    _logService.Log(LogLevel.Debug, $"[DEBUG] Found ValueMappings, calling ApplyComboboxWithValueMappingsAsync");
                    await ApplyComboboxWithValueMappingsAsync(setting, intValue);
                    return;
                }

                _logService.Log(LogLevel.Debug, $"[DEBUG] No ValueMappings found, using generic combobox handling");

                // Generic combobox handling for simple settings
                if (setting.RegistrySettings?.Count > 0)
                {
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        string hiveName = registrySetting.Hive switch
                        {
                            RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
                            RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
                            RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
                            RegistryHive.Users => "HKEY_USERS",
                            RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
                            _ => registrySetting.Hive.ToString()
                        };
                        
                        _logService.Log(LogLevel.Debug, $"[DEBUG] Setting registry: {hiveName}\\{registrySetting.SubKey}\\{registrySetting.Name} = {intValue}");
                        
                        _registryService.SetValue(
                            $"{hiveName}\\{registrySetting.SubKey}",
                            registrySetting.Name,
                            intValue,
                            RegistryValueKind.DWord
                        );
                    }
                }

                _logService.Log(LogLevel.Info, $"Successfully applied combobox setting '{setting.Id}' with value: {intValue}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying combobox setting '{setting.Id}': {ex.Message}");
                throw;
            }
        }

        private async Task ApplyComboboxWithValueMappingsAsync(ApplicationSetting setting, int comboboxValue)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"[DEBUG] ApplyComboboxWithValueMappingsAsync called: setting.Id='{setting.Id}', comboboxValue={comboboxValue}");
                
                var valueMappings = setting.CustomProperties["ValueMappings"] as Dictionary<int, Dictionary<string, int>>;
                _logService.Log(LogLevel.Debug, $"[DEBUG] ValueMappings contains {valueMappings?.Count ?? 0} entries");
                
                if (!valueMappings.TryGetValue(comboboxValue, out var registryValueMappings))
                {
                    _logService.Log(LogLevel.Error, $"[ERROR] Invalid combobox value {comboboxValue} for setting {setting.Id}. Available values: {string.Join(", ", valueMappings?.Keys?.ToArray() ?? new int[0])}");
                    throw new ArgumentException($"Invalid combobox value {comboboxValue} for setting {setting.Id}");
                }

                _logService.Log(LogLevel.Debug, $"[DEBUG] Found {registryValueMappings.Count} registry value mappings for combobox value {comboboxValue}");
                foreach (var mapping in registryValueMappings)
                {
                    _logService.Log(LogLevel.Debug, $"[DEBUG] Registry mapping: {mapping.Key} = {mapping.Value}");
                }

                // Apply the mapped values to each registry setting
                foreach (var registrySetting in setting.RegistrySettings ?? new List<RegistrySetting>())
                {
                    string hiveName = registrySetting.Hive switch
                    {
                        RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
                        RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
                        RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
                        RegistryHive.Users => "HKEY_USERS",
                        RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
                        _ => registrySetting.Hive.ToString()
                    };

                    _logService.Log(LogLevel.Debug, $"[DEBUG] Processing registry setting: {registrySetting.Name}");

                    // Get the specific value for this registry setting name
                    if (registryValueMappings.TryGetValue(registrySetting.Name, out int valueToSet))
                    {
                        _logService.Log(LogLevel.Debug, $"[DEBUG] Setting registry: {hiveName}\\{registrySetting.SubKey}\\{registrySetting.Name} = {valueToSet}");
                        
                        _registryService.SetValue(
                            $"{hiveName}\\{registrySetting.SubKey}",
                            registrySetting.Name,
                            valueToSet,
                            RegistryValueKind.DWord
                        );
                        
                        _logService.Log(LogLevel.Info, $"Set {registrySetting.Name} = {valueToSet}");
                    }
                    else
                    {
                        _logService.Log(LogLevel.Debug, $"[DEBUG] No mapping found for registry setting: {registrySetting.Name}");
                    }
                }

                _logService.Log(LogLevel.Info, $"Successfully applied combobox setting '{setting.Id}' with value mappings");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying combobox setting with value mappings '{setting.Id}': {ex.Message}");
                throw;
            }
        }
    }
}

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
    /// Service implementation for managing gaming and performance optimization settings.
    /// Handles game mode, performance tweaks, and gaming-related optimizations.
    /// </summary>
    public class GamingPerformanceService : IGamingPerformanceService
    {
        private readonly IRegistryService _registryService;
        private readonly ICommandService _commandService;
        private readonly ILogService _logService;
        private readonly ISystemSettingsDiscoveryService _systemSettingsDiscoveryService;

        public string DomainName => "GamingPerformance";

        public GamingPerformanceService(
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
                _logService.Log(LogLevel.Info, "Loading Gaming & Performance settings with system state");
                
                var optimizations = GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
                var settings = optimizations.Settings.ToList();

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
        
                        _logService.Log(LogLevel.Debug, 
                            $"Setting '{originalSetting.Id}' initialized: IsInitiallyEnabled={state.IsEnabled}, CurrentValue={state.CurrentValue}");
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
                    $"Error loading Gaming & Performance settings: {ex.Message}"
                );
                return Enumerable.Empty<ApplicationSetting>();
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying Gaming & Performance setting '{settingId}': enable={enable}, value={value}"
                );

                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException(
                        $"Setting '{settingId}' not found in Gaming & Performance domain"
                    );
                }

                // Handle combobox settings with specific values
                if (setting.ControlType == ControlType.ComboBox && value != null)
                {
                    await ApplyComboBoxSettingAsync(setting, value);
                }
                else
                {
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
                    $"Successfully applied Gaming & Performance setting '{settingId}'"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying Gaming & Performance setting '{settingId}': {ex.Message}"
                );
                throw;
            }
        }

        private async Task ApplyComboBoxSettingAsync(ApplicationSetting setting, object value)
        {
            try
            {
                int intValue = Convert.ToInt32(value);
                _logService.Log(LogLevel.Info, $"Applying combobox setting '{setting.Id}' with value: {intValue}");

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

                _logService.Log(LogLevel.Info, $"Successfully applied combobox setting '{setting.Id}' with value: {intValue}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying combobox setting '{setting.Id}': {ex.Message}");
                throw;
            }
        }

        public async Task<bool> GetSettingStatusAsync(string settingId)
        {
            try
            {
                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting != null)
                {
                    // Use system settings discovery for more accurate state detection
                    var state = await _systemSettingsDiscoveryService.GetCurrentSettingsStateAsync(new[] { setting });
                    return state.TryGetValue(settingId, out var isEnabled) ? isEnabled : false;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error checking Gaming & Performance setting '{settingId}': {ex.Message}"
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
                    $"Error getting Gaming & Performance setting value '{settingId}': {ex.Message}"
                );
                return null;
            }
        }
        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Checking if setting '{settingId}' is enabled");
                
                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                if (setting != null)
                {
                    // Use system settings discovery for more accurate state detection
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
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces.WindowsRegistry;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Models.WindowsRegistry;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Infrastructure implementation of tooltip data service.
    /// Provides direct, reliable tooltip data retrieval without event dependencies.
    /// Follows SRP by handling only tooltip data operations.
    /// </summary>
    public class TooltipDataService : ITooltipDataService
    {
        private readonly IWindowsRegistryService _registryService;
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the TooltipDataService
        /// </summary>
        /// <param name="registryService">The registry service</param>
        /// <param name="logService">The log service</param>
        public TooltipDataService(
            IWindowsRegistryService windowsRegistryService,
            ILogService logService)
        {
            _registryService = windowsRegistryService ?? throw new ArgumentNullException(nameof(windowsRegistryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Gets tooltip data for the specified settings
        /// </summary>
        /// <param name="settings">The settings to get tooltip data for</param>
        /// <returns>A dictionary mapping setting IDs to tooltip data</returns>
        public async Task<Dictionary<string, SettingTooltipData>> GetTooltipDataAsync(IEnumerable<SettingDefinition> settings)
        {
            var tooltipData = new Dictionary<string, SettingTooltipData>();

            try
            {
                foreach (var setting in settings)
                {
                    var data = await GetTooltipDataForSettingAsync(setting);
                    if (data != null)
                    {
                        tooltipData[setting.Id] = data;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting tooltip data: {ex.Message}");
            }

            return tooltipData;
        }

        /// <summary>
        /// Refreshes tooltip data for a specific setting by retrieving current registry values
        /// </summary>
        /// <param name="settingId">The ID of the setting to refresh</param>
        /// <param name="setting">The application setting model</param>
        /// <returns>Updated tooltip data for the setting, or null if not found</returns>
        public async Task<SettingTooltipData?> RefreshTooltipDataAsync(string settingId, SettingDefinition setting)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"Refreshing tooltip data for setting: {settingId}");
                
                var data = await GetTooltipDataForSettingAsync(setting);
                if (data != null)
                {
                    _logService.Log(LogLevel.Debug, $"Successfully refreshed tooltip data for setting: {settingId}");
                }
                return data;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing tooltip data for setting {settingId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Refreshes tooltip data for multiple settings efficiently
        /// </summary>
        /// <param name="settings">The settings to refresh tooltip data for</param>
        /// <returns>A dictionary mapping setting IDs to updated tooltip data</returns>
        public async Task<Dictionary<string, SettingTooltipData>> RefreshMultipleTooltipDataAsync(IEnumerable<SettingDefinition> settings)
        {
            var tooltipData = new Dictionary<string, SettingTooltipData>();

            try
            {
                _logService.Log(LogLevel.Debug, $"Refreshing tooltip data for {settings.Count()} settings");

                foreach (var setting in settings)
                {
                    var data = await GetTooltipDataForSettingAsync(setting);
                    if (data != null)
                    {
                        tooltipData[setting.Id] = data;
                    }
                }

                _logService.Log(LogLevel.Debug, $"Successfully refreshed tooltip data for {tooltipData.Count} settings");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing multiple tooltip data: {ex.Message}");
            }

            return tooltipData;
        }

        /// <summary>
        /// Gets tooltip data for a single setting by reading current registry values
        /// </summary>
        /// <param name="setting">The setting to get tooltip data for</param>
        /// <returns>The tooltip data</returns>
        private async Task<SettingTooltipData?> GetTooltipDataForSettingAsync(SettingDefinition setting)
        {
            if (setting.RegistrySettings == null || !setting.RegistrySettings.Any())
                return null;

            try
            {
                var registrySettings = setting.RegistrySettings.ToList();
                var individualValues = new Dictionary<RegistrySetting, object?>();
                var primaryRegistrySetting = registrySettings.First();
                string primaryDisplayValue = "(not set)";

                _logService.Log(LogLevel.Debug, 
                    $"Processing tooltip data for setting {setting.Id} with {registrySettings.Count} registry settings");

                // Process all registry settings for this application setting
                foreach (var registrySetting in registrySettings)
                {
                    try
                    {
                        var keyPath = registrySetting.KeyPath;
                        
                        // CRITICAL: Always get fresh values from registry (no caching for tooltips)
                        var currentValue = _registryService.GetValue(keyPath, registrySetting.ValueName);
                        var valueExists = _registryService.ValueExists(keyPath, registrySetting.ValueName);
                        var keyExists = _registryService.KeyExists(keyPath);

                        _logService.Log(LogLevel.Debug, 
                            $"Registry setting {registrySetting.ValueName}: KeyExists={keyExists}, ValueExists={valueExists}, Value={currentValue}");

                        // Add to individual values dictionary
                        individualValues[registrySetting] = currentValue;

                        // If this is the primary (first) setting, use its value for display
                        if (registrySetting == primaryRegistrySetting)
                        {
                            primaryDisplayValue = currentValue?.ToString() ?? "(not set)";
                        }
                    }
                    catch (Exception regEx)
                    {
                        _logService.Log(LogLevel.Warning, 
                            $"Error reading registry value for {registrySetting.KeyPath}\\{registrySetting.ValueName}: {regEx.Message}");
                        
                        // Still add to dictionary with null value to show in tooltip
                        individualValues[registrySetting] = null;
                    }
                }

                return new SettingTooltipData
                {
                    SettingId = setting.Id,
                    RegistrySetting = primaryRegistrySetting, // Primary setting for backward compatibility
                    DisplayValue = primaryDisplayValue,
                    IndividualRegistryValues = individualValues, // All registry settings and their values
                };
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting tooltip data for setting {setting.Id}: {ex.Message}");
                return null;
            }
        }
    }
}

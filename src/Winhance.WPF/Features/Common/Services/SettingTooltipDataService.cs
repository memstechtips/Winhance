using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Registry;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service for retrieving tooltip data for settings.
    /// This service operates in the WPF layer and coordinates with Core services
    /// to get individual registry values for tooltip display without polluting domain models.
    /// </summary>
    public class SettingTooltipDataService
    {
        private readonly ISystemSettingsDiscoveryService _systemSettingsDiscoveryService;
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;

        public SettingTooltipDataService(
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService,
            IRegistryService registryService,
            ILogService logService)
        {
            _systemSettingsDiscoveryService = systemSettingsDiscoveryService ?? throw new ArgumentNullException(nameof(systemSettingsDiscoveryService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Gets tooltip data for the specified settings.
        /// This method directly accesses the registry service to get fresh values for reactive updates.
        /// </summary>
        /// <param name="settings">The settings to get tooltip data for</param>
        /// <returns>Dictionary mapping setting ID to tooltip data</returns>
        public async Task<Dictionary<string, SettingTooltipData>> GetTooltipDataAsync(IEnumerable<ApplicationSetting> settings)
        {
            var results = new Dictionary<string, SettingTooltipData>();

            try
            {
                var settingsList = settings.ToList();
                
                // Create tooltip data for each setting with direct registry access
                foreach (var setting in settingsList)
                {
                    var tooltipData = new SettingTooltipData
                    {
                        SettingId = setting.Id,
                        CommandSettings = new List<CommandSetting>(setting.CommandSettings)
                    };
                
                    // Get fresh registry values directly from the registry service
                    if (setting.RegistrySettings != null && setting.RegistrySettings.Count > 0)
                    {
                        var registryValues = new Dictionary<RegistrySetting, object?>();
                    
                        foreach (var registrySetting in setting.RegistrySettings)
                        {
                            // Get the registry path
                            var keyPath = $"{RegistryExtensions.GetRegistryHiveString(registrySetting.Hive)}\\{registrySetting.SubKey}";
                        
                            // Get fresh value directly from registry service
                            var freshValue = _registryService.GetValue(keyPath, registrySetting.Name);
                            registryValues[registrySetting] = freshValue;
                        
                        }
                    
                        tooltipData.IndividualRegistryValues = registryValues;
                    }

                    results[setting.Id] = tooltipData;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error getting tooltip data: {ex.Message}");
            }

            return results;
        }
    }
}

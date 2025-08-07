using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service implementation for managing power management optimization settings.
    /// Handles power plans, sleep settings, and power-related optimizations.
    /// </summary>
    public class PowerService : IPowerService
    {
        private readonly IRegistryService _registryService;
        private readonly ICommandService _commandService;
        private readonly ILogService _logService;
        private readonly ISystemSettingsDiscoveryService _discoveryService;

        public string DomainName => "Power";

        public PowerService(
            IRegistryService registryService,
            ICommandService commandService,
            ILogService logService,
            ISystemSettingsDiscoveryService discoveryService)
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Power settings");
                
                var optimizations = PowerOptimizations.GetPowerOptimizations();
                return optimizations.Settings;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error loading Power settings: {ex.Message}"
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
                    $"Applying Power setting '{settingId}': enable={enable}"
                );

                var settings = await GetSettingsAsync();
                var setting = settings.FirstOrDefault(s => s.Id == settingId);
                
                if (setting == null)
                {
                    throw new ArgumentException(
                        $"Setting '{settingId}' not found in Power domain"
                    );
                }

                // Apply registry settings
                if (setting.RegistrySettings?.Count > 0)
                {
                    foreach (var registrySetting in setting.RegistrySettings)
                    {
                        await _registryService.ApplySettingAsync(registrySetting, enable);
                    }
                }

                // Apply command settings
                if (setting.CommandSettings?.Count > 0)
                {
                    foreach (var commandSetting in setting.CommandSettings)
                    {
                        await _commandService.ApplyCommandSettingsAsync(new[] { commandSetting }, enable);
                    }
                }

                _logService.Log(
                    LogLevel.Info,
                    $"Successfully applied Power setting '{settingId}'"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying Power setting '{settingId}': {ex.Message}"
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
                
                if (setting?.RegistrySettings?.Count > 0)
                {
                    var status = await _registryService.GetSettingStatusAsync(setting.RegistrySettings[0]);
                    return status == RegistrySettingStatus.Applied;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error checking Power setting '{settingId}': {ex.Message}"
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
                    $"Error getting Power setting value '{settingId}': {ex.Message}"
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
                if (setting?.RegistrySettings?.Count > 0)
                {
                    var status = await _registryService.GetSettingStatusAsync(setting.RegistrySettings[0]);
                    return status == RegistrySettingStatus.Applied;
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

        public async Task<PowerPlan?> GetActivePowerPlanAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Getting active power plan");
                
                // Use command service to get active power plan
                var result = await _commandService.ExecuteCommandAsync("powercfg /getactivescheme");
                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    // Extract GUID and name from output like "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)"
                    var guidMatch = System.Text.RegularExpressions.Regex.Match(result.Output, @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(result.Output, @"\((.+?)\)");
                    
                    if (guidMatch.Success)
                    {
                        return new PowerPlan
                        {
                            Guid = guidMatch.Value,
                            Name = nameMatch.Success ? nameMatch.Groups[1].Value : "Unknown",
                            IsActive = true
                        };
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error getting active power plan: {ex.Message}"
                );
                return null;
            }
        }

        public async Task ApplyAdvancedPowerSettingAsync(
            string powerPlanGuid,
            string subgroupGuid,
            string settingGuid,
            int acValue,
            int dcValue)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying advanced power setting: {settingGuid}"
                );

                // Apply AC value
                var acCommand = $"powercfg /setacvalueindex {powerPlanGuid} {subgroupGuid} {settingGuid} {acValue}";
                await _commandService.ExecuteCommandAsync(acCommand);

                // Apply DC value
                var dcCommand = $"powercfg /setdcvalueindex {powerPlanGuid} {subgroupGuid} {settingGuid} {dcValue}";
                await _commandService.ExecuteCommandAsync(dcCommand);

                // Apply changes
                var applyCommand = $"powercfg /setactive {powerPlanGuid}";
                await _commandService.ExecuteCommandAsync(applyCommand);

                _logService.Log(LogLevel.Info, "Advanced power setting applied successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying advanced power setting: {ex.Message}"
                );
                throw;
            }
        }

        public async Task<Dictionary<string, bool>> CheckPowerSystemCapabilitiesAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Checking power system capabilities");
                
                var capabilities = new Dictionary<string, bool>();
                
                // Check for battery
                var batteryCommand = "powercfg /batteryreport /output nul";
                try
                {
                    await _commandService.ExecuteCommandAsync(batteryCommand);
                    capabilities["HasBattery"] = true;
                }
                catch
                {
                    capabilities["HasBattery"] = false;
                }

                // Check for lid detection (assume true for laptops with battery)
                capabilities["HasLidDetection"] = capabilities["HasBattery"];

                // Check for hibernate support
                var hibernateCommand = "powercfg /availablesleepstates";
                try
                {
                    var result = await _commandService.ExecuteCommandAsync(hibernateCommand);
                    capabilities["SupportsHibernate"] = result.Success && result.Output?.Contains("Hibernate") == true;
                }
                catch
                {
                    capabilities["SupportsHibernate"] = false;
                }

                return capabilities;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error checking power system capabilities: {ex.Message}"
                );
                return new Dictionary<string, bool>();
            }
        }

        public async Task<IEnumerable<object>> GetAvailablePowerPlansAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Getting available power plans");
                
                var result = await _commandService.ExecuteCommandAsync("powercfg /list");
                var powerPlans = new List<object>();
                
                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Power Scheme GUID:"))
                        {
                            var guidMatch = System.Text.RegularExpressions.Regex.Match(line, @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
                            var nameMatch = System.Text.RegularExpressions.Regex.Match(line, @"\((.+?)\)");
                            
                            if (guidMatch.Success && nameMatch.Success)
                            {
                                powerPlans.Add(new PowerPlan
                                {
                                    Guid = guidMatch.Value,
                                    Name = nameMatch.Groups[1].Value,
                                    IsActive = line.Contains("*")
                                });
                            }
                        }
                    }
                }
                
                return powerPlans;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting available power plans: {ex.Message}");
                return Enumerable.Empty<object>();
            }
        }

        public async Task<IEnumerable<AdvancedPowerSettingGroup>> GetAdvancedPowerSettingGroupsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Getting advanced power setting groups");
                
                // Get active power plan first
                var activePlan = await GetActivePowerPlanAsync();
                if (activePlan == null)
                {
                    return Enumerable.Empty<AdvancedPowerSettingGroup>();
                }
                
                var planGuid = activePlan.Guid;
                var result = await _commandService.ExecuteCommandAsync($"powercfg /query {planGuid}");
                var settingGroups = new List<AdvancedPowerSettingGroup>();
                
                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    // Parse the output to extract subgroups and settings
                    // This is a simplified implementation - in a real scenario you'd want more robust parsing
                    var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var line in lines)
                    {
                        if (line.Contains("Subgroup GUID:"))
                        {
                            var guidMatch = System.Text.RegularExpressions.Regex.Match(line, @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
                            var nameMatch = System.Text.RegularExpressions.Regex.Match(line, @"\((.+?)\)");
                            
                            if (guidMatch.Success && nameMatch.Success)
                            {
                                var subgroup = new PowerSettingSubgroup
                                {
                                    Guid = guidMatch.Value,
                                    DisplayName = nameMatch.Groups[1].Value,
                                    Alias = nameMatch.Groups[1].Value
                                };
                                
                                var advancedGroup = new AdvancedPowerSettingGroup
                                {
                                    Subgroup = subgroup
                                };
                                
                                settingGroups.Add(advancedGroup);
                            }
                        }
                    }
                }
                
                return settingGroups;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting advanced power setting groups: {ex.Message}");
                return Enumerable.Empty<AdvancedPowerSettingGroup>();
            }
        }
    }
}

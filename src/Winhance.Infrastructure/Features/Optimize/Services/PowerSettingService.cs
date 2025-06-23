using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service for managing power settings.
    /// </summary>
    public class PowerSettingService : IPowerSettingService
    {
        private readonly ICommandService _commandService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerSettingService"/> class.
        /// </summary>
        /// <param name="commandService">The command service.</param>
        public PowerSettingService(ICommandService commandService)
        {
            _commandService = commandService;
        }

        /// <summary>
        /// Gets all available power settings.
        /// </summary>
        /// <returns>A list of all power settings.</returns>
        public List<PowerSettingDefinition> GetAllSettings()
        {
            return PowerSettingCatalog.GetAllSettings();
        }

        /// <summary>
        /// Gets all power setting subgroups.
        /// </summary>
        /// <returns>A list of all power setting subgroups.</returns>
        public List<PowerSettingSubgroup> GetAllSubgroups()
        {
            return PowerSettingCatalog.GetAllSubgroups();
        }

        /// <summary>
        /// Gets a power setting by its GUID.
        /// </summary>
        /// <param name="guid">The GUID of the power setting.</param>
        /// <returns>The power setting definition, or null if not found.</returns>
        public PowerSettingDefinition? GetSettingByGuid(string guid)
        {
            return PowerSettingCatalog.GetSettingByGuid(guid);
        }

        /// <summary>
        /// Gets a power setting by its alias.
        /// </summary>
        /// <param name="alias">The alias of the power setting.</param>
        /// <returns>The power setting definition, or null if not found.</returns>
        public PowerSettingDefinition? GetSettingByAlias(string alias)
        {
            return PowerSettingCatalog.GetSettingByAlias(alias);
        }

        /// <summary>
        /// Gets the current value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <returns>A tuple containing the AC and DC values.</returns>
        public async Task<(int acValue, int dcValue)> GetSettingValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid)
        {
            // Check if this is a custom command setting
            var settingDefinition = PowerSettingCatalog.GetSettingByGuid(settingGuid);
            if (settingDefinition != null && settingDefinition.CustomCommand)
            {
                // For custom commands like hibernation, we need to determine the current state
                // For hibernation specifically, we can check if hibernation is enabled
                if (settingDefinition.Alias == "hibernation")
                {
                    // Run powercfg /a to check available sleep states
                    var result = await _commandService.ExecuteCommandAsync("powercfg /a");
                    
                    // If hibernation is available, the output will contain "Hibernation"
                    bool hibernationEnabled = result.Output.Contains("Hibernation", StringComparison.OrdinalIgnoreCase) &&
                                           !result.Output.Contains("Hibernation is not available", StringComparison.OrdinalIgnoreCase);
                    
                    // Return 1 for enabled, 0 for disabled (for both AC and DC since this is a system-wide setting)
                    return (hibernationEnabled ? 1 : 0, hibernationEnabled ? 1 : 0);
                }
                
                // For other custom commands, default to 0
                return (0, 0);
            }
            
            // Standard power setting
            int acValue = await GetAcValueAsync(powerPlanGuid, subgroupGuid, settingGuid);
            int dcValue = await GetDcValueAsync(powerPlanGuid, subgroupGuid, settingGuid);
            return (acValue, dcValue);
        }

        /// <summary>
        /// Gets the current AC value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <returns>The AC value.</returns>
        public async Task<int> GetAcValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid)
        {
            string command = $"powercfg /query {powerPlanGuid} {subgroupGuid} {settingGuid}";
            var result = await _commandService.ExecuteCommandAsync(command);

            return ParsePowerSettingValue(result.Output, "Current AC Power Setting Index:");
        }

        /// <summary>
        /// Gets the current DC value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <returns>The DC value.</returns>
        public async Task<int> GetDcValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid)
        {
            string command = $"powercfg /query {powerPlanGuid} {subgroupGuid} {settingGuid}";
            var result = await _commandService.ExecuteCommandAsync(command);

            return ParsePowerSettingValue(result.Output, "Current DC Power Setting Index:");
        }

        /// <summary>
        /// Sets the AC value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> SetAcValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid, int value)
        {
            string command = $"powercfg /setacvalueindex {powerPlanGuid} {subgroupGuid} {settingGuid} {value}";
            var result = await _commandService.ExecuteCommandAsync(command);

            // Apply changes to the active power plan
            await _commandService.ExecuteCommandAsync($"powercfg /setactive {powerPlanGuid}");

            return result.Success && !result.Output.Contains("Error");
        }

        /// <summary>
        /// Sets the DC value of a power setting for a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> SetDcValueAsync(string powerPlanGuid, string subgroupGuid, string settingGuid, int value)
        {
            string command = $"powercfg /setdcvalueindex {powerPlanGuid} {subgroupGuid} {settingGuid} {value}";
            var result = await _commandService.ExecuteCommandAsync(command);

            // Apply changes to the active power plan
            var activateResult = await _commandService.ExecuteCommandAsync($"powercfg /setactive {powerPlanGuid}");

            return result.Success && !result.Output.Contains("Error");
        }

        /// <summary>
        /// Applies a power setting value to a power plan.
        /// </summary>
        /// <param name="settingValue">The power setting value to apply.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> ApplySettingValueAsync(PowerSettingApplyValue settingValue)
        {
            // Get the setting definition to check if it's a custom command
            var settingDefinition = PowerSettingCatalog.GetSettingByGuid(settingValue.SettingGuid);
            
            // If this is a custom command setting, handle it differently
            if (settingDefinition != null && settingDefinition.CustomCommand)
            {
                // For custom commands, we typically only need to use one value (AC or DC)
                // We'll prioritize AC value if both are set
                int valueToUse = settingValue.ApplyAcValue ? settingValue.AcValue : settingValue.DcValue;
                
                // Check if we have a mapping for this value
                if (settingDefinition.CustomCommandValueMap.TryGetValue(valueToUse, out string commandArg))
                {
                    // Format the command using the template and the mapped argument
                    string command = string.Format(settingDefinition.CustomCommandTemplate, commandArg);
                    
                    // Execute the custom command
                    var result = await _commandService.ExecuteCommandAsync(command);
                    return result.Success && !result.Output.Contains("Error");
                }
                
                return false;
            }
            
            // Standard power setting handling
            bool success = true;

            if (settingValue.ApplyAcValue)
            {
                success &= await SetAcValueAsync(
                    settingValue.PowerPlanGuid,
                    settingValue.SubgroupGuid,
                    settingValue.SettingGuid,
                    settingValue.AcValue);
            }

            if (settingValue.ApplyDcValue)
            {
                success &= await SetDcValueAsync(
                    settingValue.PowerPlanGuid,
                    settingValue.SubgroupGuid,
                    settingValue.SettingGuid,
                    settingValue.DcValue);
            }

            return success;
        }

        /// <summary>
        /// Parses a power setting value from the powercfg output.
        /// </summary>
        /// <param name="output">The powercfg output.</param>
        /// <param name="marker">The marker to search for.</param>
        /// <returns>The parsed value, or 0 if not found.</returns>
        private int ParsePowerSettingValue(string output, string marker)
        {
            if (string.IsNullOrEmpty(output))
                return 0;

            int index = output.IndexOf(marker);
            if (index == -1)
                return 0;

            string valueString = output.Substring(index + marker.Length).Trim();
            int endIndex = valueString.IndexOf('\r');
            if (endIndex != -1)
                valueString = valueString.Substring(0, endIndex).Trim();

            if (int.TryParse(valueString, out int value))
                return value;

            // Try to parse as hex (0x format)
            if (valueString.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(valueString.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int hexValue))
                return hexValue;

            return 0;
        }

        /// <summary>
        /// Checks if a power setting exists on the system.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID.</param>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <param name="settingGuid">The setting GUID (optional). If null, only checks if the subgroup exists.</param>
        /// <returns>True if the setting exists, false otherwise.</returns>
        public async Task<bool> DoesSettingExistAsync(string powerPlanGuid, string subgroupGuid, string? settingGuid = null)
        {
            try
            {
                // Check if this is a custom command setting
                if (!string.IsNullOrEmpty(settingGuid))
                {
                    var settingDefinition = PowerSettingCatalog.GetSettingByGuid(settingGuid);
                    if (settingDefinition != null && settingDefinition.CustomCommand)
                    {
                        // For custom commands like hibernation, we consider them to exist if they're defined in our catalog
                        // We could also check if the command is executable, but that's more complex and may require elevation
                        return true;
                    }
                }
                
                // Standard powercfg query approach for regular settings
                string command;
                
                if (string.IsNullOrEmpty(settingGuid))
                {
                    // Only check if the subgroup exists
                    command = $"powercfg /query {powerPlanGuid} {subgroupGuid}";
                }
                else
                {
                    // Check if both subgroup and setting exist
                    command = $"powercfg /query {powerPlanGuid} {subgroupGuid} {settingGuid}";
                }

                var result = await _commandService.ExecuteCommandAsync(command);

                // If the command was successful and the output doesn't contain error messages,
                // the setting exists
                return result.Success && 
                       !result.Output.Contains("Error", StringComparison.OrdinalIgnoreCase) &&
                       !result.Output.Contains("not valid", StringComparison.OrdinalIgnoreCase) &&
                       !result.Output.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // If an exception occurs, the setting doesn't exist or can't be accessed
                return false;
            }
        }
    }
}

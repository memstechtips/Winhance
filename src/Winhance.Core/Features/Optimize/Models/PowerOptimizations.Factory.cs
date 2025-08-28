using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Factory methods for creating and converting power optimization settings.
    /// </summary>
    public static partial class PowerOptimizations
    {
        /// <summary>
        /// Gets all power settings from all subgroups.
        /// </summary>
        public static List<PowerSettingDefinition> GetAllSettings()
        {
            return GetAllSubgroups().SelectMany(s => s.Settings).ToList();
        }

        /// <summary>
        /// Gets a power setting by its GUID.
        /// </summary>
        public static PowerSettingDefinition? GetSettingByGuid(string guid)
        {
            return GetAllSettings()
                .FirstOrDefault(s => s.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets a power setting by its alias.
        /// </summary>
        public static PowerSettingDefinition? GetSettingByAlias(string alias)
        {
            return GetAllSettings()
                .FirstOrDefault(s => s.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets all power optimizations as an SettingGroup.
        /// </summary>
        /// <returns>An SettingGroup containing all power settings converted to optimization settings.</returns>
        public static SettingGroup GetPowerOptimizations()
        {
            var settings = new List<SettingDefinition>();
            var powerSettingDefinitions = GetAllSettings();

            // Convert each PowerSettingDefinition to SettingDefinition
            foreach (var definition in powerSettingDefinitions)
            {
                var SettingDefinition = ConvertToSettingDefinition(definition);
                if (SettingDefinition != null)
                {
                    settings.Add(SettingDefinition);
                }
            }

            return new SettingGroup
            {
                Name = "Power",
                FeatureId = FeatureIds.Power,
                Settings = settings,
            };
        }

        /// <summary>
        /// Converts a PowerSettingDefinition to an SettingDefinition.
        /// </summary>
        /// <param name="definition">The power setting definition to convert.</param>
        /// <returns>The converted optimization setting, or null if conversion is not supported.</returns>
        public static SettingDefinition? ConvertToSettingDefinition(
            PowerSettingDefinition definition
        )
        {
            // Use the InputType directly from the definition (no fallback needed since we removed SettingType)
            var inputType = definition.InputType;

            var setting = new SettingDefinition
            {
                Id = definition.Guid, // Use actual GUID as ID for proper mapping
                Name = definition.DisplayName,
                Description = definition.Description,
                GroupName = GetSubgroupDisplayName(definition.SubgroupGuid),
                InputType = inputType,
                CommandSettings = CreatePowerCfgCommands(definition),
                // Copy power-specific properties to CustomProperties for UI use
                CustomProperties = CreateCustomProperties(definition),
            };

            return setting;
        }

        /// <summary>
        /// Creates custom properties for power settings that need special handling in the UI.
        /// </summary>
        private static Dictionary<string, object> CreateCustomProperties(
            PowerSettingDefinition definition
        )
        {
            var properties = new Dictionary<string, object>();

            // Add ComboBox options for enum-like settings
            if (definition.PossibleValues?.Count > 0)
            {
                var options = definition
                    .PossibleValues.Select(v => new PowerSettingOption
                    {
                        Name = v.FriendlyName,
                        Value = v.Index,
                    })
                    .ToList();
                properties["Options"] = options;
            }

            // Add time intervals for time-based settings
            if (definition.UseTimeIntervals && definition.TimeValues?.Count > 0)
            {
                var options = definition
                    .TimeValues.Select(t => new PowerSettingOption
                    {
                        Name = t.DisplayName,
                        Value = t.Minutes,
                    })
                    .ToList();
                properties["Options"] = options;
            }

            // Add min/max/increment for numeric settings
            if (definition.InputType == SettingInputType.NumericRange)
            {
                properties["MinValue"] = definition.MinValue;
                properties["MaxValue"] = definition.MaxValue;
                properties["Increment"] = definition.Increment;
                properties["Units"] = definition.Units;
            }

            // Add power setting metadata
            properties["SettingGuid"] = definition.Guid;
            properties["SubgroupGuid"] = definition.SubgroupGuid;
            properties["Alias"] = definition.Alias;

            return properties;
        }

        /// <summary>
        /// Gets the display name for a subgroup by its GUID.
        /// </summary>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <returns>The display name of the subgroup.</returns>
        private static string GetSubgroupDisplayName(string subgroupGuid)
        {
            var subgroup = GetAllSubgroups()
                .FirstOrDefault(s =>
                    s.Guid.Equals(subgroupGuid, StringComparison.OrdinalIgnoreCase)
                );
            return subgroup?.DisplayName ?? "Power Settings";
        }

        /// <summary>
        /// Creates powercfg command settings for a power setting definition.
        /// </summary>
        /// <param name="definition">The power setting definition.</param>
        /// <returns>A list of command settings.</returns>
        private static List<CommandSetting> CreatePowerCfgCommands(
            PowerSettingDefinition definition
        )
        {
            var commands = new List<CommandSetting>();

            // Handle custom commands (like hibernation)
            if (definition.CustomCommand && !string.IsNullOrEmpty(definition.CustomCommandTemplate))
            {
                commands.Add(
                    new CommandSetting
                    {
                        Id = $"power-{definition.Alias}-custom",
                        Category = "Power",
                        EnabledCommand =
                            $"powercfg {definition.CustomCommandTemplate.Replace("{0}", "on")}",
                        DisabledCommand =
                            $"powercfg {definition.CustomCommandTemplate.Replace("{0}", "off")}",
                        Description = $"Toggle {definition.DisplayName}",
                    }
                );
            }
            else
            {
                // Standard powercfg commands for AC and DC power
                commands.Add(
                    new CommandSetting
                    {
                        Id = $"power-{definition.Alias}-ac",
                        Category = "Power",
                        EnabledCommand =
                            $"powercfg /setacvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 1",
                        DisabledCommand =
                            $"powercfg /setacvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 0",
                        Description = $"Set {definition.DisplayName} on AC power",
                    }
                );

                commands.Add(
                    new CommandSetting
                    {
                        Id = $"power-{definition.Alias}-dc",
                        Category = "Power",
                        EnabledCommand =
                            $"powercfg /setdcvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 1",
                        DisabledCommand =
                            $"powercfg /setdcvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 0",
                        Description = $"Set {definition.DisplayName} on battery power",
                    }
                );

                commands.Add(
                    new CommandSetting
                    {
                        Id = $"power-{definition.Alias}-apply",
                        Category = "Power",
                        EnabledCommand = "powercfg /setactive SCHEME_CURRENT",
                        DisabledCommand = "powercfg /setactive SCHEME_CURRENT",
                        Description = "Apply power plan changes",
                    }
                );
            }

            return commands;
        }

        /// <summary>
        /// Creates an AdvancedPowerSetting from a PowerSettingDefinition with current system values.
        /// </summary>
        /// <param name="definition">The power setting definition.</param>
        /// <param name="acValue">The current AC value from the system.</param>
        /// <param name="dcValue">The current DC value from the system.</param>
        /// <returns>An AdvancedPowerSetting ready for UI binding.</returns>
        public static AdvancedPowerSetting CreateAdvancedPowerSetting(
            PowerSettingDefinition definition,
            int acValue = 0,
            int dcValue = 0
        )
        {
            return new AdvancedPowerSetting
            {
                Definition = definition,
                AcValue = acValue,
                DcValue = dcValue,
            };
        }

        /// <summary>
        /// Creates an AdvancedPowerSettingGroup from a PowerSettingSubgroup with current system values.
        /// </summary>
        /// <param name="subgroup">The power setting subgroup.</param>
        /// <param name="systemValues">Dictionary of setting GUID to (AC, DC) values from the system.</param>
        /// <returns>An AdvancedPowerSettingGroup ready for UI binding.</returns>
        public static AdvancedPowerSettingGroup CreateAdvancedPowerSettingGroup(
            PowerSettingSubgroup subgroup,
            Dictionary<string, (int ac, int dc)> systemValues = null
        )
        {
            var group = new AdvancedPowerSettingGroup { Subgroup = subgroup };

            foreach (var settingDef in subgroup.Settings)
            {
                var acValue = 0;
                var dcValue = 0;

                if (systemValues?.ContainsKey(settingDef.Guid) == true)
                {
                    (acValue, dcValue) = systemValues[settingDef.Guid];
                }

                var advancedSetting = CreateAdvancedPowerSetting(settingDef, acValue, dcValue);
                group.Settings.Add(advancedSetting);
            }

            return group;
        }
    }
}

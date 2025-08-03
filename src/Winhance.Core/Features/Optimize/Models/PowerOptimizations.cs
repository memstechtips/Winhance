using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Represents a PowerCfg command setting.
    /// </summary>
    public class PowerCfgSetting
    {
        /// <summary>
        /// Gets or sets the command to execute.
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the command.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value to use when the setting is enabled.
        /// </summary>
        public string EnabledValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value to use when the setting is disabled.
        /// </summary>
        public string DisabledValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Provides access to all available power plans.
    /// </summary>
    public static class PowerPlans
    {
        /// <summary>
        /// The Balanced power plan.
        /// </summary>
        public static readonly PowerPlan Balanced = new PowerPlan
        {
            Name = "Balanced",
            Guid = "381b4222-f694-41f0-9685-ff5bb260df2e",
            Description =
                "Automatically balances performance with energy consumption on capable hardware.",
        };

        /// <summary>
        /// The High Performance power plan.
        /// </summary>
        public static readonly PowerPlan HighPerformance = new PowerPlan
        {
            Name = "High Performance",
            Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
            Description = "Favors performance, but may use more energy.",
        };

        /// <summary>
        /// The Ultimate Performance power plan.
        /// </summary>
        public static readonly PowerPlan UltimatePerformance = new PowerPlan
        {
            Name = "Ultimate Performance",
            // This GUID is a placeholder and will be updated at runtime by PowerPlanService
            Guid = "e9a42b02-d5df-448d-aa00-03f14749eb61",
            Description = "Provides ultimate performance on Windows.",
        };

        /// <summary>
        /// Gets a list of all available power plans.
        /// </summary>
        /// <returns>A list of all power plans.</returns>
        public static List<PowerPlan> GetAllPowerPlans()
        {
            return new List<PowerPlan> { Balanced, HighPerformance, UltimatePerformance };
        }
    }

    /// <summary>
    /// Provides power optimization settings and power setting catalog definitions.
    /// </summary>
    public static class PowerOptimizations
    {
        /// <summary>
        /// Gets all power setting subgroups with their settings.
        /// </summary>
        public static List<PowerSettingSubgroup> GetAllSubgroups()
        {
            var subgroups = new List<PowerSettingSubgroup>
            {
                GetHardDiskSubgroup(),
                GetSleepSubgroup(),
                GetPowerButtonsSubgroup(),
                GetDisplaySubgroup(),
                GetProcessorPowerManagementSubgroup(),
                GetBatterySubgroup(),
                GetDesktopBackgroundSettingsSubgroup(),
                GetWirelessAdapterSettingsSubgroup(),
                GetUsbSettingsSubgroup(),
                GetPciExpressSubgroup(),
                GetMultimediaSettingsSubgroup(),
                GetAmdPowerSliderSubgroup(),
                GetInternetExplorerSubgroup(),
                GetIntelGraphicsPowerSubgroup(),
                GetAtiPowerPlaySubgroup(),
                GetSwitchableGraphicsSubgroup(),
            };

            return subgroups;
        }

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
        /// Gets all power optimizations as an OptimizationGroup.
        /// </summary>
        /// <returns>An OptimizationGroup containing all power settings converted to optimization settings.</returns>
        public static OptimizationGroup GetPowerOptimizations()
        {
            var settings = new List<OptimizationSetting>();
            var powerSettingDefinitions = GetAllSettings();

            // Convert each PowerSettingDefinition to OptimizationSetting
            foreach (var definition in powerSettingDefinitions)
            {
                var optimizationSetting = ConvertToOptimizationSetting(definition);
                if (optimizationSetting != null)
                {
                    settings.Add(optimizationSetting);
                }
            }

            return new OptimizationGroup
            {
                Name = "Power",
                Category = OptimizationCategory.Power,
                Settings = settings
            };
        }

        /// <summary>
        /// Converts a PowerSettingDefinition to an OptimizationSetting.
        /// </summary>
        /// <param name="definition">The power setting definition to convert.</param>
        /// <returns>The converted optimization setting, or null if conversion is not supported.</returns>
        private static OptimizationSetting? ConvertToOptimizationSetting(PowerSettingDefinition definition)
        {
            // Determine control type based on setting type
            var controlType = definition.SettingType switch
            {
                PowerSettingType.Enum => ControlType.ComboBox,
                PowerSettingType.Numeric when definition.UseTimeIntervals && definition.TimeValues != null => ControlType.ComboBox,
                _ => ControlType.BinaryToggle
            };

            var setting = new OptimizationSetting
            {
                Id = $"power-{definition.Alias}",
                Name = definition.DisplayName,
                Description = definition.Description,
                Category = OptimizationCategory.Power,
                GroupName = GetSubgroupDisplayName(definition.SubgroupGuid),
                ControlType = controlType,
                CommandSettings = CreatePowerCfgCommands(definition)
            };

            return setting;
        }

        /// <summary>
        /// Gets the display name for a subgroup by its GUID.
        /// </summary>
        /// <param name="subgroupGuid">The subgroup GUID.</param>
        /// <returns>The display name of the subgroup.</returns>
        private static string GetSubgroupDisplayName(string subgroupGuid)
        {
            var subgroup = GetAllSubgroups().FirstOrDefault(s => s.Guid.Equals(subgroupGuid, StringComparison.OrdinalIgnoreCase));
            return subgroup?.DisplayName ?? "Power Settings";
        }

        /// <summary>
        /// Creates powercfg command settings for a power setting definition.
        /// </summary>
        /// <param name="definition">The power setting definition.</param>
        /// <returns>A list of command settings.</returns>
        private static List<CommandSetting> CreatePowerCfgCommands(PowerSettingDefinition definition)
        {
            var commands = new List<CommandSetting>();

            // Handle custom commands (like hibernation)
            if (definition.CustomCommand && !string.IsNullOrEmpty(definition.CustomCommandTemplate))
            {
                commands.Add(new CommandSetting
                {
                    Id = $"power-{definition.Alias}-custom",
                    Category = "Power",
                    EnabledCommand = $"powercfg {definition.CustomCommandTemplate.Replace("{0}", "on")}",
                    DisabledCommand = $"powercfg {definition.CustomCommandTemplate.Replace("{0}", "off")}",
                    Description = $"Toggle {definition.DisplayName}"
                });
            }
            else
            {
                // Standard powercfg commands for AC and DC power
                commands.Add(new CommandSetting
                {
                    Id = $"power-{definition.Alias}-ac",
                    Category = "Power",
                    EnabledCommand = $"powercfg /setacvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 1",
                    DisabledCommand = $"powercfg /setacvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 0",
                    Description = $"Set {definition.DisplayName} on AC power"
                });

                commands.Add(new CommandSetting
                {
                    Id = $"power-{definition.Alias}-dc",
                    Category = "Power",
                    EnabledCommand = $"powercfg /setdcvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 1",
                    DisabledCommand = $"powercfg /setdcvalueindex SCHEME_CURRENT {definition.SubgroupGuid} {definition.Guid} 0",
                    Description = $"Set {definition.DisplayName} on battery power"
                });

                commands.Add(new CommandSetting
                {
                    Id = $"power-{definition.Alias}-apply",
                    Category = "Power",
                    EnabledCommand = "powercfg /setactive SCHEME_CURRENT",
                    DisabledCommand = "powercfg /setactive SCHEME_CURRENT",
                    Description = "Apply power plan changes"
                });
            }

            return commands;
        }

        /// <summary>
        /// Gets the Hard Disk subgroup with its settings.
        /// </summary>
        private static PowerSettingSubgroup GetHardDiskSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "0012ee47-9041-4b5d-9b77-535fba8b1442",
                Alias = "disk",
                DisplayName = "Hard Disk",
            };

            // Turn off hard disk after
            var turnOffHardDiskAfter = new PowerSettingDefinition
            {
                Guid = "6738e2c4-e8a5-4a42-b16a-e040e769756e",
                Alias = "diskidle",
                DisplayName = "Turn off hard disk after",
                Description =
                    "Specifies the period of inactivity before Windows turns off the hard disk.",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 1200,
                Increment = 1,
                Units = "Minutes",
                UseTimeIntervals = true,
                TimeValues = GetStandardTimeIntervals(),
            };

            subgroup.Settings.Add(turnOffHardDiskAfter);
            return subgroup;
        }

        /// <summary>
        /// Gets the Sleep subgroup with its settings.
        /// </summary>
        private static PowerSettingSubgroup GetSleepSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                Alias = "sleep",
                DisplayName = "Sleep",
            };

            // Put the computer to sleep
            var sleepAfter = new PowerSettingDefinition
            {
                Guid = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da",
                Alias = "standbyidle",
                DisplayName = "Put the computer to sleep",
                Description =
                    "Specifies the period of inactivity before Windows puts the computer to sleep.",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 1200,
                Increment = 1,
                Units = "Minutes",
                UseTimeIntervals = true,
                TimeValues = GetStandardTimeIntervals(),
            };

            // Hibernate after
            var hibernateAfter = new PowerSettingDefinition
            {
                Guid = "9d7815a6-7ee4-497e-8888-515a05f02364",
                Alias = "hibernateidle",
                DisplayName = "Hibernate after",
                Description =
                    "Specifies the period of inactivity before Windows hibernates the computer.",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 1200,
                Increment = 1,
                Units = "Minutes",
                UseTimeIntervals = true,
                TimeValues = GetStandardTimeIntervals(),
            };

            // Allow hybrid sleep
            var allowHybridSleep = new PowerSettingDefinition
            {
                Guid = "94ac6d29-73ce-41a6-809f-6363ba21b47e",
                Alias = "hybridsleep",
                DisplayName = "Allow hybrid sleep",
                Description = "Enables or disables hybrid sleep mode.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Off" },
                    new PowerSettingValue { Index = 1, FriendlyName = "On" },
                },
            };

            // Allow wake timers
            var allowWakeTimers = new PowerSettingDefinition
            {
                Guid = "bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d",
                Alias = "rtcwake",
                DisplayName = "Allow wake timers",
                Description = "Determines whether Windows allows or ignores wake timer events.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Disable" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Enable" },
                    new PowerSettingValue
                    {
                        Index = 2,
                        FriendlyName = "Important Wake Timers Only",
                    },
                },
            };

            // Enable/Disable Hibernation
            var enableHibernation = new PowerSettingDefinition
            {
                Guid = "9d82f7ff-0cf2-4fe3-9c7c-4d3320f8d9f1", // Custom GUID
                Alias = "hibernation",
                DisplayName = "Enable Hibernation",
                Description =
                    "Enables or disables system hibernation. Note: Disabling hibernation also disables Fast Startup.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Off" },
                    new PowerSettingValue { Index = 1, FriendlyName = "On" },
                },
                CustomCommand = true, // Indicates this setting uses a custom command
                CustomCommandTemplate = "powercfg /hibernate {0}", // Template for the command
                CustomCommandValueMap = new Dictionary<int, string> // Maps index values to command arguments
                {
                    { 0, "off" },
                    { 1, "on" },
                },
            };

            subgroup.Settings.Add(sleepAfter);
            subgroup.Settings.Add(hibernateAfter);
            subgroup.Settings.Add(allowHybridSleep);
            subgroup.Settings.Add(allowWakeTimers);
            subgroup.Settings.Add(enableHibernation);
            return subgroup;
        }

        /// <summary>
        /// Gets the Power Buttons subgroup with its settings.
        /// </summary>
        private static PowerSettingSubgroup GetPowerButtonsSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                Alias = "buttons",
                DisplayName = "Power Buttons and Lid",
            };

            // Power button action
            var powerButtonAction = new PowerSettingDefinition
            {
                Guid = "7648efa3-dd9c-4e3e-b566-50f929386280",
                Alias = "powerbutton",
                DisplayName = "Power button action",
                Description =
                    "Specifies the action that Windows takes when the user presses the power button.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Do nothing" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Sleep" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Hibernate" },
                    new PowerSettingValue { Index = 3, FriendlyName = "Shut down" },
                    new PowerSettingValue { Index = 4, FriendlyName = "Turn off the display" },
                },
            };

            // Sleep button action
            var sleepButtonAction = new PowerSettingDefinition
            {
                Guid = "96996bc0-ad50-47ec-923b-6f41874dd9eb",
                Alias = "sleepbutton",
                DisplayName = "Sleep button action",
                Description =
                    "Specifies the action that Windows takes when the user presses the sleep button.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Do nothing" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Sleep" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Hibernate" },
                    new PowerSettingValue { Index = 3, FriendlyName = "Shut down" },
                    new PowerSettingValue { Index = 4, FriendlyName = "Turn off the display" },
                },
            };

            // Lid close action
            var lidCloseAction = new PowerSettingDefinition
            {
                Guid = "5ca83367-6e45-459f-a27b-476b1d01c936",
                Alias = "lidaction",
                DisplayName = "Lid close action",
                Description =
                    "Specifies the action that Windows takes when the user closes the lid on a laptop.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Do nothing" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Sleep" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Hibernate" },
                    new PowerSettingValue { Index = 3, FriendlyName = "Shut down" },
                },
            };

            subgroup.Settings.Add(powerButtonAction);
            subgroup.Settings.Add(sleepButtonAction);
            subgroup.Settings.Add(lidCloseAction);
            return subgroup;
        }

        /// <summary>
        /// Gets the Display subgroup with its settings.
        /// </summary>
        private static PowerSettingSubgroup GetDisplaySubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "7516b95f-f776-4464-8c53-06167f40cc99",
                Alias = "display",
                DisplayName = "Display",
            };

            // Turn off the display
            var turnOffDisplayAfter = new PowerSettingDefinition
            {
                Guid = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e",
                Alias = "monitor-timeout",
                DisplayName = "Turn off the display",
                Description =
                    "Specifies the period of inactivity before Windows turns off the display.",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 1200,
                Increment = 1,
                Units = "Minutes",
                UseTimeIntervals = true,
                TimeValues = GetStandardTimeIntervals(),
            };

            // Display brightness
            var displayBrightness = new PowerSettingDefinition
            {
                Guid = "aded5e82-b909-4619-9949-f5d71dac0bcb",
                Alias = "brightness",
                DisplayName = "Display brightness",
                Description = "Specifies the brightness level of the display.",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 100,
                Increment = 1,
                Units = "%",
            };

            // Dimmed display brightness
            var dimmedDisplayBrightness = new PowerSettingDefinition
            {
                Guid = "f1fbfde2-a960-4165-9f88-50667911ce96",
                Alias = "dimbrightness",
                DisplayName = "Dimmed display brightness",
                Description = "Specifies the brightness level when the display is dimmed.",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 100,
                Increment = 1,
                Units = "%",
            };

            // Enable adaptive brightness
            var enableAdaptiveBrightness = new PowerSettingDefinition
            {
                Guid = "fbd9aa66-9553-4097-ba44-ed6e9d65eab8",
                Alias = "adaptbright",
                DisplayName = "Enable adaptive brightness",
                Description =
                    "Specifies whether Windows automatically adjusts display brightness based on ambient light.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Off" },
                    new PowerSettingValue { Index = 1, FriendlyName = "On" },
                },
            };

            subgroup.Settings.Add(turnOffDisplayAfter);
            subgroup.Settings.Add(displayBrightness);
            subgroup.Settings.Add(dimmedDisplayBrightness);
            subgroup.Settings.Add(enableAdaptiveBrightness);
            return subgroup;
        }

        /// <summary>
        /// Gets the Processor Power Management subgroup with its settings.
        /// </summary>
        private static PowerSettingSubgroup GetProcessorPowerManagementSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "54533251-82be-4824-96c1-47b60b740d00",
                Alias = "processor",
                DisplayName = "Processor Power Management",
            };

            // Minimum processor state
            var minProcessorState = new PowerSettingDefinition
            {
                Guid = "893dee8e-2bef-41e0-89c6-b55d0929964c",
                Alias = "procthrottlemin",
                DisplayName = "Minimum processor state",
                Description =
                    "Specifies the minimum processor performance state (as a percentage).",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 100,
                Increment = 1,
                Units = "%",
            };

            // Maximum processor state
            var maxProcessorState = new PowerSettingDefinition
            {
                Guid = "bc5038f7-23e0-4960-96da-33abaf5935ec",
                Alias = "procthrottlemax",
                DisplayName = "Maximum processor state",
                Description =
                    "Specifies the maximum processor performance state (as a percentage).",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 100,
                Increment = 1,
                Units = "%",
            };

            // System cooling policy
            var systemCoolingPolicy = new PowerSettingDefinition
            {
                Guid = "94d3a615-a899-4ac5-ae2b-e4d8f634367f",
                Alias = "syscoolpol",
                DisplayName = "System cooling policy",
                Description =
                    "Specifies the cooling mode that Windows uses for the current power plan.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Passive" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Active" },
                },
            };

            subgroup.Settings.Add(minProcessorState);
            subgroup.Settings.Add(maxProcessorState);
            subgroup.Settings.Add(systemCoolingPolicy);
            return subgroup;
        }

        /// <summary>
        /// Gets the Battery subgroup with its settings.
        /// </summary>
        private static PowerSettingSubgroup GetBatterySubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                Alias = "battery",
                DisplayName = "Battery",
            };

            // Critical battery action
            var criticalBatteryAction = new PowerSettingDefinition
            {
                Guid = "637ea02f-bbcb-4015-8e2c-a1c7b9c0b546",
                Alias = "criticalaction",
                DisplayName = "Critical battery action",
                Description =
                    "Specifies the action that Windows takes when battery capacity reaches the critical battery level.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Do nothing" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Sleep" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Hibernate" },
                    new PowerSettingValue { Index = 3, FriendlyName = "Shut down" },
                },
            };

            // Low battery level
            var lowBatteryLevel = new PowerSettingDefinition
            {
                Guid = "8183ba9a-e910-48da-8769-14ae6dc1170a",
                Alias = "batthreshold",
                DisplayName = "Low battery level",
                Description =
                    "Specifies the percentage of battery capacity that Windows considers to be low.",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 100,
                Increment = 1,
                Units = "%",
            };

            // Critical battery level
            var criticalBatteryLevel = new PowerSettingDefinition
            {
                Guid = "9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469",
                Alias = "batflagscritlevel",
                DisplayName = "Critical battery level",
                Description =
                    "Specifies the percentage of battery capacity that Windows considers to be critical.",
                SettingType = PowerSettingType.Numeric,
                SubgroupGuid = subgroup.Guid,
                MinValue = 0,
                MaxValue = 100,
                Increment = 1,
                Units = "%",
            };

            // Low battery notification
            var lowBatteryNotification = new PowerSettingDefinition
            {
                Guid = "bcded951-187b-4d05-bccc-f7e51960c258",
                Alias = "batflagslowlevelnotify",
                DisplayName = "Low battery notification",
                Description =
                    "Specifies whether Windows notifies the user when battery capacity reaches the low battery level.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Off" },
                    new PowerSettingValue { Index = 1, FriendlyName = "On" },
                },
            };

            subgroup.Settings.Add(criticalBatteryAction);
            subgroup.Settings.Add(lowBatteryLevel);
            subgroup.Settings.Add(criticalBatteryLevel);
            subgroup.Settings.Add(lowBatteryNotification);
            return subgroup;
        }

        /// <summary>
        /// Gets the Desktop background settings subgroup.
        /// </summary>
        /// <returns>The Desktop background settings subgroup.</returns>
        private static PowerSettingSubgroup GetDesktopBackgroundSettingsSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "0d7dbae2-4294-402a-ba8e-26777e8488cd",
                Alias = "desktopbackground",
                DisplayName = "Desktop background settings",
            };

            var slideshow = new PowerSettingDefinition
            {
                Guid = "309dce9b-bef4-4119-9921-a851fb12f0f4",
                Alias = "slideshow",
                DisplayName = "Slide show",
                Description =
                    "Specifies whether Windows desktop background slide show is available when on battery power.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Paused" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Available" },
                },
            };

            subgroup.Settings.Add(slideshow);

            return subgroup;
        }

        /// <summary>
        /// Gets the Wireless Adapter Settings subgroup.
        /// </summary>
        /// <returns>The Wireless Adapter Settings subgroup.</returns>
        private static PowerSettingSubgroup GetWirelessAdapterSettingsSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1",
                Alias = "wirelessadapter",
                DisplayName = "Wireless Adapter Settings",
            };

            var powerSavingMode = new PowerSettingDefinition
            {
                Guid = "12bbebe6-58d6-4636-95bb-3217ef867c1a",
                Alias = "powersavingmode",
                DisplayName = "Power Saving Mode",
                Description = "Specifies the power saving mode for the wireless adapter.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Maximum Performance" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Low Power Saving" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Medium Power Saving" },
                    new PowerSettingValue { Index = 3, FriendlyName = "Maximum Power Saving" },
                },
            };

            subgroup.Settings.Add(powerSavingMode);

            return subgroup;
        }

        /// <summary>
        /// Gets the USB settings subgroup.
        /// </summary>
        /// <returns>The USB settings subgroup.</returns>
        private static PowerSettingSubgroup GetUsbSettingsSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "2a737441-1930-4402-8d77-b2bebba308a3",
                Alias = "usb",
                DisplayName = "USB settings",
            };

            var usbSelectiveSuspendSetting = new PowerSettingDefinition
            {
                Guid = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226",
                Alias = "usbselectivesuspend",
                DisplayName = "USB selective suspend setting",
                Description = "Specifies whether the USB selective suspend feature is enabled.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Disabled" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Enabled" },
                },
            };

            subgroup.Settings.Add(usbSelectiveSuspendSetting);

            return subgroup;
        }

        /// <summary>
        /// Gets the PCI Express subgroup.
        /// </summary>
        /// <returns>The PCI Express subgroup.</returns>
        private static PowerSettingSubgroup GetPciExpressSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "501a4d13-42af-4429-9fd1-a8218c268e20",
                Alias = "pciexpress",
                DisplayName = "PCI Express",
            };

            var linkStatePowerManagement = new PowerSettingDefinition
            {
                Guid = "ee12f906-d277-404b-b6da-e5fa1a576df5",
                Alias = "aspm",
                DisplayName = "Link State Power Management",
                Description =
                    "Specifies the Active State Power Management (ASPM) policy to use for capable PCIe devices.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Off" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Moderate power savings" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Maximum power savings" },
                },
            };

            subgroup.Settings.Add(linkStatePowerManagement);

            return subgroup;
        }

        /// <summary>
        /// Gets the Multimedia settings subgroup.
        /// </summary>
        /// <returns>The Multimedia settings subgroup.</returns>
        private static PowerSettingSubgroup GetMultimediaSettingsSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                Alias = "multimedia",
                DisplayName = "Multimedia settings",
            };

            var whenSharingMedia = new PowerSettingDefinition
            {
                Guid = "03680956-93bc-4294-bba6-4e0f09bb717f",
                Alias = "sharingmedia",
                DisplayName = "When sharing media",
                Description =
                    "Specifies the power management behavior when sharing media with other devices.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue
                    {
                        Index = 0,
                        FriendlyName = "Allow the computer to sleep",
                    },
                    new PowerSettingValue { Index = 1, FriendlyName = "Prevent idling to sleep" },
                    new PowerSettingValue
                    {
                        Index = 2,
                        FriendlyName = "Allow the computer to enter Away Mode",
                    },
                },
            };

            var videoPlaybackQualityBias = new PowerSettingDefinition
            {
                Guid = "10778347-1370-4ee0-8bbd-33bdacaade49",
                Alias = "videoqualitybias",
                DisplayName = "Video playback quality bias",
                Description =
                    "Specifies the bias for video playback quality versus energy savings.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue
                    {
                        Index = 0,
                        FriendlyName = "Video playback power-saving bias",
                    },
                    new PowerSettingValue
                    {
                        Index = 1,
                        FriendlyName = "Video playback performance bias",
                    },
                },
            };

            var whenPlayingVideo = new PowerSettingDefinition
            {
                Guid = "34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4",
                Alias = "playingvideo",
                DisplayName = "When playing video",
                Description = "Specifies the power management behavior when playing video.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Optimize video quality" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Balanced" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Optimize power savings" },
                },
            };

            subgroup.Settings.Add(whenSharingMedia);
            subgroup.Settings.Add(videoPlaybackQualityBias);
            subgroup.Settings.Add(whenPlayingVideo);

            return subgroup;
        }

        /// <summary>
        /// Gets the AMD Power Slider subgroup.
        /// </summary>
        /// <returns>The AMD Power Slider subgroup.</returns>
        private static PowerSettingSubgroup GetAmdPowerSliderSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "c763b4ec-0e50-4b6b-9bed-2b92a6ee884e",
                Alias = "amdpowerslider",
                DisplayName = "AMD Power Slider",
            };

            var overlay = new PowerSettingDefinition
            {
                Guid = "7ec1751b-60ed-4588-afb5-9819d3d77d90",
                Alias = "overlay",
                DisplayName = "Overlay",
                Description = "Specifies the AMD Power Slider overlay settings.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Battery saver" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Better battery" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Better performance" },
                    new PowerSettingValue { Index = 3, FriendlyName = "Best performance" },
                },
            };

            subgroup.Settings.Add(overlay);

            return subgroup;
        }

        /// <summary>
        /// Gets the Internet Explorer subgroup.
        /// </summary>
        /// <returns>The Internet Explorer subgroup.</returns>
        private static PowerSettingSubgroup GetInternetExplorerSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "02f815b5-a5cf-4c84-bf20-649d1f75d3d8",
                Alias = "internetexplorer",
                DisplayName = "Internet Explorer",
            };

            var javascriptTimerFrequency = new PowerSettingDefinition
            {
                Guid = "4c793e7d-a264-42e1-87d3-7a0d2f523ccd",
                Alias = "javascripttimer",
                DisplayName = "JavaScript Timer Frequency",
                Description = "Specifies the frequency of JavaScript timers.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Maximum Power Savings" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Maximum Performance" },
                },
            };

            subgroup.Settings.Add(javascriptTimerFrequency);

            return subgroup;
        }

        /// <summary>
        /// Gets the Intel Graphics Power subgroup.
        /// </summary>
        /// <returns>The Intel Graphics Power subgroup.</returns>
        private static PowerSettingSubgroup GetIntelGraphicsPowerSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "44f3beca-a7c0-460e-9df2-bb8b99e0cba6",
                Alias = "intelgraphicspower",
                DisplayName = "Intel Graphics Power",
            };

            var graphicsPowerPlan = new PowerSettingDefinition
            {
                Guid = "3619c3f2-afb2-4afc-b0e9-e7fef372de36",
                Alias = "graphicspowerplan",
                DisplayName = "Graphics Power Plan",
                Description = "Specifies the power plan for Intel Graphics.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Maximum Battery Life" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Balanced Mode" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Maximum Performance" },
                },
            };

            subgroup.Settings.Add(graphicsPowerPlan);

            return subgroup;
        }

        /// <summary>
        /// Gets the ATI PowerPlay subgroup (Legacy AMD GPU Power Management).
        /// </summary>
        /// <returns>The ATI PowerPlay subgroup.</returns>
        private static PowerSettingSubgroup GetAtiPowerPlaySubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "f693fb01-e858-4f00-b20f-f30e12ac06d6",
                Alias = "atipowerplay",
                DisplayName = "ATI PowerPlay",
            };

            var powerPlaySetting = new PowerSettingDefinition
            {
                Guid = "191f65b5-d45c-4a4f-8aae-1ab8bfd980e6",
                Alias = "powerplaysetting",
                DisplayName = "PowerPlay Setting",
                Description = "Specifies the power management mode for legacy AMD GPUs.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Battery Optimized" },
                    new PowerSettingValue { Index = 1, FriendlyName = "Balanced" },
                    new PowerSettingValue { Index = 2, FriendlyName = "High Performance" },
                },
            };

            subgroup.Settings.Add(powerPlaySetting);

            return subgroup;
        }

        /// <summary>
        /// Gets the Switchable Graphics subgroup (Hybrid GPU Systems).
        /// </summary>
        /// <returns>The Switchable Graphics subgroup.</returns>
        private static PowerSettingSubgroup GetSwitchableGraphicsSubgroup()
        {
            var subgroup = new PowerSettingSubgroup
            {
                Guid = "e276e160-7cb0-43c6-b20b-73f5dce39954",
                Alias = "switchablegraphics",
                DisplayName = "Switchable Graphics",
            };

            var gpuPreference = new PowerSettingDefinition
            {
                Guid = "a1662ab2-9d34-4e53-ba8b-2639b9e20857",
                Alias = "gpupreference",
                DisplayName = "GPU Preference",
                Description = "Specifies which GPU to use in hybrid graphics systems.",
                SettingType = PowerSettingType.Enum,
                SubgroupGuid = subgroup.Guid,
                PossibleValues = new List<PowerSettingValue>
                {
                    new PowerSettingValue { Index = 0, FriendlyName = "Power Saving" },
                    new PowerSettingValue { Index = 1, FriendlyName = "High Performance" },
                    new PowerSettingValue { Index = 2, FriendlyName = "Dynamic Switching" },
                },
            };

            subgroup.Settings.Add(gpuPreference);

            return subgroup;
        }

        /// <summary>
        /// Gets a standard list of time interval values for power settings.
        /// </summary>
        /// <returns>A list of standard time interval values.</returns>
        private static List<PowerSettingTimeValue> GetStandardTimeIntervals()
        {
            return new List<PowerSettingTimeValue>
            {
                new PowerSettingTimeValue { Minutes = 0, DisplayName = "Never" },
                new PowerSettingTimeValue { Minutes = 1, DisplayName = "1 minute" },
                new PowerSettingTimeValue { Minutes = 2, DisplayName = "2 minutes" },
                new PowerSettingTimeValue { Minutes = 3, DisplayName = "3 minutes" },
                new PowerSettingTimeValue { Minutes = 5, DisplayName = "5 minutes" },
                new PowerSettingTimeValue { Minutes = 10, DisplayName = "10 minutes" },
                new PowerSettingTimeValue { Minutes = 15, DisplayName = "15 minutes" },
                new PowerSettingTimeValue { Minutes = 20, DisplayName = "20 minutes" },
                new PowerSettingTimeValue { Minutes = 25, DisplayName = "25 minutes" },
                new PowerSettingTimeValue { Minutes = 30, DisplayName = "30 minutes" },
                new PowerSettingTimeValue { Minutes = 45, DisplayName = "45 minutes" },
                new PowerSettingTimeValue { Minutes = 60, DisplayName = "1 hour" },
                new PowerSettingTimeValue { Minutes = 120, DisplayName = "2 hours" },
                new PowerSettingTimeValue { Minutes = 180, DisplayName = "3 hours" },
                new PowerSettingTimeValue { Minutes = 240, DisplayName = "4 hours" },
                new PowerSettingTimeValue { Minutes = 300, DisplayName = "5 hours" },
            };
        }
    }

    /// <summary>
    /// Provides predefined power setting presets for different power plans.
    /// </summary>
    public static class PowerPlanPresets
    {
        /// <summary>
        /// Gets all available power plan presets with their recommended settings.
        /// </summary>
        /// <returns>Dictionary of power plan names to their setting values.</returns>
        public static Dictionary<string, Dictionary<string, object>> GetPresets()
        {
            return new Dictionary<string, Dictionary<string, object>>
            {
                ["Ultimate Performance"] = GetUltimatePerformancePreset(),
                ["High Performance"] = GetHighPerformancePreset(),
                ["Balanced"] = GetBalancedPreset(),
                ["Power Saver"] = GetPowerSaverPreset(),
                ["Gaming"] = GetGamingPreset()
            };
        }

        /// <summary>
        /// Gets the Ultimate Performance preset - maximum performance, no power saving.
        /// </summary>
        private static Dictionary<string, object> GetUltimatePerformancePreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Never turn off
                ["diskidle"] = 0,
                
                // Sleep - Never sleep or hibernate
                ["standbyidle"] = 0,
                ["hibernateidle"] = 0,
                ["hybridsleep"] = 0,
                ["waketimers"] = 1, // Enable wake timers
                
                // Processor - Maximum performance
                ["procthrottlemin"] = 100, // 100% minimum processor state
                ["procthrottlemax"] = 100, // 100% maximum processor state
                ["syscoolingpolicy"] = 1,  // Active cooling
                
                // Display - Never turn off
                ["videoidle"] = 0,
                ["videoconlock"] = 0,
                
                // USB - Disable power saving
                ["usbselectivesuspend"] = 0,
                ["usbidle"] = 0,
                ["usb3linkpowermgmt"] = 0,
                
                // Wireless - Maximum performance
                ["wirelessidle"] = 0, // Maximum performance
                
                // PCI Express - No power saving
                ["aspm"] = 0, // Off
                
                // Desktop Background - Available
                ["slideshow"] = 1,
                
                // Power Buttons
                ["powerbutton"] = 2, // Do nothing
                ["sleepbutton"] = 2, // Do nothing
                ["lidaction"] = 2,   // Do nothing
            };
        }

        /// <summary>
        /// Gets the High Performance preset - high performance with minimal power saving.
        /// </summary>
        private static Dictionary<string, object> GetHighPerformancePreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Turn off after 20 minutes
                ["diskidle"] = 20,
                
                // Sleep - Never sleep, hibernate after 3 hours
                ["standbyidle"] = 0,
                ["hibernateidle"] = 180,
                ["hybridsleep"] = 0,
                ["waketimers"] = 1,
                
                // Processor - High performance
                ["procthrottlemin"] = 100,
                ["procthrottlemax"] = 100,
                ["syscoolingpolicy"] = 1,
                
                // Display - Turn off after 20 minutes
                ["videoidle"] = 20,
                ["videoconlock"] = 0,
                
                // USB - Minimal power saving
                ["usbselectivesuspend"] = 0,
                ["usbidle"] = 0,
                ["usb3linkpowermgmt"] = 1,
                
                // Wireless - High performance
                ["wirelessidle"] = 0,
                
                // PCI Express - Moderate power saving
                ["aspm"] = 1, // Moderate power savings
                
                // Desktop Background
                ["slideshow"] = 1,
                
                // Power Buttons
                ["powerbutton"] = 1, // Sleep
                ["sleepbutton"] = 1, // Sleep
                ["lidaction"] = 1,   // Sleep
            };
        }

        /// <summary>
        /// Gets the Balanced preset - balanced performance and power saving.
        /// </summary>
        private static Dictionary<string, object> GetBalancedPreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Turn off after 20 minutes
                ["diskidle"] = 20,
                
                // Sleep - Sleep after 30 minutes, hibernate after 3 hours
                ["standbyidle"] = 30,
                ["hibernateidle"] = 180,
                ["hybridsleep"] = 1,
                ["waketimers"] = 1,
                
                // Processor - Balanced
                ["procthrottlemin"] = 5,   // 5% minimum
                ["procthrottlemax"] = 100, // 100% maximum
                ["syscoolingpolicy"] = 0,  // Passive cooling
                
                // Display - Turn off after 15 minutes
                ["videoidle"] = 15,
                ["videoconlock"] = 0,
                
                // USB - Enabled power saving
                ["usbselectivesuspend"] = 1,
                ["usbidle"] = 1,
                ["usb3linkpowermgmt"] = 2,
                
                // Wireless - Balanced
                ["wirelessidle"] = 2, // Medium power saving
                
                // PCI Express - Moderate power saving
                ["aspm"] = 1,
                
                // Desktop Background
                ["slideshow"] = 1,
                
                // Power Buttons
                ["powerbutton"] = 1, // Sleep
                ["sleepbutton"] = 1, // Sleep
                ["lidaction"] = 1,   // Sleep
            };
        }

        /// <summary>
        /// Gets the Power Saver preset - maximum power saving.
        /// </summary>
        private static Dictionary<string, object> GetPowerSaverPreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Turn off after 5 minutes
                ["diskidle"] = 5,
                
                // Sleep - Sleep after 15 minutes, hibernate after 1 hour
                ["standbyidle"] = 15,
                ["hibernateidle"] = 60,
                ["hybridsleep"] = 1,
                ["waketimers"] = 0, // Disable wake timers
                
                // Processor - Power saving
                ["procthrottlemin"] = 5,  // 5% minimum
                ["procthrottlemax"] = 50, // 50% maximum
                ["syscoolingpolicy"] = 0, // Passive cooling
                
                // Display - Turn off after 5 minutes
                ["videoidle"] = 5,
                ["videoconlock"] = 1,
                
                // USB - Maximum power saving
                ["usbselectivesuspend"] = 1,
                ["usbidle"] = 1,
                ["usb3linkpowermgmt"] = 3, // Maximum power savings
                
                // Wireless - Maximum power saving
                ["wirelessidle"] = 3,
                
                // PCI Express - Maximum power saving
                ["aspm"] = 2,
                
                // Desktop Background - Paused
                ["slideshow"] = 0,
                
                // Power Buttons
                ["powerbutton"] = 1, // Sleep
                ["sleepbutton"] = 1, // Sleep
                ["lidaction"] = 1,   // Sleep
            };
        }

        /// <summary>
        /// Gets the Gaming preset - optimized for gaming performance.
        /// </summary>
        private static Dictionary<string, object> GetGamingPreset()
        {
            return new Dictionary<string, object>
            {
                // Hard Disk - Never turn off during gaming
                ["diskidle"] = 0,
                
                // Sleep - Never sleep during gaming
                ["standbyidle"] = 0,
                ["hibernateidle"] = 0,
                ["hybridsleep"] = 0,
                ["waketimers"] = 1,
                
                // Processor - Maximum performance
                ["procthrottlemin"] = 100,
                ["procthrottlemax"] = 100,
                ["syscoolingpolicy"] = 1, // Active cooling for better temps
                
                // Display - Never turn off
                ["videoidle"] = 0,
                ["videoconlock"] = 0,
                
                // USB - Disable power saving for gaming peripherals
                ["usbselectivesuspend"] = 0,
                ["usbidle"] = 0,
                ["usb3linkpowermgmt"] = 0,
                
                // Wireless - Maximum performance for online gaming
                ["wirelessidle"] = 0,
                
                // PCI Express - No power saving for GPU
                ["aspm"] = 0,
                
                // Desktop Background - Available
                ["slideshow"] = 1,
                
                // Power Buttons - Prevent accidental sleep
                ["powerbutton"] = 2, // Do nothing
                ["sleepbutton"] = 2, // Do nothing
                ["lidaction"] = 2,   // Do nothing
            };
        }

        /// <summary>
        /// Applies a preset to the current power optimization settings.
        /// </summary>
        /// <param name="presetName">The name of the preset to apply.</param>
        /// <param name="currentSettings">The current optimization settings to update.</param>
        /// <returns>The updated settings with preset values applied.</returns>
        public static List<OptimizationSetting> ApplyPreset(string presetName, List<OptimizationSetting> currentSettings)
        {
            var presets = GetPresets();
            if (!presets.ContainsKey(presetName))
            {
                return currentSettings; // Return unchanged if preset not found
            }

            var presetValues = presets[presetName];
            var updatedSettings = new List<OptimizationSetting>();
            
            foreach (var setting in currentSettings)
            {
                // Extract the alias from the setting ID (format: "power-{alias}")
                var alias = setting.Id.StartsWith("power-") ? setting.Id.Substring(6) : setting.Id;
                
                if (presetValues.ContainsKey(alias))
                {
                    var presetValue = presetValues[alias];
                    
                    // Create a new setting with the preset value applied
                    var updatedSetting = setting with
                    {
                        IsEnabled = Convert.ToBoolean(presetValue)
                    };
                    
                    updatedSettings.Add(updatedSetting);
                }
                else
                {
                    updatedSettings.Add(setting);
                }
            }

            return updatedSettings;
        }

        /// <summary>
        /// Gets the list of available preset names.
        /// </summary>
        /// <returns>List of preset names.</returns>
        public static List<string> GetPresetNames()
        {
            return GetPresets().Keys.ToList();
        }
    }
}

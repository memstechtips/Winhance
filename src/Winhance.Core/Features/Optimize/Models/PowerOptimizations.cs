using System;
using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

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

public static class PowerOptimizations
{
    /// <summary>
    /// Gets all power optimizations as an OptimizationGroup.
    /// </summary>
    /// <returns>An OptimizationGroup containing all power settings.</returns>
    public static OptimizationGroup GetPowerOptimizations()
    {
        return new OptimizationGroup
        {
            Name = "Power",
            Category = OptimizationCategory.Power,
            Settings = new List<OptimizationSetting>
            {
                // Hibernate Settings
                new OptimizationSetting
                {
                    Id = "power-hibernate-enabled",
                    Name = "Hibernate",
                    Description = "Controls whether hibernate is enabled",
                    Category = OptimizationCategory.Power,
                    GroupName = "Power Management",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Power",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SYSTEM\\CurrentControlSet\\Control\\Power",
                            Name = "HibernateEnabled",
                            RecommendedValue = 0,  // For backward compatibility
                            EnabledValue = 1,      // When toggle is ON, hibernate is enabled
                            DisabledValue = 0,     // When toggle is OFF, hibernate is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,      // Default value when registry key exists but no value is set
                            Description = "Controls whether hibernate is enabled",
                            IsPrimary = true,      // Mark as primary for linked settings
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Power",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SYSTEM\\CurrentControlSet\\Control\\Power",
                            Name = "HibernateEnabledDefault",
                            RecommendedValue = 0,  // For backward compatibility
                            EnabledValue = 1,      // When toggle is ON, hibernate is enabled
                            DisabledValue = 0,     // When toggle is OFF, hibernate is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,      // Default value when registry key exists but no value is set
                            Description = "Controls whether hibernate is enabled by default",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.Primary,
                },
                // Video Settings
                new OptimizationSetting
                {
                    Id = "power-video-quality",
                    Name = "High Video Quality on Battery",
                    Description = "Controls video quality when running on battery power",
                    Category = OptimizationCategory.Power,
                    GroupName = "Display & Graphics",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Power",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\VideoSettings",
                            Name = "VideoQualityOnBattery",
                            RecommendedValue = 1,   // For backward compatibility
                            EnabledValue = 1,      // High quality (from Recommended)
                            DisabledValue = 0,     // Lower quality (from Default)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,      // Default value when registry key exists but no value is set
                            Description = "Controls video quality when running on battery power",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                // Performance Settings
                new OptimizationSetting
                {
                    Id = "power-fast-boot",
                    Name = "Fast Boot",
                    Description = "Controls whether fast boot (hybrid boot) is enabled",
                    Category = OptimizationCategory.Power,
                    GroupName = "Performance",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Power",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power",
                            Name = "HiberbootEnabled",
                            RecommendedValue = 0,   // For backward compatibility
                            EnabledValue = 1,      //  Enable fast boot
                            DisabledValue = 0,     // Disable fast boot
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,      // Default value when registry key exists but no value is set
                            Description = "Controls whether fast boot (hybrid boot) is enabled",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                // Sleep Settings
                new OptimizationSetting
                {
                    Id = "power-sleep-settings",
                    Name = "Sleep & Hibernate",
                    Description =
                        "When enabled, prevents your computer from going to sleep or hibernating",
                    Category = OptimizationCategory.Power,
                    GroupName = "Sleep & Hibernate",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command = "powercfg /hibernate off",
                                    Description = "Disable hibernate",
                                    EnabledValue = "/hibernate off",
                                    DisabledValue = "/hibernate on",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000",
                                    Description = "Set sleep timeout (AC) to never",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000",
                                    DisabledValue =
                                        "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000",
                                    Description = "Set sleep timeout (DC) to never",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000",
                                    DisabledValue =
                                        "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e",
                                },
                            }
                        },
                    },
                },
                // Display Settings
                new OptimizationSetting
                {
                    Id = "power-display-settings",
                    Name = "Display Always On",
                    Description =
                        "When enabled, prevents your display from turning off and sets maximum brightness",
                    Category = OptimizationCategory.Power,
                    GroupName = "Display & Graphics",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 0x00000000",
                                    Description = "Set display timeout (AC) to never",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 0x00000000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 0x00000258",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 0x00000000",
                                    Description = "Set display timeout (DC) to never",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 0x00000000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 0x00000258",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 aded5e82-b909-4619-9949-f5d71dac0bcb 0x00000064",
                                    Description = "Set display brightness (AC) to 100%",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 aded5e82-b909-4619-9949-f5d71dac0bcb 0x00000064",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 aded5e82-b909-4619-9949-f5d71dac0bcb 0x00000032",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000",
                                    Description = "Disable adaptive brightness (AC)",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000",
                                    Description = "Disable adaptive brightness (DC)",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 001",
                                },
                            }
                        },
                    },
                },
                // Processor Settings
                new OptimizationSetting
                {
                    Id = "power-processor-settings",
                    Name = "CPU Performance",
                    Description =
                        "When enabled, sets processor to run at 100% power with active cooling",
                    Category = OptimizationCategory.Power,
                    GroupName = "Performance",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c 0x00000064",
                                    Description = "Set processor minimum state (AC) to 100%",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c 0x00000064",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c 0x00000005",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 0x00000064",
                                    Description = "Set processor maximum state (AC) to 100%",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 0x00000064",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 0x00000064",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 001",
                                    Description = "Set cooling policy (AC) to active",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 001",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 000",
                                },
                            }
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "power-cpu-unpark",
                    Name = "CPU Core Unparking",
                    Description = "Controls CPU core parking for better performance",
                    Category = OptimizationCategory.Power,
                    GroupName = "Performance",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Power",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "SYSTEM\\ControlSet001\\Control\\Power\\PowerSettings\\54533251-82be-4824-96c1-47b60b740d00\\0cc5b647-c1df-4637-891a-dec35c318583",
                            Name = "ValueMax",
                            RecommendedValue = 0,   // For backward compatibility
                            EnabledValue = 0,      // Unpark CPU cores (from Recommended)
                            DisabledValue = 1,     // Allow CPU core parking (from Default)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,      // Default value when registry key exists but no value is set
                            Description = "Controls CPU core parking for better performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "power-throttling",
                    Name = "Power Throttling",
                    Description = "Controls power throttling for better performance",
                    Category = OptimizationCategory.Power,
                    GroupName = "Performance",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Power",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SYSTEM\\CurrentControlSet\\Control\\Power\\PowerThrottling",
                            Name = "PowerThrottlingOff",
                            RecommendedValue = 1,   // For backward compatibility
                            EnabledValue = 0,      // Enable power throttling
                            DisabledValue = 1,     // Disable power throttling
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,      // Default value when registry key exists but no value is set
                            Description = "Controls power throttling for better performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                // Storage Settings
                new OptimizationSetting
                {
                    Id = "power-hard-disk-settings",
                    Name = "Hard Disks Always On",
                    Description =
                        "When enabled, prevents hard disks from turning off to improve performance",
                    Category = OptimizationCategory.Power,
                    GroupName = "Storage",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000",
                                    Description = "Set hard disk timeout (AC) to never",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000258",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000",
                                    Description = "Set hard disk timeout (DC) to never",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000258",
                                },
                            }
                        },
                    },
                },
                // Desktop & Display Settings
                new OptimizationSetting
                {
                    Id = "power-desktop-slideshow-settings",
                    Name = "Desktop Slideshow",
                    Description =
                        "When enabled, allows desktop background slideshow to run even on battery power",
                    Category = OptimizationCategory.Power,
                    GroupName = "Desktop & Display",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 000",
                                    Description =
                                        "Desktop slideshow (AC) - Value 0 means Available",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 000",
                                    Description =
                                        "Desktop slideshow (DC) - Value 0 means Available",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 001",
                                },
                            }
                        },
                    },
                },
                // Network Settings
                new OptimizationSetting
                {
                    Id = "power-wireless-adapter-settings",
                    Name = "Wi-Fi Performance",
                    Description = "When enabled, sets wireless adapter to maximum performance mode",
                    Category = OptimizationCategory.Power,
                    GroupName = "Network",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000",
                                    Description =
                                        "Wireless adapter power saving mode (AC) - Maximum performance",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000",
                                    Description =
                                        "Wireless adapter power saving mode (DC) - Maximum performance",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 001",
                                },
                            }
                        },
                    },
                },
                // Sleep & Hibernate Settings
                new OptimizationSetting
                {
                    Id = "power-sleep-hibernate-settings",
                    Name = "All Sleep Features",
                    Description =
                        "When enabled, disables sleep, hybrid sleep, hibernate, and wake timers",
                    Category = OptimizationCategory.Power,
                    GroupName = "Sleep & Hibernate",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000",
                                    Description = "Sleep timeout (AC) - Never",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000384",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000",
                                    Description = "Sleep timeout (DC) - Never",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000384",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000",
                                    Description = "Hybrid sleep (AC) - Off",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000",
                                    Description = "Hybrid sleep (DC) - Off",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000",
                                    Description = "Hibernate timeout (AC) - Never",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000384",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000",
                                    Description = "Hibernate timeout (DC) - Never",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000384",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000",
                                    Description = "Wake timers (AC) - Disabled",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000",
                                    Description = "Wake timers (DC) - Disabled",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 001",
                                },
                            }
                        },
                    },
                },
                // USB Settings
                new OptimizationSetting
                {
                    Id = "power-usb-settings",
                    Name = "USB Devices Always On",
                    Description =
                        "When enabled, prevents USB devices from being powered down to save energy",
                    Category = OptimizationCategory.Power,
                    GroupName = "USB",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000",
                                    Description = "USB hub timeout (AC) - Never",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000258",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000",
                                    Description = "USB hub timeout (DC) - Never",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000258",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000",
                                    Description = "USB selective suspend (AC) - Disabled",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000",
                                    Description = "USB selective suspend (DC) - Disabled",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000",
                                    Description = "USB 3.0 link power management (AC) - Disabled",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000",
                                    Description = "USB 3.0 link power management (DC) - Disabled",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 001",
                                },
                            }
                        },
                    },
                },
                // Power Button Settings
                new OptimizationSetting
                {
                    Id = "power-button-settings",
                    Name = "Power Button Shutdown",
                    Description =
                        "When enabled, sets power button to immediately shut down your computer",
                    Category = OptimizationCategory.Power,
                    GroupName = "Power Buttons",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002",
                                    Description = "Power button action (AC) - Shut down",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002",
                                    Description = "Power button action (DC) - Shut down",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 000",
                                },
                            }
                        },
                    },
                },
                // PCI Express Settings
                new OptimizationSetting
                {
                    Id = "power-pci-express-settings",
                    Name = "PCI Express Performance",
                    Description =
                        "When enabled, disables power saving features for PCI Express devices",
                    Category = OptimizationCategory.Power,
                    GroupName = "PCI Express",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000",
                                    Description = "PCI Express power management (AC) - Disabled",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000",
                                    Description = "PCI Express power management (DC) - Disabled",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 001",
                                },
                            }
                        },
                    },
                },
                // Video Playback Settings
                new OptimizationSetting
                {
                    Id = "power-video-playback-settings",
                    Name = "Video Playback Quality",
                    Description =
                        "When enabled, ensures maximum video quality even when on battery power",
                    Category = OptimizationCategory.Power,
                    GroupName = "Video Playback",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001",
                                    Description =
                                        "Video playback quality (AC) - Optimized for performance",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001",
                                    Description =
                                        "Video playback quality (DC) - Optimized for performance",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000",
                                    Description =
                                        "Video quality reduction on battery (AC) - Disabled",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000",
                                    Description =
                                        "Video quality reduction on battery (DC) - Disabled",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 001",
                                },
                            }
                        },
                    },
                },
                // Graphics Settings
                new OptimizationSetting
                {
                    Id = "power-graphics-settings",
                    Name = "GPU Performance",
                    Description = "When enabled, sets graphics cards to run at maximum performance",
                    Category = OptimizationCategory.Power,
                    GroupName = "Graphics",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002",
                                    Description = "Intel graphics power (AC) - Maximum performance",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002",
                                    Description = "Intel graphics power (DC) - Maximum performance",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003",
                                    Description = "AMD power slider (AC) - Maximum performance",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003",
                                    Description = "AMD power slider (DC) - Maximum performance",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001",
                                    Description = "ATI PowerPlay (AC) - Enabled",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001",
                                    Description = "ATI PowerPlay (DC) - Enabled",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003",
                                    Description = "Switchable graphics (AC) - Maximum performance",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 000",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003",
                                    Description = "Switchable graphics (DC) - Maximum performance",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 000",
                                },
                            }
                        },
                    },
                },
                // Battery Settings
                new OptimizationSetting
                {
                    Id = "power-battery-settings",
                    Name = "Battery Notifications",
                    Description =
                        "When enabled, turns off low battery and critical battery notifications",
                    Category = OptimizationCategory.Power,
                    GroupName = "Battery",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000",
                                    Description = "Critical battery notification (AC) - Disabled",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000",
                                    Description = "Critical battery notification (DC) - Disabled",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000",
                                    Description = "Critical battery action (AC) - Do nothing",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000",
                                    Description = "Critical battery action (DC) - Do nothing",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000",
                                    Description = "Low battery level (AC) - 0%",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x0000000A",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000",
                                    Description = "Low battery level (DC) - 0%",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x0000000A",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000",
                                    Description = "Low battery notification (AC) - Disabled",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 001",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000",
                                    Description = "Low battery notification (DC) - Disabled",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 001",
                                },
                            }
                        },
                    },
                },
                // Battery Saver Settings
                new OptimizationSetting
                {
                    Id = "power-battery-saver-settings",
                    Name = "Battery Saver",
                    Description =
                        "When enabled, prevents battery saver from activating at any battery level",
                    Category = OptimizationCategory.Power,
                    GroupName = "Battery Saver",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CustomProperties = new Dictionary<string, object>
                    {
                        {
                            "PowerCfgSettings",
                            new List<PowerCfgSetting>
                            {
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064",
                                    Description = "Battery saver brightness (AC) - 100%",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000032",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064",
                                    Description = "Battery saver brightness (DC) - 100%",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000032",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setacvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000",
                                    Description = "Battery saver threshold (AC) - 0%",
                                    EnabledValue =
                                        "/setacvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000",
                                    DisabledValue =
                                        "/setacvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000032",
                                },
                                new PowerCfgSetting
                                {
                                    Command =
                                        "powercfg /setdcvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000",
                                    Description = "Battery saver threshold (DC) - 0%",
                                    EnabledValue =
                                        "/setdcvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000",
                                    DisabledValue =
                                        "/setdcvalueindex {active_guid} de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000032",
                                },
                            }
                        },
                    },
                },
            },
        };
    }

    /// <summary>
    /// Contains all the powercfg commands for the Ultimate Performance Power Plan.
    /// </summary>
    public static class UltimatePerformancePowerPlan
    {
        public static readonly Dictionary<string, string> PowerCfgCommands = new()
        {
            // Create and set the Ultimate Performance power plan
            {
                "CreateUltimatePlan",
                "/duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 99999999-9999-9999-9999-999999999999"
            },
            { "SetActivePlan", "/SETACTIVE 99999999-9999-9999-9999-999999999999" },
            // Hard disk settings
            {
                "HardDiskTimeout_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000"
            },
            {
                "HardDiskTimeout_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000"
            },
            // Desktop background slideshow
            {
                "DesktopSlideshow_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 001"
            },
            {
                "DesktopSlideshow_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 001"
            },
            // Wireless adapter settings
            {
                "WirelessAdapter_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000"
            },
            {
                "WirelessAdapter_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000"
            },
            // Sleep settings
            {
                "SleepTimeout_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000"
            },
            {
                "SleepTimeout_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000"
            },
            {
                "HybridSleep_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000"
            },
            {
                "HybridSleep_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000"
            },
            {
                "HibernateTimeout_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000"
            },
            {
                "HibernateTimeout_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000"
            },
            {
                "WakeTimers_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000"
            },
            {
                "WakeTimers_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000"
            },
            // USB settings
            {
                "UsbHubTimeout_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000"
            },
            {
                "UsbHubTimeout_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000"
            },
            {
                "UsbSelectiveSuspend_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000"
            },
            {
                "UsbSelectiveSuspend_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000"
            },
            {
                "Usb3LinkPower_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000"
            },
            {
                "Usb3LinkPower_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000"
            },
            // Power button action
            {
                "PowerButtonAction_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002"
            },
            {
                "PowerButtonAction_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002"
            },
            // PCI Express
            {
                "PciExpressPower_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000"
            },
            {
                "PciExpressPower_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000"
            },
            // Processor settings
            {
                "ProcessorMinState_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c 0x00000064"
            },
            {
                "ProcessorMinState_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c 0x00000064"
            },
            {
                "CoolingPolicy_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 001"
            },
            {
                "CoolingPolicy_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 001"
            },
            {
                "ProcessorMaxState_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 0x00000064"
            },
            {
                "ProcessorMaxState_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 0x00000064"
            },
            // Display settings
            {
                "DisplayTimeout_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 0x00000000"
            },
            {
                "DisplayTimeout_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 0x00000000"
            },
            {
                "DisplayBrightness_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 7516b95f-f776-4464-8c53-06167f40cc99 aded5e82-b909-4619-9949-f5d71dac0bcb 0x00000064"
            },
            {
                "DisplayBrightness_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 7516b95f-f776-4464-8c53-06167f40cc99 aded5e82-b909-4619-9949-f5d71dac0bcb 0x00000064"
            },
            {
                "DimmedBrightness_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 7516b95f-f776-4464-8c53-06167f40cc99 f1fbfde2-a960-4165-9f88-50667911ce96 0x00000064"
            },
            {
                "DimmedBrightness_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 7516b95f-f776-4464-8c53-06167f40cc99 f1fbfde2-a960-4165-9f88-50667911ce96 0x00000064"
            },
            {
                "AdaptiveBrightness_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000"
            },
            {
                "AdaptiveBrightness_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000"
            },
            // Video playback settings
            {
                "VideoPlaybackQuality_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001"
            },
            {
                "VideoPlaybackQuality_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001"
            },
            {
                "VideoPlaybackQualityOnBattery_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000"
            },
            {
                "VideoPlaybackQualityOnBattery_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000"
            },
            // Graphics settings
            {
                "IntelGraphicsPower_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002"
            },
            {
                "IntelGraphicsPower_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002"
            },
            {
                "AmdPowerSlider_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003"
            },
            {
                "AmdPowerSlider_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003"
            },
            {
                "AtiPowerPlay_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001"
            },
            {
                "AtiPowerPlay_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001"
            },
            {
                "SwitchableGraphics_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003"
            },
            {
                "SwitchableGraphics_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003"
            },
            // Battery settings
            {
                "CriticalBatteryNotification_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000"
            },
            {
                "CriticalBatteryNotification_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000"
            },
            {
                "CriticalBatteryAction_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000"
            },
            {
                "CriticalBatteryAction_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000"
            },
            {
                "LowBatteryLevel_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000"
            },
            {
                "LowBatteryLevel_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000"
            },
            {
                "CriticalBatteryLevel_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f 9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469 0x00000000"
            },
            {
                "CriticalBatteryLevel_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f 9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469 0x00000000"
            },
            {
                "LowBatteryNotification_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000"
            },
            {
                "LowBatteryNotification_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000"
            },
            {
                "LowBatteryAction_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f d8742dcb-3e6a-4b3c-b3fe-374623cdcf06 000"
            },
            {
                "LowBatteryAction_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f d8742dcb-3e6a-4b3c-b3fe-374623cdcf06 000"
            },
            {
                "ReserveBatteryLevel_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f f3c5027d-cd16-4930-aa6b-90db844a8f00 0x00000000"
            },
            {
                "ReserveBatteryLevel_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 e73a048d-bf27-4f12-9731-8b2076e8891f f3c5027d-cd16-4930-aa6b-90db844a8f00 0x00000000"
            },
            // Battery Saver settings
            {
                "BatterySaverBrightness_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064"
            },
            {
                "BatterySaverBrightness_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064"
            },
            {
                "BatterySaverThreshold_AC",
                "/setacvalueindex 99999999-9999-9999-9999-999999999999 de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000"
            },
            {
                "BatterySaverThreshold_DC",
                "/setdcvalueindex 99999999-9999-9999-9999-999999999999 de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000"
            },
        };
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
}

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
                // Empty for now as no registry keys are defined for Power settings.
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

using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Microsoft.Win32;

namespace Winhance.Core.Features.Optimize.Models
{
    public static class PowerOptimizations
    {
        public static SettingGroup GetPowerOptimizations()
        {
            return new SettingGroup
            {
                Name = "Power",
                FeatureId = FeatureIds.Power,
                Settings = new List<SettingDefinition>
                {
                    new SettingDefinition
                    {
                        Id = "power-plan-selection",
                        Name = "Power Plan",
                        Description = "Select the active power plan for your system",
                        GroupName = "Power Plan",
                        InputType = InputType.Selection,
                        RequiresDomainServiceContext = true,
                        CustomProperties = new Dictionary<string, object>
                        {
                            ["LoadDynamicOptions"] = true
                        }
                    },
                    new SettingDefinition
                    {
                        Id = "power-display-timeout",
                        Name = "Turn off the display",
                        Description = "Specifies the period of inactivity before Windows turns off the display",
                        GroupName = "Display",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_VIDEO",
                                SettingGUIDAlias = "VIDEOIDLE",
                                SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
                                SettingGuid = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.TimeIntervals
                    },

                    new SettingDefinition
                    {
                        Id = "power-sleep-timeout",
                        Name = "Put the computer to sleep",
                        Description = "Specifies the period of inactivity before Windows puts the computer to sleep",
                        GroupName = "Sleep",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_SLEEP",
                                SettingGUIDAlias = "STANBYIDLE",
                                SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                                SettingGuid = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.TimeIntervals
                    },

                    new SettingDefinition
                    {
                        Id = "power-harddisk-timeout",
                        Name = "Turn off hard disk after",
                        Description = "Specifies the period of inactivity before Windows turns off the hard disk",
                        GroupName = "Hard Disk",
                        RequiresDesktop = true,
                        InputType = InputType.NumericRange,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_DISK",
                                SettingGUIDAlias = "DISKIDLE",
                                SubgroupGuid = "0012ee47-9041-4b5d-9b77-535fba8b1442",
                                SettingGuid = "6738e2c4-e8a5-4a42-b16a-e040e769756e",
                                ApplyToACDC = true,
                                Units = "Seconds"
                            }
                        },
                        CustomProperties = Templates.CreateNumericRange(0, int.MaxValue, "Minutes")
                    },

                    new SettingDefinition
                    {
                        Id = "desktop-slideshow",
                        Name = "Desktop Background Slide Show",
                        Description = "Specify if you want the desktop background slide show to be available",
                        GroupName = "Desktop Background Settings",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "0d7dbae2-4294-402a-ba8e-26777e8488cd",
                                SettingGuid = "309dce9b-bef4-4119-9921-a851fb12f0f4",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.Slideshow
                    },

                    new SettingDefinition
                    {
                        Id = "wireless-power-mode",
                        Name = "Wireless Adapter Power Saving Mode",
                        Description = "Specifies the power saving mode for the wireless adapter",
                        GroupName = "Wireless Adapter Settings",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1",
                                SettingGuid = "12bbebe6-58d6-4636-95bb-3217ef867c1a",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.WirelessPower
                    },

                    new SettingDefinition
                    {
                        Id = "power-wake-timers",
                        Name = "Allow wake timers",
                        Description = "Determines whether Windows allows or ignores wake timer events",
                        GroupName = "Sleep",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_SLEEP",
                                SettingGUIDAlias = "RTCWAKE",
                                SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                                SettingGuid = "bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.WakeTimers
                    },

                    new SettingDefinition
                    {
                        Id = "usb-selective-suspend",
                        Name = "USB selective suspend setting",
                        Description = "Specifies whether the USB selective suspend feature is enabled",
                        GroupName = "USB settings",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "2a737441-1930-4402-8d77-b2bebba308a3",
                                SettingGuid = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.EnabledDisabled
                    },

                    new SettingDefinition
                    {
                        Id = "pci-link-state-power-management",
                        Name = "Link State Power Management",
                        Description = "Specifies the Active State Power Management (ASPM) policy to use for capable PCIe devices",
                        GroupName = "PCI Express",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PCIEXPRESS",
                                SettingGUIDAlias = "ASPM",
                                SubgroupGuid = "501a4d13-42af-4429-9fd1-a8218c268e20",
                                SettingGuid = "ee12f906-d277-404b-b6da-e5fa1a576df5",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.PciExpress
                    },

                    new SettingDefinition
                    {
                        Id = "system-cooling-policy",
                        Name = "System cooling policy",
                        Description = "Specifies the cooling mode that Windows uses for the current power plan",
                        GroupName = "Processor Power Management",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "94d3a615-a899-4ac5-ae2b-e4d8f634367f",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.CoolingPolicy
                    },

                    new SettingDefinition
                    {
                        Id = "processor-core-parking-min-cores",
                        Name = "CPU Core Parking",
                        Description = "Specifies the minimum number of cores that can be unparked in Processor Power Management (0 = Cores Unparked)",
                        GroupName = "Processor Power Management",
                        InputType = InputType.NumericRange,
                        ValidateExistence = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "CPMINCORES",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "0cc5b647-c1df-4637-891a-dec35c318583",
                                ApplyToACDC = true,
                                Units = "%",
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = Templates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "processor-performance-boost-mode",
                        Name = "Processor performance boost mode",
                        Description = "Specifies the processor performance boost mode policy",
                        GroupName = "Processor Power Management",
                        InputType = InputType.Selection,
                        ValidateExistence = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PERFBOOSTMODE",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "be337238-0d82-4146-a960-4f3749d470c7",
                                ApplyToACDC = true,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\be337238-0d82-4146-a960-4f3749d470c7",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = Templates.ProcessorBoostMode
                    },

                    new SettingDefinition
                    {
                        Id = "processor-min-state",
                        Name = "Minimum processor state",
                        Description = "Specifies the minimum processor performance state (as a percentage)",
                        GroupName = "Processor Power Management",
                        InputType = InputType.NumericRange,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PROCTHROTTLEMIN",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "893dee8e-2bef-41e0-89c6-b55d0929964c",
                                ApplyToACDC = true,
                                Units = "%"
                            }
                        },
                        CustomProperties = Templates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "processor-max-state",
                        Name = "Maximum processor state",
                        Description = "Specifies the maximum processor performance state (as a percentage)",
                        GroupName = "Processor Power Management",
                        InputType = InputType.NumericRange,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PROCTHROTTLEMAX",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "bc5038f7-23e0-4960-96da-33abaf5935ec",
                                ApplyToACDC = true,
                                Units = "%"
                            }
                        },
                        CustomProperties = Templates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "multimedia-when-sharing-media",
                        Name = "When Sharing Media",
                        Description = "Specifies the power management behavior when sharing media with other devices",
                        GroupName = "Multimedia Settings",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                                SettingGuid = "03680956-93bc-4294-bba6-4e0f09bb717f",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.MediaSharing
                    },

                    new SettingDefinition
                    {
                        Id = "multimedia-video-playback-quality-bias",
                        Name = "Video Playback Quality Bias",
                        Description = "Specifies the bias for video playback quality versus energy savings",
                        GroupName = "Multimedia Settings",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                                SettingGuid = "10778347-1370-4ee0-8bbd-33bdacaade49",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.VideoQualityBias
                    },

                    new SettingDefinition
                    {
                        Id = "multimedia-when-playing-video",
                        Name = "When Playing Video",
                        Description = "Specifies the power management behavior when playing video",
                        GroupName = "Multimedia Settings",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                                SettingGuid = "34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.VideoPlayback
                    },

                    new SettingDefinition
                    {
                        Id = "amd-power-slider-overlay",
                        Name = "AMD Power Slider Overlay",
                        Description = "Specifies the AMD Power Slider overlay settings",
                        GroupName = "AMD Power Slider",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "c763b4ec-0e50-4b6b-9bed-2b92a6ee884e",
                                SettingGuid = "7ec1751b-60ed-4588-afb5-9819d3d77d90",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.AmdPowerSlider
                    },

                    new SettingDefinition
                    {
                        Id = "intel-graphics-power-plan",
                        Name = "Intel Graphics Power Plan",
                        Description = "Specifies the power plan for Intel Graphics",
                        GroupName = "Intel Graphics Power",
                        ValidateExistence = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "44f3beca-a7c0-460e-9df2-bb8b99e0cba6",
                                SettingGuid = "3619c3f2-afb2-4afc-b0e9-e7fef372de36",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.IntelGraphics
                    },

                    new SettingDefinition
                    {
                        Id = "ati-powerplay-setting",
                        Name = "ATI PowerPlay Setting",
                        Description = "Specifies the power management mode for legacy AMD GPUs",
                        GroupName = "ATI PowerPlay",
                        ValidateExistence = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "f693fb01-e858-4f00-b20f-f30e12ac06d6",
                                SettingGuid = "191f65b5-d45c-4a4f-8aae-1ab8bfd980e6",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.AtiPowerPlay
                    },

                    new SettingDefinition
                    {
                        Id = "switchable-graphics-gpu-preference",
                        Name = "GPU Preference",
                        Description = "Specifies which GPU to use in hybrid graphics systems",
                        GroupName = "Switchable Graphics",
                        ValidateExistence = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "e276e160-7cb0-43c6-b20b-73f5dce39954",
                                SettingGuid = "a1662ab2-9d34-4e53-ba8b-2639b9e20857",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.SwitchableGraphics
                    },

                    new SettingDefinition
                    {
                        Id = "internet-explorer-javascript-timer",
                        Name = "JavaScript Timer Frequency",
                        Description = "Specifies the frequency of JavaScript timers",
                        GroupName = "Internet Explorer",
                        ValidateExistence = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "02f815b5-a5cf-4c84-bf20-649d1f75d3d8",
                                SettingGuid = "4c793e7d-a264-42e1-87d3-7a0d2f523ccd",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.JavaScriptTimers
                    },

                    new SettingDefinition
                    {
                        Id = "power-button-action",
                        Name = "Power button action",
                        Description = "Specifies the action that Windows takes when the user presses the power button",
                        GroupName = "Power Buttons and Lid",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BUTTONS",
                                SubgroupGuid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                                SettingGuid = "7648efa3-dd9c-4e3e-b566-50f929386280",
                                ApplyToACDC = true,
                                TargetPowerPlanGuid = "381b4222-f694-41f0-9685-ff5bb260df2e",
                                RequiresPowerPlanCreation = true,
                                SourcePowerPlanForCreation = "e9a42b02-d5df-448d-aa00-03f14749eb61"
                            }
                        },
                        CustomProperties = Templates.PowerButtonActions
                    },

                    new SettingDefinition
                    {
                        Id = "sleep-button-action",
                        Name = "Sleep button action",
                        Description = "Specifies the action that Windows takes when the user presses the sleep button",
                        GroupName = "Power Buttons and Lid",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BUTTONS",
                                SubgroupGuid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                                SettingGuid = "96996bc0-ad50-47ec-923b-6f41874dd9eb",
                                ApplyToACDC = true,
                                TargetPowerPlanGuid = "381b4222-f694-41f0-9685-ff5bb260df2e",
                                RequiresPowerPlanCreation = true,
                                SourcePowerPlanForCreation = "e9a42b02-d5df-448d-aa00-03f14749eb61"
                            }
                        },
                        CustomProperties = Templates.SleepButtonActions
                    },

                    new SettingDefinition
                    {
                        Id = "lid-close-action",
                        Name = "Lid close action",
                        Description = "Specifies the action that Windows takes when the user closes the lid on a laptop",
                        GroupName = "Power Buttons and Lid",
                        RequiresLid = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BUTTONS",
                                SettingGUIDAlias = "LIDACTION",
                                SubgroupGuid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                                SettingGuid = "5ca83367-6e45-459f-a27b-476b1d01c936",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.LidActions
                    },

                    new SettingDefinition
                    {
                        Id = "display-brightness",
                        Name = "Display brightness",
                        Description = "Specifies the brightness level of the display",
                        GroupName = "Display",
                        InputType = InputType.NumericRange,
                        RequiresBrightnessSupport = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_VIDEO",
                                SettingGUIDAlias = "VIDEONORMALLEVEL",
                                SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
                                SettingGuid = "aded5e82-b909-4619-9949-f5d71dac0bcb",
                                ApplyToACDC = true,
                                Units = "%"
                            }
                        },
                        CustomProperties = Templates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "display-dimmed-brightness",
                        Name = "Dimmed display brightness",
                        Description = "Specifies the brightness level when the display is dimmed",
                        GroupName = "Display",
                        InputType = InputType.NumericRange,
                        RequiresBrightnessSupport = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_VIDEO",
                                SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
                                SettingGuid = "f1fbfde2-a960-4165-9f88-50667911ce96",
                                ApplyToACDC = true,
                                Units = "%"
                            }
                        },
                        CustomProperties = Templates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "adaptive-brightness",
                        Name = "Enable adaptive brightness",
                        Description = "Specifies whether Windows automatically adjusts display brightness based on ambient light",
                        GroupName = "Display",
                        InputType = InputType.Selection,
                        RequiresBrightnessSupport = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_VIDEO",
                                SettingGUIDAlias = "ADAPTBRIGHT",
                                SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
                                SettingGuid = "fbd9aa66-9553-4097-ba44-ed6e9d65eab8",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.OnOff
                    },

                    new SettingDefinition
                    {
                        Id = "critical-battery-action",
                        Name = "Critical battery action",
                        Description = "Specifies the action that Windows takes when battery capacity reaches the critical battery level",
                        GroupName = "Battery",
                        RequiresBattery = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BATTERY",
                                SettingGUIDAlias = "BATACTIONCRIT",
                                SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                                SettingGuid = "637ea02f-bbcb-4015-8e2c-a1c7b9c0b546",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.BatteryActions
                    },

                    new SettingDefinition
                    {
                        Id = "low-battery-level",
                        Name = "Low battery level",
                        Description = "Specifies the percentage of battery capacity that Windows considers to be low",
                        GroupName = "Battery",
                        RequiresBattery = true,
                        InputType = InputType.NumericRange,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BATTERY",
                                SettingGUIDAlias = "BATLEVELOW",
                                SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                                SettingGuid = "8183ba9a-e910-48da-8769-14ae6dc1170a",
                                ApplyToACDC = true,
                                Units = "%"
                            }
                        },
                        CustomProperties = Templates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "critical-battery-level",
                        Name = "Critical battery level",
                        Description = "Specifies the percentage of battery capacity that Windows considers to be critical",
                        GroupName = "Battery",
                        RequiresBattery = true,
                        InputType = InputType.NumericRange,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BATTERY",
                                SettingGUIDAlias = "BATLEVELCRIT",
                                SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                                SettingGuid = "9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469",
                                ApplyToACDC = true,
                                Units = "%"
                            }
                        },
                        CustomProperties = Templates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "low-battery-notification",
                        Name = "Low battery notification",
                        Description = "Specifies whether Windows notifies the user when battery capacity reaches the low battery level",
                        GroupName = "Battery",
                        RequiresBattery = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BATTERY",
                                SettingGUIDAlias = "BATFLAGSLOW",
                                SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                                SettingGuid = "bcded951-187b-4d05-bccc-f7e51960c258",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.OnOff
                    },

                    new SettingDefinition
                    {
                        Id = "power-hibernation-enable",
                        Name = "Hibernation",
                        Description = "Enables or disables system hibernation. Note: Disabling hibernation also disables Fast Startup",
                        GroupName = "Sleep",
                        InputType = InputType.Toggle,
                        CommandSettings = new List<CommandSetting>
                        {
                            new CommandSetting
                            {
                                Id = "hibernation-toggle",
                                EnabledCommand = "powercfg /hibernate on",
                                DisabledCommand = "powercfg /hibernate off",
                            }
                        },
                    },

                    new SettingDefinition
                    {
                        Id = "power-hibernate-timeout",
                        Name = "Hibernate after",
                        Description = "Specifies the period of inactivity before Windows hibernates the computer",
                        GroupName = "Sleep",
                        ParentSettingId = "power-hibernation-enable",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_SLEEP",
                                SettingGUIDAlias = "HIBERNATEIDLE",
                                SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                                SettingGuid = "9d7815a6-7ee4-497e-8888-515a05f02364",
                                ApplyToACDC = true
                            }
                        },
                        CustomProperties = Templates.TimeIntervals
                    },

                    new SettingDefinition
                    {
                        Id = "power-hybrid-sleep",
                        Name = "Allow hybrid sleep",
                        Description = "Enables or disables hybrid sleep mode",
                        GroupName = "Sleep",
                        ParentSettingId = "power-hibernation-enable",
                        InputType = InputType.Toggle,
                        Dependencies = new List<SettingDependency>
                        {
                            new SettingDependency
                            {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "power-hybrid-sleep",
                            RequiredSettingId = "power-hibernation-enable",
                            },
                        },
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_SLEEP",
                                SettingGUIDAlias = "HYBRIDSLEEP",
                                SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                                SettingGuid = "94ac6d29-73ce-41a6-809f-6363ba21b47e",
                                ApplyToACDC = true
                            }
                        },
                    },

                    new SettingDefinition
                    {
                        Id = "power-fast-startup",
                        Name = "Fast Startup",
                        Description = "This helps start your PC faster after shutdown. Restart isn't affected.",
                        GroupName = "Hibernate",
                        ParentSettingId = "power-hibernation-enable",
                        InputType = InputType.Toggle,
                        Dependencies = new List<SettingDependency>
                        {
                            new SettingDependency
                            {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "power-fast-startup",
                            RequiredSettingId = "power-hibernation-enable",
                            },
                        },
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Session Manager\Power",
                                ValueName = "HiberbootEnabled",
                                RecommendedValue = 0,
                                EnabledValue = 1,
                                DisabledValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord,
                                AbsenceMeansEnabled = true,
                            },
                        },
                    },

                    new SettingDefinition
                    {
                        Id = "start-power-hibernate-option",
                        Name = "Show Hibernate Option",
                        Description = "Controls visibility of the Hibernate option in the Start Menu power flyout",
                        GroupName = "Start Menu",
                        ParentSettingId = "power-hibernation-enable",
                        InputType = InputType.Toggle,
                        Dependencies = new List<SettingDependency>
                        {
                            new SettingDependency
                            {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "start-power-hibernate-option",
                            RequiredSettingId = "power-hibernation-enable",
                            },
                        },
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings",
                                ValueName = "ShowHibernateOption",
                                RecommendedValue = 0,
                                EnabledValue = 1,
                                DisabledValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord,
                                AbsenceMeansEnabled = true,
                            },
                        },
                    },

                    new SettingDefinition
                    {
                        Id = "start-power-lock-option",
                        Name = "Show Lock Option",
                        Description = "Controls visibility of the Lock option in the Start Menu power flyout",
                        GroupName = "Start Menu",
                        InputType = InputType.Toggle,
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings",
                                ValueName = "ShowLockOption",
                                RecommendedValue = 0,
                                EnabledValue = 1,
                                DisabledValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord,
                                AbsenceMeansEnabled = true,
                            },
                        },
                    },

                    new SettingDefinition
                    {
                        Id = "start-power-sleep-option",
                        Name = "Show Sleep Option",
                        Description = "Controls visibility of the Sleep option in the Start Menu power flyout",
                        GroupName = "Start Menu",
                        InputType = InputType.Toggle,
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings",
                                ValueName = "ShowSleepOption",
                                RecommendedValue = 0,
                                EnabledValue = 1,
                                DisabledValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord,
                                AbsenceMeansEnabled = true,
                            },
                        },
                    },

                    new SettingDefinition
                    {
                        Id = "power-throttling",
                        Name = "Disable Power Throttling",
                        Description = "Automatically reduces CPU performance for background processes to improve battery life and reduce heat generation",
                        GroupName = "Power",
                        InputType = InputType.Toggle,
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                                ValueName = "PowerThrottlingOff",
                                RecommendedValue = 1,
                                EnabledValue = 1,
                                DisabledValue = 0,
                                DefaultValue = 0,
                                ValueType = RegistryValueKind.DWord,
                                AbsenceMeansEnabled = true,
                            },
                        },
                    },

                }
            };
        }

        private static class Templates
        {
            public static readonly Dictionary<string, object> TimeIntervals = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                {
                    "Never", "1 minute", "2 minutes", "3 minutes", "5 minutes", "10 minutes",
                    "15 minutes", "20 minutes", "25 minutes", "30 minutes", "45 minutes",
                    "1 hour", "2 hours", "3 hours", "4 hours", "5 hours"
                },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 60 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 120 },
                    [3] = new Dictionary<string, int?> { ["PowerCfgValue"] = 180 },
                    [4] = new Dictionary<string, int?> { ["PowerCfgValue"] = 300 },
                    [5] = new Dictionary<string, int?> { ["PowerCfgValue"] = 600 },
                    [6] = new Dictionary<string, int?> { ["PowerCfgValue"] = 900 },
                    [7] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1200 },
                    [8] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1500 },
                    [9] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1800 },
                    [10] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2700 },
                    [11] = new Dictionary<string, int?> { ["PowerCfgValue"] = 3600 },
                    [12] = new Dictionary<string, int?> { ["PowerCfgValue"] = 7200 },
                    [13] = new Dictionary<string, int?> { ["PowerCfgValue"] = 10800 },
                    [14] = new Dictionary<string, int?> { ["PowerCfgValue"] = 14400 },
                    [15] = new Dictionary<string, int?> { ["PowerCfgValue"] = 18000 }
                }
            };

            public static readonly Dictionary<string, object> OnOff = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Off", "On" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["Value"] = 0 },
                    [1] = new Dictionary<string, int?> { ["Value"] = 1 }
                }
            };

            public static readonly Dictionary<string, object> OnOffCommand = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Off", "On" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["CommandEnabled"] = 0 },
                    [1] = new Dictionary<string, int?> { ["CommandEnabled"] = 1 }
                }
            };

            public static readonly Dictionary<string, object> EnabledDisabled = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Disabled", "Enabled" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["Value"] = 0 },
                    [1] = new Dictionary<string, int?> { ["Value"] = 1 }
                }
            };

            public static readonly Dictionary<string, object> WakeTimers = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Disable", "Enable", "Important Wake Timers Only" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 }
                }
            };

            public static readonly Dictionary<string, object> PowerButtonActions = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Do nothing", "Sleep", "Hibernate", "Shut down", "Turn off the display" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 },
                    [3] = new Dictionary<string, int?> { ["PowerCfgValue"] = 3 },
                    [4] = new Dictionary<string, int?> { ["PowerCfgValue"] = 4 }
                }
            };

            public static readonly Dictionary<string, object> SleepButtonActions = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Do nothing", "Sleep", "Turn off the display" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 }
                }
            };

            public static readonly Dictionary<string, object> LidActions = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Do nothing", "Sleep", "Hibernate", "Shut down" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 },
                    [3] = new Dictionary<string, int?> { ["PowerCfgValue"] = 3 }
                }
            };

            public static readonly Dictionary<string, object> CoolingPolicy = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Passive", "Active" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 }
                }
            };

            public static readonly Dictionary<string, object> BatteryActions = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Do nothing", "Sleep", "Hibernate", "Shut down" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 },
                    [3] = new Dictionary<string, int?> { ["PowerCfgValue"] = 3 }
                }
            };

            public static readonly Dictionary<string, object> WirelessPower = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Maximum Performance", "Low Power Saving", "Medium Power Saving", "Maximum Power Saving" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 },
                    [3] = new Dictionary<string, int?> { ["PowerCfgValue"] = 3 }
                }
            };

            public static readonly Dictionary<string, object> Slideshow = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Available", "Paused" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 }
                }
            };

            public static readonly Dictionary<string, object> PciExpress = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Off", "Moderate power savings", "Maximum power savings" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 }
                }
            };

            public static readonly Dictionary<string, object> MediaSharing = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Allow the computer to sleep", "Prevent idling to sleep" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 }
                }
            };

            public static readonly Dictionary<string, object> VideoQualityBias = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Video playback power-saving bias", "Video playback performance bias" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 }
                }
            };

            public static readonly Dictionary<string, object> VideoPlayback = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Optimize video quality", "Balanced", "Optimize power savings" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 }
                }
            };

            public static readonly Dictionary<string, object> AmdPowerSlider = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Battery Saver", "Better Battery", "Better Performance", "Best Performance" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 },
                    [3] = new Dictionary<string, int?> { ["PowerCfgValue"] = 3 }
                }
            };

            public static readonly Dictionary<string, object> JavaScriptTimers = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Maximum Power Savings", "Maximum Performance" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 }
                }
            };

            public static readonly Dictionary<string, object> IntelGraphics = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Maximum Battery Life", "Balanced", "Maximum Performance" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 }
                }
            };

            public static readonly Dictionary<string, object> AtiPowerPlay = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Maximum Battery Life", "Balanced", "Maximum Performance" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 }
                }
            };

            public static readonly Dictionary<string, object> SwitchableGraphics = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Maximize Battery Life", "Optimize Power Savings", "Maximize Performance" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 }
                }
            };

            public static readonly Dictionary<string, object> ProcessorBoostMode = new()
            {
                [CustomPropertyKeys.ComboBoxDisplayNames] = new[] { "Disabled", "Enabled", "Aggressive", "Efficient Enabled", "Efficient Aggressive", "Aggressive At Guaranteed", "Efficient Aggressive At Guaranteed" },
                [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                {
                    [0] = new Dictionary<string, int?> { ["PowerCfgValue"] = 0 },
                    [1] = new Dictionary<string, int?> { ["PowerCfgValue"] = 1 },
                    [2] = new Dictionary<string, int?> { ["PowerCfgValue"] = 2 },
                    [3] = new Dictionary<string, int?> { ["PowerCfgValue"] = 3 },
                    [4] = new Dictionary<string, int?> { ["PowerCfgValue"] = 4 },
                    [5] = new Dictionary<string, int?> { ["PowerCfgValue"] = 5 },
                    [6] = new Dictionary<string, int?> { ["PowerCfgValue"] = 6 }
                }
            };

            public static Dictionary<string, object> CreateNumericRange(int minValue, int maxValue, string units)
            {
                return new Dictionary<string, object>
                {
                    ["MinValue"] = minValue,
                    ["MaxValue"] = maxValue,
                    ["Increment"] = 1,
                    ["Units"] = units
                };
            }
        }
    }

    public static class PowerPlanDefinitions
    {
        public static readonly List<PredefinedPowerPlan> BuiltInPowerPlans = new List<PredefinedPowerPlan>
        {
            new("Power Saver", "Delivers reduced performance which may increase power savings.", "a1841308-3541-4fab-bc81-f71556f20b4a"),
            new("Balanced", "Automatically balances performance and power consumption according to demand.", "381b4222-f694-41f0-9685-ff5bb260df2e"),
            new("High Performance", "Delivers maximum performance at the expense of higher power consumption.", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
            new("Ultimate Performance", "Provides ultimate performance on higher end PCs.", "e9a42b02-d5df-448d-aa00-03f14749eb61")
        };
    }
}
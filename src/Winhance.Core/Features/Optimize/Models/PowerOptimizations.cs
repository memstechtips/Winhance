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
                        IconPack = "Fluent",
                        Icon = "NotebookLightning",
                        InputType = InputType.Selection,
                        CustomProperties = new Dictionary<string, object>
                        {
                            ["LoadDynamicOptions"] = true
                        }
                    },

                    // Display
                    new SettingDefinition
                    {
                        Id = "power-display-timeout",
                        Name = "Turn off the display",
                        Description = "Specifies the period of inactivity before Windows turns off the display",
                        GroupName = "Display",
                        Icon = "MonitorOff",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_VIDEO",
                                SettingGUIDAlias = "VIDEOIDLE",
                                SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
                                SettingGuid = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 300
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.TimeIntervals)
                        {
                            ["RecommendedOptionAC"] = "Never",
                            ["RecommendedOptionDC"] = "5 minutes"
                        }
                    },

                    
//                    new SettingDefinition
//                    {
//                        Id = "display-brightness",
//                        Name = "Display brightness",
//                        Description = "Specifies the brightness level of the display",
//                        GroupName = "Display",
//                        InputType = InputType.NumericRange,
//                        RequiresBrightnessSupport = true,
//                        PowerCfgSettings = new List<PowerCfgSetting>
//                        {
//                            new PowerCfgSetting
//                            {
//                                SubgroupGUIDAlias = "SUB_VIDEO",
//                                SettingGUIDAlias = "VIDEONORMALLEVEL",
//                                SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
//                                SettingGuid = "aded5e82-b909-4619-9949-f5d71dac0bcb",
//                                PowerModeSupport = PowerModeSupport.Separate,
//                                Units = "%"
//                            }
//                        },
//                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
//                    },

//                    new SettingDefinition
//                    {
//                        Id = "display-dimmed-brightness",
//                        Name = "Dimmed display brightness",
//                        Description = "Specifies the brightness level when the display is dimmed",
//                        GroupName = "Display",
//                        InputType = InputType.NumericRange,
//                        RequiresBrightnessSupport = true,
//                        PowerCfgSettings = new List<PowerCfgSetting>
//                        {
//                            new PowerCfgSetting
//                            {
//                                SubgroupGUIDAlias = "SUB_VIDEO",
//                                SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
//                                SettingGuid = "f1fbfde2-a960-4165-9f88-50667911ce96",
//                                PowerModeSupport = PowerModeSupport.Separate,
//                                Units = "%"
//                            }
//                        },
//                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
//                    },

//                    new SettingDefinition
//                    {
//                        Id = "adaptive-brightness",
//                        Name = "Enable adaptive brightness",
//                        Description = "Specifies whether Windows automatically adjusts display brightness based on ambient light",
//                        GroupName = "Display",
//                        InputType = InputType.Selection,
//                        RequiresBrightnessSupport = true,
//                        PowerCfgSettings = new List<PowerCfgSetting>
//                        {
//                            new PowerCfgSetting
//                            {
//                                SubgroupGUIDAlias = "SUB_VIDEO",
//                                SettingGUIDAlias = "ADAPTBRIGHT",
//                                SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
//                                SettingGuid = "fbd9aa66-9553-4097-ba44-ed6e9d65eab8",
//                            }
//                        },
//                        CustomProperties = PowerTemplates.OnOff
//                    },

                    // Hard Disk
                    new SettingDefinition
                    {
                        Id = "power-harddisk-timeout",
                        Name = "Turn off hard disk after",
                        Description = "Specifies the period of inactivity before Windows turns off the hard disk",
                        GroupName = "Hard Disk",
                        Icon = "Harddisk",
                        InputType = InputType.NumericRange,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_DISK",
                                SettingGUIDAlias = "DISKIDLE",
                                SubgroupGuid = "0012ee47-9041-4b5d-9b77-535fba8b1442",
                                SettingGuid = "6738e2c4-e8a5-4a42-b16a-e040e769756e",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "Seconds",
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 600
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, int.MaxValue, "Minutes")
                    },

                    // Internet Explorer
                    new SettingDefinition
                    {
                        Id = "internet-explorer-javascript-timer",
                        Name = "JavaScript Timer Frequency",
                        Description = "Specifies the frequency of JavaScript timers",
                        GroupName = "Internet Explorer",
                        Icon = "CodeBraces",
                        ValidateExistence = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "02f815b5-a5cf-4c84-bf20-649d1f75d3d8",
                                SettingGuid = "4c793e7d-a264-42e1-87d3-7a0d2f523ccd",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 0
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.JavaScriptTimers)
                        {
                            ["RecommendedOptionAC"] = "Maximum Performance",
                            ["RecommendedOptionDC"] = "Maximum Performance"
                        }
                    },

                    // Desktop Background Settings
                    new SettingDefinition
                    {
                        Id = "desktop-slideshow",
                        Name = "Desktop Background Slide Show",
                        Description = "Allow or prevent Windows from rotating through multiple wallpaper images",
                        GroupName = "Desktop Background Settings",
                        Icon = "Image",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "0d7dbae2-4294-402a-ba8e-26777e8488cd",
                                SettingGuid = "309dce9b-bef4-4119-9921-a851fb12f0f4",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 1,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.Slideshow)
                        {
                            ["RecommendedOptionAC"] = "Available",
                            ["RecommendedOptionDC"] = "Paused"
                        }
                    },

                    // Wireless Adapter Settings
                    new SettingDefinition
                    {
                        Id = "wireless-power-mode",
                        Name = "Power Saving Mode",
                        Description = "Balance wireless network performance with battery life by adjusting adapter power usage",
                        GroupName = "Wireless Adapter Settings",
                        Icon = "Wifi",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1",
                                SettingGuid = "12bbebe6-58d6-4636-95bb-3217ef867c1a",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 2
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.WirelessPower)
                        {
                            ["RecommendedOptionAC"] = "Maximum Performance",
                            ["RecommendedOptionDC"] = "Medium Power Saving"
                        }
                    },

                    // Sleep
                    new SettingDefinition
                    {
                        Id = "power-sleep-timeout",
                        Name = "Put the computer to sleep",
                        Description = "Specifies the period of inactivity before Windows puts the computer to sleep",
                        GroupName = "Sleep",
                        Icon = "Sleep",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_SLEEP",
                                SettingGUIDAlias = "STANBYIDLE",
                                SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                                SettingGuid = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 900
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.TimeIntervals)
                        {
                            ["RecommendedOptionAC"] = "Never",
                            ["RecommendedOptionDC"] = "15 minutes"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "power-wake-timers",
                        Name = "Allow wake timers",
                        Description = "Allow scheduled tasks and applications to wake your computer from sleep",
                        GroupName = "Sleep",
                        Icon = "Alarm",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_SLEEP",
                                SettingGUIDAlias = "RTCWAKE",
                                SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                                SettingGuid = "bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 0
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.WakeTimers)
                        {
                            ["RecommendedOptionAC"] = "Disable",
                            ["RecommendedOptionDC"] = "Disable"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "power-hibernation-enable",
                        Name = "Hibernation",
                        Description = "Save your session to disk and power down completely, using no battery while preserving your work",
                        GroupName = "Sleep",
                        Icon = "PowerSleep",
                        InputType = InputType.Toggle,
                    },

                    new SettingDefinition
                    {
                        Id = "power-hibernate-timeout",
                        Name = "Hibernate after",
                        Description = "Specifies the period of inactivity before Windows hibernates the computer",
                        GroupName = "Sleep",
                        Icon = "BedClock",
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
                                PowerModeSupport = PowerModeSupport.Separate
                            }
                        },
                        CustomProperties = PowerTemplates.TimeIntervals
                    },

                    new SettingDefinition
                    {
                        Id = "power-hybrid-sleep",
                        Name = "Allow hybrid sleep",
                        Description = "Combines sleep and hibernate by saving your session to disk while staying in low-power mode for faster wake",
                        GroupName = "Sleep",
                        Icon = "WeatherNight",
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
                                PowerModeSupport = PowerModeSupport.Both
                            }
                        },
                    },

                    new SettingDefinition
                    {
                        Id = "power-fast-startup",
                        Name = "Fast Startup",
                        Description = "Hibernate system state during shutdown for faster boot times (does not affect restart)",
                        GroupName = "Sleep",
                        Icon = "FlashAuto",
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
                                EnabledValue = null,
                                DisabledValue = 0,
                                ValueType = RegistryValueKind.DWord,
                            },
                        },
                    },

                    new SettingDefinition
                    {
                        Id = "start-power-hibernate-option",
                        Name = "Show Hibernate Option",
                        Description = "Display the Hibernate option in the Start Menu power button menu",
                        GroupName = "Sleep",
                        Icon = "FlashRedEye",
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
                                EnabledValue = null,
                                DisabledValue = 0,
                                ValueType = RegistryValueKind.DWord,
                            },
                        },
                    },

                    // USB settings
                    new SettingDefinition
                    {
                        Id = "usb-hub-selective-suspend-timeout",
                        Name = "USB Hub Selective Suspend Timeout",
                        Description = "Set how long USB hubs wait idle before powering down to save energy",
                        GroupName = "USB settings",
                        Icon = "TimerPause",
                        InputType = InputType.NumericRange,
                        ValidateExistence = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "2a737441-1930-4402-8d77-b2bebba308a3",
                                SettingGuid = "0853a681-27c8-4100-a2fd-82013e970683",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "Milliseconds",
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 1000,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\0853a681-27c8-4100-a2fd-82013e970683",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100000, "Milliseconds")
                    },
                    new SettingDefinition
                    {
                        Id = "usb-selective-suspend",
                        Name = "USB selective suspend setting",
                        Description = "Allow Windows to power down individual USB ports when devices are idle to save energy",
                        GroupName = "USB settings",
                        Icon = "Usb",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "2a737441-1930-4402-8d77-b2bebba308a3",
                                SettingGuid = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.EnabledDisabled)
                        {
                            ["RecommendedOptionAC"] = "Disabled",
                            ["RecommendedOptionDC"] = "Enabled"
                        }
                    },
                    new SettingDefinition
                    {
                        Id = "usb3-link-power-management",
                        Name = "USB 3 Link Power Management",
                        Description = "Control how aggressively USB 3.0 ports enter low-power states when devices are idle",
                        GroupName = "USB settings",
                        Icon = "UsbPort",
                        InputType = InputType.Selection,
                        ValidateExistence = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "2a737441-1930-4402-8d77-b2bebba308a3",
                                SettingGuid = "d4e98f31-5ffe-4ce1-be31-1b38b384c009",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 2,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\d4e98f31-5ffe-4ce1-be31-1b38b384c009",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.Usb3LinkPower)
                        {
                            ["RecommendedOptionAC"] = "Off",
                            ["RecommendedOptionDC"] = "Moderate power savings"
                        }
                    },

                    // Intel(R) Graphics Settings
                    new SettingDefinition
                    {
                        Id = "intel-graphics-power-plan",
                        Name = "Intel(R) Graphics Power Plan",
                        Description = "Balance Intel integrated graphics performance with power consumption and battery life",
                        GroupName = "Intel(R) Graphics Settings",
                        Icon = "ExpansionCard",
                        ValidateExistence = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "44f3beca-a7c0-460e-9df2-bb8b99e0cba6",
                                SettingGuid = "3619c3f2-afb2-4afc-b0e9-e7fef372de36",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 2,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.IntelGraphics)
                        {
                            ["RecommendedOptionAC"] = "Maximum Performance",
                            ["RecommendedOptionDC"] = "Balanced"
                        }
                    },

                    // Power Buttons and Lid
                    new SettingDefinition
                    {
                        Id = "power-button-action",
                        Name = "Power button action",
                        Description = "Choose what happens when you press the physical power button on your computer",
                        GroupName = "Power Buttons and Lid",
                        Icon = "PowerSettings",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BUTTONS",
                                SubgroupGuid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                                SettingGuid = "7648efa3-dd9c-4e3e-b566-50f929386280",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 0,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\4f971e89-eebd-4455-a8de-9e59040e7347\7648efa3-dd9c-4e3e-b566-50f929386280",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.PowerButtonActions)
                        {
                            ["RecommendedOptionAC"] = "Do nothing",
                            ["RecommendedOptionDC"] = "Do nothing"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "sleep-button-action",
                        Name = "Sleep button action",
                        Description = "Choose what happens when you press the dedicated sleep button on your keyboard or computer",
                        GroupName = "Power Buttons and Lid",
                        Icon = "Sleep",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BUTTONS",
                                SubgroupGuid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                                SettingGuid = "96996bc0-ad50-47ec-923b-6f41874dd9eb",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 0,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\4f971e89-eebd-4455-a8de-9e59040e7347\96996bc0-ad50-47ec-923b-6f41874dd9eb",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.PowerButtonActions)
                        {
                            ["RecommendedOptionAC"] = "Do nothing",
                            ["RecommendedOptionDC"] = "Do nothing"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "lid-close-action",
                        Name = "Lid close action",
                        Description = "Choose what happens when you close your laptop lid",
                        GroupName = "Power Buttons and Lid",
                        Icon = "Laptop",
                        RequiresBattery = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BUTTONS",
                                SettingGUIDAlias = "LIDACTION",
                                SubgroupGuid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                                SettingGuid = "5ca83367-6e45-459f-a27b-476b1d01c936",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 1,
                                RecommendedValueDC = 1,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\4f971e89-eebd-4455-a8de-9e59040e7347\5ca83367-6e45-459f-a27b-476b1d01c936",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.LidActions)
                        {
                            ["RecommendedOptionAC"] = "Sleep",
                            ["RecommendedOptionDC"] = "Sleep"
                        }
                    },

                    // PCI Express
                    new SettingDefinition
                    {
                        Id = "pci-link-state-power-management",
                        Name = "Link State Power Management",
                        Description = "Control power savings for PCIe devices like graphics cards, SSDs, and expansion cards",
                        GroupName = "PCI Express",
                        Icon = "Router",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PCIEXPRESS",
                                SettingGUIDAlias = "ASPM",
                                SubgroupGuid = "501a4d13-42af-4429-9fd1-a8218c268e20",
                                SettingGuid = "ee12f906-d277-404b-b6da-e5fa1a576df5",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 2
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.PciExpress)
                        {
                            ["RecommendedOptionAC"] = "Off",
                            ["RecommendedOptionDC"] = "Maximum power savings"
                        }
                    },

                    // Processor Power Management
                    new SettingDefinition
                    {
                        Id = "processor-min-state",
                        Name = "Minimum processor state",
                        Description = "Set the lowest CPU speed allowed as a percentage of maximum frequency",
                        GroupName = "Processor Power Management",
                        Icon = "SpeedometerSlow",
                        InputType = InputType.NumericRange,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PROCTHROTTLEMIN",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "893dee8e-2bef-41e0-89c6-b55d0929964c",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 100,
                                RecommendedValueDC = 5
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "processor-max-state",
                        Name = "Maximum processor state",
                        Description = "Set the highest CPU speed allowed as a percentage of maximum frequency",
                        GroupName = "Processor Power Management",
                        Icon = "Speedometer",
                        InputType = InputType.NumericRange,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PROCTHROTTLEMAX",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "bc5038f7-23e0-4960-96da-33abaf5935ec",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 100,
                                RecommendedValueDC = 100
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "system-cooling-policy",
                        Name = "System cooling policy",
                        Description = "Choose whether to slow down the processor first (passive) or speed up fans first (active) when hot",
                        GroupName = "Processor Power Management",
                        Icon = "Fan",
                        InputType = InputType.Selection,
                        ValidateExistence = true,
                        RequiresAdvancedUnlock = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "94d3a615-a899-4ac5-ae2b-e4d8f634367f",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 1,
                                RecommendedValueDC = 1,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\94d3a615-a899-4ac5-ae2b-e4d8f634367f",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.CoolingPolicy)
                        {
                            ["RecommendedOptionAC"] = "Active",
                            ["RecommendedOptionDC"] = "Active"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "processor-performance-boost-mode",
                        Name = "Processor performance boost mode",
                        Description = "Control how aggressively your CPU boosts above base frequency for demanding tasks",
                        GroupName = "Processor Power Management",
                        Icon = "RocketLaunch",
                        InputType = InputType.Selection,
                        ValidateExistence = true,
                        RequiresAdvancedUnlock = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PERFBOOSTMODE",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "be337238-0d82-4146-a960-4f3749d470c7",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 2,
                                RecommendedValueDC = 1,
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
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.ProcessorBoostMode)
                        {
                            ["RecommendedOptionAC"] = "Aggressive",
                            ["RecommendedOptionDC"] = "Enabled"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "processor-performance-increase-policy",
                        Name = "Processor Performance Increase Policy",
                        Description = "Control how quickly CPU ramps up speed when workload increases (for legacy non-HWP processors)",
                        GroupName = "Processor Power Management",
                        Icon = "TrendingUp",
                        InputType = InputType.Selection,
                        ValidateExistence = true,
                        RequiresAdvancedUnlock = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PERFINCPOL",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "465e1f50-b610-473a-ab58-00d1077dc418",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 2,
                                RecommendedValueDC = 0,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\465e1f50-b610-473a-ab58-00d1077dc418",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.PerformanceIncreasePolicy)
                        {
                            ["RecommendedOptionAC"] = "Rocket",
                            ["RecommendedOptionDC"] = "Ideal"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "processor-performance-decrease-policy",
                        Name = "Processor Performance Decrease Policy",
                        Description = "Control how quickly CPU reduces speed when workload decreases (for legacy non-HWP processors)",
                        GroupName = "Processor Power Management",
                        Icon = "TrendingDown",
                        InputType = InputType.Selection,
                        ValidateExistence = true,
                        RequiresAdvancedUnlock = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PERFDECPOL",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "40fbefc7-2e9d-4d25-a185-0cfd8574bac6",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 1,
                                RecommendedValueDC = 2,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\40fbefc7-2e9d-4d25-a185-0cfd8574bac6",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.PerformanceDecreasePolicy)
                        {
                            ["RecommendedOptionAC"] = "Single",
                            ["RecommendedOptionDC"] = "Rocket"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "processor-core-parking-min-cores",
                        Name = "CPU Core Parking Minimum Cores",
                        Description = "Set the minimum percentage of CPU cores that must remain active and responsive",
                        GroupName = "Processor Power Management",
                        Icon = "Cpu64Bit",
                        InputType = InputType.NumericRange,
                        ValidateExistence = true,
                        RequiresAdvancedUnlock = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "CPMINCORES",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "0cc5b647-c1df-4637-891a-dec35c318583",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 0,
                                CheckForHardwareControl = true,
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
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "processor-core-parking-max-cores",
                        Name = "CPU Core Parking Maximum Cores",
                        Description = "Set the maximum percentage of CPU cores allowed to be active (100% for best performance)",
                        GroupName = "Processor Power Management",
                        Icon = "Cpu64Bit",
                        InputType = InputType.NumericRange,
                        ValidateExistence = true,
                        RequiresAdvancedUnlock = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "CPMAXCORES",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "ea062031-0e34-4ff1-9b6d-eb1059334028",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 100,
                                RecommendedValueDC = 100,
                                CheckForHardwareControl = true,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\ea062031-0e34-4ff1-9b6d-eb1059334028",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "processor-energy-performance-preference",
                        Name = "Processor Energy Performance Preference",
                        Description = "Balance power efficiency and performance for modern CPUs with HWP (0 = max performance, 100 = max efficiency)",
                        GroupName = "Processor Power Management",
                        Icon = "Tune",
                        InputType = InputType.NumericRange,
                        ValidateExistence = true,
                        RequiresAdvancedUnlock = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PERFEPP",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "36687f9e-e3a5-4dbf-b1dc-15eb381c6863",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 50,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\36687f9e-e3a5-4dbf-b1dc-15eb381c6863",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "processor-performance-increase-threshold",
                        Name = "Processor Performance Increase Threshold",
                        Description = "Set CPU usage percentage that triggers speed increase (lower = more responsive, for legacy non-HWP CPUs)",
                        GroupName = "Processor Power Management",
                        Icon = "TrendingUp",
                        InputType = InputType.NumericRange,
                        ValidateExistence = true,
                        RequiresAdvancedUnlock = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PERFINCTHRESHOLD",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "06cadf0e-64ed-448a-8927-ce7bf90eb35d",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 10,
                                RecommendedValueDC = 30,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\06cadf0e-64ed-448a-8927-ce7bf90eb35d",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "processor-performance-decrease-threshold",
                        Name = "Processor Performance Decrease Threshold",
                        Description = "Set CPU usage percentage that triggers speed reduction (lower = maintains performance longer, for legacy non-HWP CPUs)",
                        GroupName = "Processor Power Management",
                        Icon = "TrendingDown",
                        InputType = InputType.NumericRange,
                        ValidateExistence = true,
                        RequiresAdvancedUnlock = true,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_PROCESSOR",
                                SettingGUIDAlias = "PERFDECTHRESHOLD",
                                SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                                SettingGuid = "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 8,
                                RecommendedValueDC = 20,
                                EnablementRegistrySetting = new RegistrySetting
                                {
                                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\12a0ab44-fe28-4fa9-b3bd-4b64f44960a6",
                                    ValueName = "Attributes",
                                    EnabledValue = 0,
                                    DisabledValue = 1,
                                    DefaultValue = 1,
                                    ValueType = RegistryValueKind.DWord
                                }
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "power-throttling",
                        Name = "Disable Power Throttling",
                        Description = "Automatically reduces CPU performance for background processes to improve battery life and reduce heat generation",
                        GroupName = "Processor Power Management",
                        Icon = "SelectOff",
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
                            },
                        },
                    },

                    // Multimedia Settings
                    new SettingDefinition
                    {
                        Id = "multimedia-when-sharing-media",
                        Name = "When Sharing Media",
                        Description = "Control whether your PC can sleep while streaming media to other devices on your network",
                        GroupName = "Multimedia Settings",
                        Icon = "Share",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                                SettingGuid = "03680956-93bc-4294-bba6-4e0f09bb717f",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 1,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.MediaSharing)
                        {
                            ["RecommendedOptionAC"] = "Prevent idling to sleep",
                            ["RecommendedOptionDC"] = "Prevent idling to sleep"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "multimedia-video-playback-quality-bias",
                        Name = "Video Playback Quality Bias",
                        Description = "Prioritize smooth video playback over battery life when watching videos",
                        GroupName = "Multimedia Settings",
                        Icon = "HighDefinition",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                                SettingGuid = "10778347-1370-4ee0-8bbd-33bdacaade49",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 1,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.VideoQualityBias)
                        {
                            ["RecommendedOptionAC"] = "Video playback performance bias",
                            ["RecommendedOptionDC"] = "Video playback performance bias"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "multimedia-when-playing-video",
                        Name = "When Playing Video",
                        Description = "Balance video quality and power consumption during video playback",
                        GroupName = "Multimedia Settings",
                        Icon = "Play",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                                SettingGuid = "34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 0
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.VideoPlayback)
                        {
                            ["RecommendedOptionAC"] = "Optimize video quality",
                            ["RecommendedOptionDC"] = "Optimize video quality"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "critical-battery-notification",
                        Name = "Critical battery notification",
                        Description = "Show notification when battery reaches critically low level",
                        GroupName = "Battery",
                        Icon = "AlertCircle",
                        RequiresBattery = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BATTERY",
                                SettingGUIDAlias = "BATFLAGSCRIT",
                                SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                                SettingGuid = "5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 1,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.OnOff)
                        {
                            ["RecommendedOptionAC"] = "On",
                            ["RecommendedOptionDC"] = "On"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "critical-battery-action",
                        Name = "Critical battery action",
                        Description = "Choose what happens when battery reaches critically low level",
                        GroupName = "Battery",
                        Icon = "BatteryAlert",
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
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 2,
                                RecommendedValueDC = 2
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.BatteryActions)
                        {
                            ["RecommendedOptionAC"] = "Hibernate",
                            ["RecommendedOptionDC"] = "Hibernate"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "low-battery-level",
                        Name = "Low battery level",
                        Description = "Set the battery percentage that triggers low battery warnings and actions",
                        GroupName = "Battery",
                        Icon = "Battery20",
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
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 10,
                                RecommendedValueDC = 10
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "critical-battery-level",
                        Name = "Critical battery level",
                        Description = "Set the battery percentage that triggers critical battery warnings and emergency actions",
                        GroupName = "Battery",
                        Icon = "BatteryOutline",
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
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 5,
                                RecommendedValueDC = 5
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "low-battery-notification",
                        Name = "Low battery notification",
                        Description = "Show notification when battery reaches low battery level",
                        GroupName = "Battery",
                        Icon = "Bell",
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
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 1,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.OnOff)
                        {
                            ["RecommendedOptionAC"] = "On",
                            ["RecommendedOptionDC"] = "On"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "low-battery-action",
                        Name = "Low battery action",
                        Description = "Choose what happens when battery reaches low battery level",
                        GroupName = "Battery",
                        Icon = "Battery20",
                        RequiresBattery = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BATTERY",
                                SettingGUIDAlias = "BATACTIONLOW",
                                SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                                SettingGuid = "d8742dcb-3e6a-4b3c-b3fe-374623cdcf06",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 0,
                                RecommendedValueDC = 0
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.BatteryActions)
                        {
                            ["RecommendedOptionAC"] = "Do nothing",
                            ["RecommendedOptionDC"] = "Do nothing"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "reserve-battery-level",
                        Name = "Reserve battery level",
                        Description = "Set battery percentage reserved to protect battery health and prevent unexpected shutdowns",
                        GroupName = "Battery",
                        Icon = "BatteryCharging",
                        RequiresBattery = true,
                        InputType = InputType.NumericRange,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGUIDAlias = "SUB_BATTERY",
                                SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                                SettingGuid = "f3c5027d-cd16-4930-aa6b-90db844a8f00",
                                PowerModeSupport = PowerModeSupport.Separate,
                                Units = "%",
                                RecommendedValueAC = 7,
                                RecommendedValueDC = 7
                            }
                        },
                        CustomProperties = PowerTemplates.CreateNumericRange(0, 100, "%")
                    },

                    new SettingDefinition
                    {
                        Id = "amd-power-slider-overlay",
                        Name = "Overlay",
                        Description = "Balance AMD laptop performance and battery life with quick power mode selection",
                        GroupName = "AMD Power Slider",
                        Icon = "ExpansionCard",
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "c763b4ec-0e50-4b6b-9bed-2b92a6ee884e",
                                SettingGuid = "7ec1751b-60ed-4588-afb5-9819d3d77d90",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 3,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.AmdPowerSlider)
                        {
                            ["RecommendedOptionAC"] = "Best Performance",
                            ["RecommendedOptionDC"] = "Better Battery"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "ati-powerplay-setting",
                        Name = "ATI PowerPlay Setting",
                        Description = "Control power management for older AMD Radeon graphics cards",
                        GroupName = "ATI PowerPlay",
                        Icon = "ExpansionCard",
                        ValidateExistence = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "f693fb01-e858-4f00-b20f-f30e12ac06d6",
                                SettingGuid = "191f65b5-d45c-4a4f-8aae-1ab8bfd980e6",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 2,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.AtiPowerPlay)
                        {
                            ["RecommendedOptionAC"] = "Maximum Performance",
                            ["RecommendedOptionDC"] = "Balanced"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "switchable-graphics-gpu-preference",
                        Name = "GPU Preference",
                        Description = "Choose between integrated GPU for battery life or dedicated GPU for performance in hybrid graphics laptops",
                        GroupName = "Switchable Graphics",
                        Icon = "SwapHorizontal",
                        ValidateExistence = true,
                        InputType = InputType.Selection,
                        PowerCfgSettings = new List<PowerCfgSetting>
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "e276e160-7cb0-43c6-b20b-73f5dce39954",
                                SettingGuid = "a1662ab2-9d34-4e53-ba8b-2639b9e20857",
                                PowerModeSupport = PowerModeSupport.Separate,
                                RecommendedValueAC = 2,
                                RecommendedValueDC = 1
                            }
                        },
                        CustomProperties = new Dictionary<string, object>(PowerTemplates.SwitchableGraphics)
                        {
                            ["RecommendedOptionAC"] = "Maximize Performance",
                            ["RecommendedOptionDC"] = "Optimize Power Savings"
                        }
                    },

                    new SettingDefinition
                    {
                        Id = "start-power-lock-option",
                        Name = "Show Lock Option",
                        Description = "Display the Lock option in the Start Menu power button menu",
                        GroupName = "Start Menu",
                        Icon = "EyeLock",
                        InputType = InputType.Toggle,
                        Dependencies = new List<SettingDependency>
                        {
                            new SettingDependency
                            {
                                DependencyType = SettingDependencyType.RequiresEnabled,
                                DependentSettingId = "start-power-lock-option",
                                RequiredSettingId = "privacy-lock-screen",
                                RequiredModule = "PrivacyOptimizations",
                            },
                        },
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings",
                                ValueName = "ShowLockOption",
                                RecommendedValue = 0,
                                EnabledValue = null,
                                DisabledValue = 0,
                                ValueType = RegistryValueKind.DWord,
                            },
                        },
                    },

                    new SettingDefinition
                    {
                        Id = "start-power-sleep-option",
                        Name = "Show Sleep Option",
                        Description = "Display the Sleep option in the Start Menu power button menu",
                        GroupName = "Start Menu",
                        Icon = "LightbulbNight",
                        InputType = InputType.Toggle,
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings",
                                ValueName = "ShowSleepOption",
                                RecommendedValue = 0,
                                EnabledValue = null,
                                DisabledValue = 0,
                                ValueType = RegistryValueKind.DWord,
                            },
                        },
                    },

                }
            };
        }
    }

    public static class PowerPlanDefinitions
    {
        public static readonly List<PredefinedPowerPlan> BuiltInPowerPlans = new List<PredefinedPowerPlan>
        {
            new("Power saver", "Saves energy by reducing computer performance", "PowerPlan_PowerSaver_Name", "a1841308-3541-4fab-bc81-f71556f20b4a"),
            new("Balanced", "Balances performance with energy consumption", "PowerPlan_Balanced_Name", "381b4222-f694-41f0-9685-ff5bb260df2e"),
            new("High performance", "Favors performance over energy consumption", "PowerPlan_HighPerformance_Name", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
            new("Ultimate Performance", "Maximum performance with no power saving measures", "PowerPlan_UltimatePerformance_Name", "e9a42b02-d5df-448d-aa00-03f14749eb61"),
            new("Winhance Power Plan", "Optimized power plan for gaming and performance", "PowerPlan_WinhancePowerPlan_Name", "57696e68-616e-6365-506f-776572000000")
        };
    }
}
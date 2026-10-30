using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Optimize.Catalogs;

public static class PowerOptimizationsCatalog
{
    public const string FeatureId = FeatureIds.Power;
    public const string FeatureName = "Power";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "power-plan-selection",
            Display = new()
            {
                Name = LocKey.Setting.PowerPlanSelection.Name,
                Description = LocKey.Setting.PowerPlanSelection.Description,
                Icon = FluentIcons.NotebookLightning,
                IsSubjectivePreference = true,
            },
            Detector = new PowerPlanDetector(),
            Options = new(OptionSource.PowerPlans),
        },
        new()
        {
            Id = "power-display-timeout",
            Display = new()
            {
                Name = LocKey.Setting.PowerDisplayTimeout.Name,
                Description = LocKey.Setting.PowerDisplayTimeout.Description,
                GroupName = LocKey.SettingGroup.Display,
                Icon = MaterialIcons.MonitorOff,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "7516b95f-f776-4464-8c53-06167f40cc99", "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.TimeIntervals, 900, 300, 600, 300),
        },
        new()
        {
            Id = "power-harddisk-timeout",
            Display = new()
            {
                Name = LocKey.Setting.PowerHarddiskTimeout.Name,
                Description = LocKey.Setting.PowerHarddiskTimeout.Description,
                GroupName = LocKey.SettingGroup.HardDisk,
                Icon = MaterialIcons.Harddisk,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "0012ee47-9041-4b5d-9b77-535fba8b1442", "6738e2c4-e8a5-4a42-b16a-e040e769756e", PowerModeSupport.Separate) { Units = "Seconds" },
            },
            Numeric = new()
            {
                Min = 0,
                Max = int.MaxValue,
                Units = "Minutes",
                Recommended = new[] { new ContextValue(PowerContext.AC, 0), new ContextValue(PowerContext.DC, 10) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 20), new ContextValue(PowerContext.DC, 10) },
            },
        },
        new()
        {
            Id = "internet-explorer-javascript-timer",
            Display = new()
            {
                Name = LocKey.Setting.InternetExplorerJavascriptTimer.Name,
                Description = LocKey.Setting.InternetExplorerJavascriptTimer.Description,
                GroupName = LocKey.SettingGroup.InternetExplorer,
                Icon = MaterialIcons.CodeBraces,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "02f815b5-a5cf-4c84-bf20-649d1f75d3d8", "4c793e7d-a264-42e1-87d3-7a0d2f523ccd", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.JavaScriptTimers, 1, 0, 1, 0),
        },
        new()
        {
            Id = "desktop-slideshow",
            Display = new()
            {
                Name = LocKey.Setting.DesktopSlideshow.Name,
                Description = LocKey.Setting.DesktopSlideshow.Description,
                GroupName = LocKey.SettingGroup.DesktopBackgroundSettings,
                Icon = MaterialIcons.Image,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "0d7dbae2-4294-402a-ba8e-26777e8488cd", "309dce9b-bef4-4119-9921-a851fb12f0f4", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.Slideshow, 0, 1, 0, 1),
        },
        new()
        {
            Id = "wireless-power-mode",
            Display = new()
            {
                Name = LocKey.Setting.WirelessPowerMode.Name,
                Description = LocKey.Setting.WirelessPowerMode.Description,
                GroupName = LocKey.SettingGroup.WirelessAdapterSettings,
                Icon = MaterialIcons.Wifi,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1", "12bbebe6-58d6-4636-95bb-3217ef867c1a", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.WirelessPower, 0, 2, 0, 2),
        },
        new()
        {
            Id = "power-sleep-timeout",
            Display = new()
            {
                Name = LocKey.Setting.PowerSleepTimeout.Name,
                Description = LocKey.Setting.PowerSleepTimeout.Description,
                GroupName = LocKey.SettingGroup.Sleep,
                Icon = MaterialIcons.Sleep,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "238c9fa8-0aad-41ed-83f4-97be242c8f20", "29f6c1db-86da-48c5-9fdb-f2b67b1f44da", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.TimeIntervals, 0, 900, 1800, 900),
        },
        new()
        {
            Id = "power-wake-timers",
            Display = new()
            {
                Name = LocKey.Setting.PowerWakeTimers.Name,
                Description = LocKey.Setting.PowerWakeTimers.Description,
                GroupName = LocKey.SettingGroup.Sleep,
                Icon = MaterialIcons.Alarm,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "238c9fa8-0aad-41ed-83f4-97be242c8f20", "bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.WakeTimers, 1, 0, 1, 0),
        },
        new()
        {
            Id = "power-hibernation-enable",
            Display = new()
            {
                Name = LocKey.Setting.PowerHibernationEnable.Name,
                Description = LocKey.Setting.PowerHibernationEnable.Description,
                GroupName = LocKey.SettingGroup.Sleep,
                Icon = MaterialIcons.PowerSleep,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                // Not ReadOnly: a ReadOnly target beside the states' scripts would read as answer-file-only and hide the card.
                new RegTarget("HibernateEnabled", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power" }, "HibernateEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    // Turning hibernation ON is what makes the Start-menu Hibernate entry worth showing, so
                    // it force-shows it. Nothing validates a Link's direction, so keep this on the ENABLED state.
                    Links = new[]
                    {
                        new Link("start-power-hibernate-option", LinkKind.Enables, LocKey.Common.Enabled) { ReverseCascade = false, Force = true },
                    },
                    Set = new Dictionary<string, StateValue> { ["HibernateEnabled"] = StateValue.Of(1).OrAbsent() },
                    Effects = new Effect[] { new ScriptEffect("powercfg /hibernate on", RunContext.System) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["HibernateEnabled"] = StateValue.Of(0) },
                    IsFallback = true,
                    Effects = new Effect[] { new ScriptEffect("powercfg /hibernate off", RunContext.System) },
                },
            },
        },
        new()
        {
            Id = "power-hibernate-timeout",
            Display = new()
            {
                Name = LocKey.Setting.PowerHibernateTimeout.Name,
                Description = LocKey.Setting.PowerHibernateTimeout.Description,
                GroupName = LocKey.SettingGroup.Sleep,
                Icon = MaterialIcons.BedClock,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            UiParentId = "power-hibernation-enable",
            EnabledWhen = new("power-hibernation-enable", new[] { LocKey.Common.Enabled }),
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "238c9fa8-0aad-41ed-83f4-97be242c8f20", "9d7815a6-7ee4-497e-8888-515a05f02364", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.TimeIntervals, 0, 0, 0, 0),
        },
        new()
        {
            Id = "power-hybrid-sleep",
            Display = new()
            {
                Name = LocKey.Setting.PowerHybridSleep.Name,
                Description = LocKey.Setting.PowerHybridSleep.Description,
                GroupName = LocKey.SettingGroup.Sleep,
                Icon = MaterialIcons.WeatherNight,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                Hardware = new[] { HardwareRequirement.HybridSleepCapable },
                ValidatesExistence = true,
            },
            UiParentId = "power-hibernation-enable",
            EnabledWhen = new("power-hibernation-enable", new[] { LocKey.Common.Enabled }),
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "238c9fa8-0aad-41ed-83f4-97be242c8f20", "94ac6d29-73ce-41a6-809f-6363ba21b47e", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.OnOff, 1, 1, 1, 1, new[]
            {
                new Link("power-hibernation-enable", LinkKind.Requires, LocKey.Common.Enabled),
            }),
        },
        new()
        {
            Id = "power-fast-startup",
            Display = new()
            {
                Name = LocKey.Setting.PowerFastStartup.Name,
                Description = LocKey.Setting.PowerFastStartup.Description,
                GroupName = LocKey.SettingGroup.Sleep,
                Icon = MaterialIcons.FlashAuto,
            },
            UiParentId = "power-hibernation-enable",
            EnabledWhen = new("power-hibernation-enable", new[] { LocKey.Common.Enabled }),
            Targets = new Target[]
            {
                new RegTarget("HiberbootEnabled", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Session Manager\Power" }, "HiberbootEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HiberbootEnabled"] = StateValue.Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    Links = new[]
                    {
                        new Link("power-hibernation-enable", LinkKind.Requires, LocKey.Common.Enabled),
                    },
                    Set = new Dictionary<string, StateValue> { ["HiberbootEnabled"] = StateValue.Of(0) },
                    IsFallback = true,
                },
            },
        },
        new()
        {
            Id = "start-power-hibernate-option",
            Display = new()
            {
                Name = LocKey.Setting.StartPowerHibernateOption.Name,
                Description = LocKey.Setting.StartPowerHibernateOption.Description,
                GroupName = LocKey.SettingGroup.Sleep,
                Icon = MaterialIcons.FlashRedEye,
                IsSubjectivePreference = true,
            },
            UiParentId = "power-hibernation-enable",
            EnabledWhen = new("power-hibernation-enable", new[] { LocKey.Common.Enabled }),
            Targets = new Target[]
            {
                new RegTarget("ShowHibernateOption", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings" }, "ShowHibernateOption", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["ShowHibernateOption"] = StateValue.Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Links = new[]
                    {
                        new Link("power-hibernation-enable", LinkKind.Requires, LocKey.Common.Enabled),
                    },
                    Set = new Dictionary<string, StateValue> { ["ShowHibernateOption"] = StateValue.Of(0).OrAbsent() },
                    IsFallback = true,
                },
            },
        },
        new()
        {
            Id = "usb-hub-selective-suspend-timeout",
            Display = new()
            {
                Name = LocKey.Setting.UsbHubSelectiveSuspendTimeout.Name,
                Description = LocKey.Setting.UsbHubSelectiveSuspendTimeout.Description,
                GroupName = LocKey.SettingGroup.USBsettings,
                Icon = MaterialIcons.TimerPause,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "2a737441-1930-4402-8d77-b2bebba308a3", "0853a681-27c8-4100-a2fd-82013e970683", PowerModeSupport.Separate) { Units = "Milliseconds", EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\0853a681-27c8-4100-a2fd-82013e970683" }, "Attributes", RegistryValueKind.DWord) },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100000,
                Units = "Milliseconds",
                Recommended = new[] { new ContextValue(PowerContext.AC, 50), new ContextValue(PowerContext.DC, 50) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 50), new ContextValue(PowerContext.DC, 50) },
            },
        },
        new()
        {
            Id = "usb-selective-suspend",
            Display = new()
            {
                Name = LocKey.Setting.UsbSelectiveSuspend.Name,
                Description = LocKey.Setting.UsbSelectiveSuspend.Description,
                GroupName = LocKey.SettingGroup.USBsettings,
                Icon = MaterialIcons.Usb,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "2a737441-1930-4402-8d77-b2bebba308a3", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.EnabledDisabled, 0, 1, 1, 1),
        },
        new()
        {
            Id = "usb3-link-power-management",
            Display = new()
            {
                Name = LocKey.Setting.Usb3LinkPowerManagement.Name,
                Description = LocKey.Setting.Usb3LinkPowerManagement.Description,
                GroupName = LocKey.SettingGroup.USBsettings,
                Icon = MaterialIcons.UsbPort,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "2a737441-1930-4402-8d77-b2bebba308a3", "d4e98f31-5ffe-4ce1-be31-1b38b384c009", PowerModeSupport.Separate) { EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\d4e98f31-5ffe-4ce1-be31-1b38b384c009" }, "Attributes", RegistryValueKind.DWord) },
            },
            States = PowerOptions.SelectionStates(PowerOptions.Usb3LinkPower, 0, 2, 2, 2),
        },
        new()
        {
            Id = "intel-graphics-power-plan",
            Display = new()
            {
                Name = LocKey.Setting.IntelGraphicsPowerPlan.Name,
                Description = LocKey.Setting.IntelGraphicsPowerPlan.Description,
                GroupName = LocKey.SettingGroup.IntelRGraphicsSettings,
                Icon = MaterialIcons.ExpansionCard,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "44f3beca-a7c0-460e-9df2-bb8b99e0cba6", "3619c3f2-afb2-4afc-b0e9-e7fef372de36", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.IntelGraphics, 2, 1, 1, 1),
        },
        new()
        {
            Id = "power-button-action",
            Display = new()
            {
                Name = LocKey.Setting.PowerButtonAction.Name,
                Description = LocKey.Setting.PowerButtonAction.Description,
                GroupName = LocKey.SettingGroup.PowerButtonsandLid,
                Icon = MaterialIcons.PowerSettings,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "4f971e89-eebd-4455-a8de-9e59040e7347", "7648efa3-dd9c-4e3e-b566-50f929386280", PowerModeSupport.Separate) { EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\4f971e89-eebd-4455-a8de-9e59040e7347\7648efa3-dd9c-4e3e-b566-50f929386280" }, "Attributes", RegistryValueKind.DWord) },
            },
            States = PowerOptions.SelectionStates(PowerOptions.PowerButtonActions, 3, 3, 3, 3),
        },
        new()
        {
            Id = "sleep-button-action",
            Display = new()
            {
                Name = LocKey.Setting.SleepButtonAction.Name,
                Description = LocKey.Setting.SleepButtonAction.Description,
                GroupName = LocKey.SettingGroup.PowerButtonsandLid,
                Icon = MaterialIcons.Sleep,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "4f971e89-eebd-4455-a8de-9e59040e7347", "96996bc0-ad50-47ec-923b-6f41874dd9eb", PowerModeSupport.Separate) { EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\4f971e89-eebd-4455-a8de-9e59040e7347\96996bc0-ad50-47ec-923b-6f41874dd9eb" }, "Attributes", RegistryValueKind.DWord) },
            },
            States = PowerOptions.SelectionStates(PowerOptions.PowerButtonActions, 1, 1, 1, 1),
        },
        new()
        {
            Id = "lid-close-action",
            Display = new()
            {
                Name = LocKey.Setting.LidCloseAction.Name,
                Description = LocKey.Setting.LidCloseAction.Description,
                GroupName = LocKey.SettingGroup.PowerButtonsandLid,
                Icon = MaterialIcons.Laptop,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                Hardware = new[] { HardwareRequirement.Battery },
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "4f971e89-eebd-4455-a8de-9e59040e7347", "5ca83367-6e45-459f-a27b-476b1d01c936", PowerModeSupport.Separate) { EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\4f971e89-eebd-4455-a8de-9e59040e7347\5ca83367-6e45-459f-a27b-476b1d01c936" }, "Attributes", RegistryValueKind.DWord) },
            },
            States = PowerOptions.SelectionStates(PowerOptions.LidActions, 1, 1, 1, 1),
        },
        new()
        {
            Id = "pci-link-state-power-management",
            Display = new()
            {
                Name = LocKey.Setting.PciLinkStatePowerManagement.Name,
                Description = LocKey.Setting.PciLinkStatePowerManagement.Description,
                GroupName = LocKey.SettingGroup.PCIExpress,
                Icon = MaterialIcons.Router,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "501a4d13-42af-4429-9fd1-a8218c268e20", "ee12f906-d277-404b-b6da-e5fa1a576df5", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.PciExpress, 0, 2, 1, 2),
        },
        new()
        {
            Id = "processor-min-state",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorMinState.Name,
                Description = LocKey.Setting.ProcessorMinState.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.SpeedometerSlow,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "893dee8e-2bef-41e0-89c6-b55d0929964c", PowerModeSupport.Separate) { Units = "%" },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 100), new ContextValue(PowerContext.DC, 5) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 5), new ContextValue(PowerContext.DC, 5) },
            },
        },
        new()
        {
            Id = "processor-max-state",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorMaxState.Name,
                Description = LocKey.Setting.ProcessorMaxState.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.Speedometer,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "bc5038f7-23e0-4960-96da-33abaf5935ec", PowerModeSupport.Separate) { Units = "%" },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 100), new ContextValue(PowerContext.DC, 100) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 100), new ContextValue(PowerContext.DC, 100) },
            },
        },
        new()
        {
            Id = "system-cooling-policy",
            Display = new()
            {
                Name = LocKey.Setting.SystemCoolingPolicy.Name,
                Description = LocKey.Setting.SystemCoolingPolicy.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.Fan,
            },
            Availability = new()
            {
                RequiresAdvancedUnlock = true,
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "94d3a615-a899-4ac5-ae2b-e4d8f634367f", PowerModeSupport.Separate) { EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\94d3a615-a899-4ac5-ae2b-e4d8f634367f" }, "Attributes", RegistryValueKind.DWord) },
            },
            States = PowerOptions.SelectionStates(PowerOptions.CoolingPolicy, 1, 0, 1, 0),
        },
        new()
        {
            Id = "processor-performance-boost-mode",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorPerformanceBoostMode.Name,
                Description = LocKey.Setting.ProcessorPerformanceBoostMode.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.RocketLaunch,
            },
            Availability = new()
            {
                RequiresAdvancedUnlock = true,
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "be337238-0d82-4146-a960-4f3749d470c7", PowerModeSupport.Separate) { EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\be337238-0d82-4146-a960-4f3749d470c7" }, "Attributes", RegistryValueKind.DWord) },
            },
            States = PowerOptions.SelectionStates(PowerOptions.ProcessorBoostMode, 2, 2, 2, 2),
        },
        new()
        {
            Id = "processor-performance-increase-policy",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorPerformanceIncreasePolicy.Name,
                Description = LocKey.Setting.ProcessorPerformanceIncreasePolicy.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.TrendingUp,
            },
            Availability = new()
            {
                RequiresAdvancedUnlock = true,
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "465e1f50-b610-473a-ab58-00d1077dc418", PowerModeSupport.Separate) { EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\465e1f50-b610-473a-ab58-00d1077dc418" }, "Attributes", RegistryValueKind.DWord) },
            },
            States = PowerOptions.SelectionStates(PowerOptions.PerformanceIncreasePolicy, 2, 0, 0, 0),
        },
        new()
        {
            Id = "processor-performance-decrease-policy",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorPerformanceDecreasePolicy.Name,
                Description = LocKey.Setting.ProcessorPerformanceDecreasePolicy.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.TrendingDown,
            },
            Availability = new()
            {
                RequiresAdvancedUnlock = true,
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "40fbefc7-2e9d-4d25-a185-0cfd8574bac6", PowerModeSupport.Separate) { EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\40fbefc7-2e9d-4d25-a185-0cfd8574bac6" }, "Attributes", RegistryValueKind.DWord) },
            },
            States = PowerOptions.SelectionStates(PowerOptions.PerformanceDecreasePolicy, 1, 0, 0, 0),
        },
        new()
        {
            Id = "processor-core-parking-min-cores",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorCoreParkingMinCores.Name,
                Description = LocKey.Setting.ProcessorCoreParkingMinCores.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.Cpu64Bit,
            },
            Availability = new()
            {
                RequiresAdvancedUnlock = true,
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "0cc5b647-c1df-4637-891a-dec35c318583", PowerModeSupport.Separate) { Units = "%", CheckForHardwareControl = true, EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583" }, "Attributes", RegistryValueKind.DWord) },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 100), new ContextValue(PowerContext.DC, 10) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 100), new ContextValue(PowerContext.DC, 10) },
            },
        },
        new()
        {
            Id = "processor-core-parking-max-cores",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorCoreParkingMaxCores.Name,
                Description = LocKey.Setting.ProcessorCoreParkingMaxCores.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.Cpu64Bit,
            },
            Availability = new()
            {
                RequiresAdvancedUnlock = true,
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "ea062031-0e34-4ff1-9b6d-eb1059334028", PowerModeSupport.Separate) { Units = "%", CheckForHardwareControl = true, EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\ea062031-0e34-4ff1-9b6d-eb1059334028" }, "Attributes", RegistryValueKind.DWord) },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 100), new ContextValue(PowerContext.DC, 100) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 100), new ContextValue(PowerContext.DC, 100) },
            },
        },
        new()
        {
            Id = "processor-energy-performance-preference",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorEnergyPerformancePreference.Name,
                Description = LocKey.Setting.ProcessorEnergyPerformancePreference.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.Tune,
            },
            Availability = new()
            {
                RequiresAdvancedUnlock = true,
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "36687f9e-e3a5-4dbf-b1dc-15eb381c6863", PowerModeSupport.Separate) { Units = "%", EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\36687f9e-e3a5-4dbf-b1dc-15eb381c6863" }, "Attributes", RegistryValueKind.DWord) },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 0), new ContextValue(PowerContext.DC, 50) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 33), new ContextValue(PowerContext.DC, 50) },
            },
        },
        new()
        {
            Id = "processor-performance-increase-threshold",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorPerformanceIncreaseThreshold.Name,
                Description = LocKey.Setting.ProcessorPerformanceIncreaseThreshold.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.TrendingUp,
            },
            Availability = new()
            {
                RequiresAdvancedUnlock = true,
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "06cadf0e-64ed-448a-8927-ce7bf90eb35d", PowerModeSupport.Separate) { Units = "%", EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\06cadf0e-64ed-448a-8927-ce7bf90eb35d" }, "Attributes", RegistryValueKind.DWord) },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 30), new ContextValue(PowerContext.DC, 90) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 60), new ContextValue(PowerContext.DC, 90) },
            },
        },
        new()
        {
            Id = "processor-performance-decrease-threshold",
            Display = new()
            {
                Name = LocKey.Setting.ProcessorPerformanceDecreaseThreshold.Name,
                Description = LocKey.Setting.ProcessorPerformanceDecreaseThreshold.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.TrendingDown,
            },
            Availability = new()
            {
                RequiresAdvancedUnlock = true,
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "54533251-82be-4824-96c1-47b60b740d00", "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6", PowerModeSupport.Separate) { Units = "%", EnablementKey = new RegTarget("Attributes", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\12a0ab44-fe28-4fa9-b3bd-4b64f44960a6" }, "Attributes", RegistryValueKind.DWord) },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 10), new ContextValue(PowerContext.DC, 30) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 20), new ContextValue(PowerContext.DC, 30) },
            },
        },
        new()
        {
            Id = "power-throttling",
            Display = new()
            {
                Name = LocKey.Setting.PowerThrottling.Name,
                Description = LocKey.Setting.PowerThrottling.Description,
                GroupName = LocKey.SettingGroup.ProcessorPowerManagement,
                Icon = MaterialIcons.SelectOff,
            },
            Targets = new Target[]
            {
                new RegTarget("PowerThrottlingOff", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" }, "PowerThrottlingOff", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["PowerThrottlingOff"] = StateValue.Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["PowerThrottlingOff"] = StateValue.Of(1) },
                    IsFallback = true,
                },
            },
        },
        new()
        {
            Id = "multimedia-when-sharing-media",
            Display = new()
            {
                Name = LocKey.Setting.MultimediaWhenSharingMedia.Name,
                Description = LocKey.Setting.MultimediaWhenSharingMedia.Description,
                GroupName = LocKey.SettingGroup.MultimediaSettings,
                Icon = MaterialIcons.Share,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "9596fb26-9850-41fd-ac3e-f7c3c00afd4b", "03680956-93bc-4294-bba6-4e0f09bb717f", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.MediaSharing, 1, 0, 1, 0),
        },
        new()
        {
            Id = "multimedia-video-playback-quality-bias",
            Display = new()
            {
                Name = LocKey.Setting.MultimediaVideoPlaybackQualityBias.Name,
                Description = LocKey.Setting.MultimediaVideoPlaybackQualityBias.Description,
                GroupName = LocKey.SettingGroup.MultimediaSettings,
                Icon = MaterialIcons.HighDefinition,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "9596fb26-9850-41fd-ac3e-f7c3c00afd4b", "10778347-1370-4ee0-8bbd-33bdacaade49", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.VideoQualityBias, 1, 0, 1, 0),
        },
        new()
        {
            Id = "multimedia-when-playing-video",
            Display = new()
            {
                Name = LocKey.Setting.MultimediaWhenPlayingVideo.Name,
                Description = LocKey.Setting.MultimediaWhenPlayingVideo.Description,
                GroupName = LocKey.SettingGroup.MultimediaSettings,
                Icon = MaterialIcons.Play,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "9596fb26-9850-41fd-ac3e-f7c3c00afd4b", "34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.VideoPlayback, 0, 1, 0, 1),
        },
        new()
        {
            Id = "critical-battery-notification",
            Display = new()
            {
                Name = LocKey.Setting.CriticalBatteryNotification.Name,
                Description = LocKey.Setting.CriticalBatteryNotification.Description,
                GroupName = LocKey.SettingGroup.Battery,
                Icon = MaterialIcons.AlertCircle,
            },
            Availability = new()
            {
                Hardware = new[] { HardwareRequirement.Battery },
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "e73a048d-bf27-4f12-9731-8b2076e8891f", "5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.OnOff, 1, 1, 1, 1),
        },
        new()
        {
            Id = "critical-battery-action",
            Display = new()
            {
                Name = LocKey.Setting.CriticalBatteryAction.Name,
                Description = LocKey.Setting.CriticalBatteryAction.Description,
                GroupName = LocKey.SettingGroup.Battery,
                Icon = MaterialIcons.BatteryAlert,
            },
            Availability = new()
            {
                Hardware = new[] { HardwareRequirement.Battery },
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "e73a048d-bf27-4f12-9731-8b2076e8891f", "637ea02f-bbcb-4015-8e2c-a1c7b9c0b546", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.BatteryActions, 2, 2, 2, 2),
        },
        new()
        {
            Id = "low-battery-level",
            Display = new()
            {
                Name = LocKey.Setting.LowBatteryLevel.Name,
                Description = LocKey.Setting.LowBatteryLevel.Description,
                GroupName = LocKey.SettingGroup.Battery,
                Icon = MaterialIcons.Battery20,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                Hardware = new[] { HardwareRequirement.Battery },
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "e73a048d-bf27-4f12-9731-8b2076e8891f", "8183ba9a-e910-48da-8769-14ae6dc1170a", PowerModeSupport.Separate) { Units = "%" },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 10), new ContextValue(PowerContext.DC, 10) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 10), new ContextValue(PowerContext.DC, 10) },
            },
        },
        new()
        {
            Id = "critical-battery-level",
            Display = new()
            {
                Name = LocKey.Setting.CriticalBatteryLevel.Name,
                Description = LocKey.Setting.CriticalBatteryLevel.Description,
                GroupName = LocKey.SettingGroup.Battery,
                Icon = MaterialIcons.BatteryOutline,
            },
            Availability = new()
            {
                Hardware = new[] { HardwareRequirement.Battery },
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "e73a048d-bf27-4f12-9731-8b2076e8891f", "9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469", PowerModeSupport.Separate) { Units = "%" },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 5), new ContextValue(PowerContext.DC, 5) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 5), new ContextValue(PowerContext.DC, 5) },
            },
        },
        new()
        {
            Id = "low-battery-notification",
            Display = new()
            {
                Name = LocKey.Setting.LowBatteryNotification.Name,
                Description = LocKey.Setting.LowBatteryNotification.Description,
                GroupName = LocKey.SettingGroup.Battery,
                Icon = MaterialIcons.Bell,
            },
            Availability = new()
            {
                Hardware = new[] { HardwareRequirement.Battery },
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "e73a048d-bf27-4f12-9731-8b2076e8891f", "bcded951-187b-4d05-bccc-f7e51960c258", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.OnOff, 1, 1, 1, 1),
        },
        new()
        {
            Id = "low-battery-action",
            Display = new()
            {
                Name = LocKey.Setting.LowBatteryAction.Name,
                Description = LocKey.Setting.LowBatteryAction.Description,
                GroupName = LocKey.SettingGroup.Battery,
                Icon = MaterialIcons.Battery20,
                IsSubjectivePreference = true,
            },
            Availability = new()
            {
                Hardware = new[] { HardwareRequirement.Battery },
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "e73a048d-bf27-4f12-9731-8b2076e8891f", "d8742dcb-3e6a-4b3c-b3fe-374623cdcf06", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.BatteryActions, 0, 0, 0, 0),
        },
        new()
        {
            Id = "reserve-battery-level",
            Display = new()
            {
                Name = LocKey.Setting.ReserveBatteryLevel.Name,
                Description = LocKey.Setting.ReserveBatteryLevel.Description,
                GroupName = LocKey.SettingGroup.Battery,
                Icon = MaterialIcons.BatteryCharging,
            },
            Availability = new()
            {
                Hardware = new[] { HardwareRequirement.Battery },
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "e73a048d-bf27-4f12-9731-8b2076e8891f", "f3c5027d-cd16-4930-aa6b-90db844a8f00", PowerModeSupport.Separate) { Units = "%" },
            },
            Numeric = new()
            {
                Min = 0,
                Max = 100,
                Units = "%",
                Recommended = new[] { new ContextValue(PowerContext.AC, 7), new ContextValue(PowerContext.DC, 7) },
                WindowsDefault = new[] { new ContextValue(PowerContext.AC, 7), new ContextValue(PowerContext.DC, 7) },
            },
        },
        new()
        {
            Id = "amd-power-slider-overlay",
            Display = new()
            {
                Name = LocKey.Setting.AmdPowerSliderOverlay.Name,
                Description = LocKey.Setting.AmdPowerSliderOverlay.Description,
                GroupName = LocKey.SettingGroup.AMDPowerSlider,
                Icon = MaterialIcons.ExpansionCard,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "c763b4ec-0e50-4b6b-9bed-2b92a6ee884e", "7ec1751b-60ed-4588-afb5-9819d3d77d90", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.AmdPowerSlider, 3, 1, 2, 2),
        },
        new()
        {
            Id = "ati-powerplay-setting",
            Display = new()
            {
                Name = LocKey.Setting.AtiPowerplaySetting.Name,
                Description = LocKey.Setting.AtiPowerplaySetting.Description,
                GroupName = LocKey.SettingGroup.ATIPowerPlay,
                Icon = MaterialIcons.ExpansionCard,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "f693fb01-e858-4f00-b20f-f30e12ac06d6", "191f65b5-d45c-4a4f-8aae-1ab8bfd980e6", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.AtiPowerPlay, 2, 1, 1, 1),
        },
        new()
        {
            Id = "switchable-graphics-gpu-preference",
            Display = new()
            {
                Name = LocKey.Setting.SwitchableGraphicsGpuPreference.Name,
                Description = LocKey.Setting.SwitchableGraphicsGpuPreference.Description,
                GroupName = LocKey.SettingGroup.SwitchableGraphics,
                Icon = MaterialIcons.SwapHorizontal,
            },
            Availability = new()
            {
                ValidatesExistence = true,
            },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[]
            {
                new PowerCfgTarget("Power", "e276e160-7cb0-43c6-b20b-73f5dce39954", "a1662ab2-9d34-4e53-ba8b-2639b9e20857", PowerModeSupport.Separate),
            },
            States = PowerOptions.SelectionStates(PowerOptions.SwitchableGraphics, 2, 1, 1, 1),
        },
        new()
        {
            Id = "start-power-lock-option",
            Display = new()
            {
                Name = LocKey.Setting.StartPowerLockOption.Name,
                Description = LocKey.Setting.StartPowerLockOption.Description,
                GroupName = LocKey.SettingGroup.StartMenu,
                Icon = MaterialIcons.EyeLock,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowLockOption", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings" }, "ShowLockOption", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowLockOption"] = StateValue.Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    Links = new[]
                    {
                        new Link("privacy-lock-screen", LinkKind.Requires, LocKey.Common.Enabled),
                    },
                    Set = new Dictionary<string, StateValue> { ["ShowLockOption"] = StateValue.Of(0) },
                    IsFallback = true,
                },
            },
        },
        new()
        {
            Id = "start-power-sleep-option",
            Display = new()
            {
                Name = LocKey.Setting.StartPowerSleepOption.Name,
                Description = LocKey.Setting.StartPowerSleepOption.Description,
                GroupName = LocKey.SettingGroup.StartMenu,
                Icon = MaterialIcons.LightbulbNight,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowSleepOption", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings" }, "ShowSleepOption", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowSleepOption"] = StateValue.Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["ShowSleepOption"] = StateValue.Of(0) },
                    IsFallback = true,
                },
            },
        },
    };
}

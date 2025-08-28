using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Models.WindowsRegistry;

namespace Winhance.Core.Features.Optimize.Models;

public static class UpdateOptimizations
{
    public static SettingGroup GetUpdateOptimizations()
    {
        return new SettingGroup
        {
            Name = "Windows Updates",
            FeatureId = FeatureIds.Update,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "updates-auto-update",
                    Name = "Automatic Windows Updates",
                    Description = "Controls automatic Windows updates behavior",
                    GroupName = "Windows Update Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoUpdate",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, automatic updates are enabled
                            DisabledValue = 1, // When toggle is OFF, automatic updates are disabled
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AUOptions",
                            RecommendedValue = 2,
                            EnabledValue = 4, // When toggle is ON, auto download and schedule install (4)
                            DisabledValue = 2, // When toggle is OFF, notify before download (2)
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AutoInstallMinorUpdates",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, minor updates are installed automatically
                            DisabledValue = 0, // When toggle is OFF, minor updates are not installed automatically
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-defer-feature-updates",
                    Name = "Delay Feature Updates for 365 Days",
                    Description = "Delays major Windows feature updates for 365 days",
                    GroupName = "Windows Update Policies",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "DeferFeatureUpdates",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, feature updates are deferred
                            DisabledValue = 0, // When toggle is OFF, feature updates are not deferred
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "DeferFeatureUpdatesPeriodInDays",
                            RecommendedValue = 365,
                            EnabledValue = 365, // When toggle is ON, feature updates are deferred for 365 days
                            DisabledValue = 0, // When toggle is OFF, feature updates are not deferred
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-defer-quality-updates",
                    Name = "Delay Security Updates for 7 Days",
                    Description = "Delays Windows security and quality updates for 7 days",
                    GroupName = "Windows Update Policies",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "DeferQualityUpdates",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, quality updates are deferred
                            DisabledValue = 0, // When toggle is OFF, quality updates are not deferred
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "DeferQualityUpdatesPeriodInDays",
                            RecommendedValue = 7,
                            EnabledValue = 7, // When toggle is ON, quality updates are deferred for 7 days
                            DisabledValue = 0, // When toggle is OFF, quality updates are not deferred
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-delivery-optimization",
                    Name = "Delivery Optimization (LAN)",
                    Description = "Controls peer-to-peer update distribution",
                    GroupName = "Delivery Optimization",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                            ValueName = "DODownloadMode",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, peer-to-peer update distribution is enabled (LAN only)
                            DisabledValue = 0, // When toggle is OFF, peer-to-peer update distribution is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-store-auto-download",
                    Name = "Auto Update Microsoft Store Apps",
                    Description = "Controls automatic updates for Microsoft Store apps",
                    GroupName = "Microsoft Store",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore",
                            ValueName = "AutoDownload",
                            RecommendedValue = 2,
                            EnabledValue = 4, // When toggle is ON, automatic updates for Microsoft Store apps are enabled
                            DisabledValue = 2, // When toggle is OFF, automatic updates for Microsoft Store apps are disabled
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-app-archiving",
                    Name = "Automatic Archiving of Unused Apps",
                    Description = "Controls automatic archiving of unused apps",
                    GroupName = "Microsoft Store",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Appx",
                            ValueName = "AllowAutomaticAppArchiving",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, automatic archiving of unused apps is enabled
                            DisabledValue = 0, // When toggle is OFF, automatic archiving of unused apps is disabled
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-restart-options",
                    Name = "Prevent Automatic Restarts",
                    Description =
                        "Prevents automatic restarts after installing updates when users are logged on",
                    GroupName = "Update Behavior",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoRebootWithLoggedOnUsers",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, automatic restarts are prevented
                            DisabledValue = 1, // When toggle is OFF, automatic restarts are allowed
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-driver-controls",
                    Name = "Do Not Include Drivers with Updates",
                    Description = "Does not include driver updates with Windows quality updates",
                    GroupName = "Update Content",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "ExcludeWUDriversInQualityUpdate",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, driver updates are included
                            DisabledValue = 1, // When toggle is OFF, driver updates are excluded
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-notification-level",
                    Name = "Update Notifications",
                    Description = "Controls the visibility of update notifications",
                    GroupName = "Update Behavior",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "SetUpdateNotificationLevel",
                            RecommendedValue = 1,
                            EnabledValue = 2, // When toggle is ON, show all notifications (2 = default)
                            DisabledValue = 1, // When toggle is OFF, show only restart required notifications (1 = reduced)
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-metered-connection",
                    Name = "Updates on Metered Connections",
                    Description =
                        "Controls whether updates are downloaded over metered connections",
                    GroupName = "Update Behavior",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "AllowAutoWindowsUpdateDownloadOverMeteredNetwork",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, updates are downloaded over metered connections
                            DisabledValue = 0, // When toggle is OFF, updates are not downloaded over metered connections
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

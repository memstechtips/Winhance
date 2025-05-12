using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class UpdateOptimizations
{
    public static OptimizationGroup GetUpdateOptimizations()
    {
        return new OptimizationGroup
        {
            Name = "Windows Updates",
            Category = OptimizationCategory.Updates,
            Settings = new List<OptimizationSetting>
            {
                new OptimizationSetting
                {
                    Id = "updates-auto-update",
                    Name = "Automatic Windows Updates",
                    Description = "Controls automatic Windows updates behavior",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Windows Update Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU",
                            Name = "NoAutoUpdate",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, automatic updates are enabled
                            DisabledValue = 1, // When toggle is OFF, automatic updates are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls automatic Windows updates",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU",
                            Name = "AUOptions",
                            RecommendedValue = 2,
                            EnabledValue = 4, // When toggle is ON, auto download and schedule install (4)
                            DisabledValue = 2, // When toggle is OFF, notify before download (2)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            Description = "Controls automatic update behavior",
                        },
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU",
                            Name = "AutoInstallMinorUpdates",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, minor updates are installed automatically
                            DisabledValue = 0, // When toggle is OFF, minor updates are not installed automatically
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls automatic installation of minor updates",
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                },
                new OptimizationSetting
                {
                    Id = "updates-defer-feature-updates",
                    Name = "Delay Feature Updates for 365 Days",
                    Description = "Delays major Windows feature updates for 365 days",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Windows Update Policies",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate",
                            Name = "DeferFeatureUpdates",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, feature updates are deferred
                            DisabledValue = 0, // When toggle is OFF, feature updates are not deferred
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Enables deferral of feature updates",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate",
                            Name = "DeferFeatureUpdatesPeriodInDays",
                            RecommendedValue = 365,
                            EnabledValue = 365, // When toggle is ON, feature updates are deferred for 365 days
                            DisabledValue = 0, // When toggle is OFF, feature updates are not deferred
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description =
                                "Sets the deferral period for feature updates to 365 days",
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                },
                new OptimizationSetting
                {
                    Id = "updates-defer-quality-updates",
                    Name = "Delay Security Updates for 7 Days",
                    Description = "Delays Windows security and quality updates for 7 days",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Windows Update Policies",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate",
                            Name = "DeferQualityUpdates",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, quality updates are deferred
                            DisabledValue = 0, // When toggle is OFF, quality updates are not deferred
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Enables deferral of security and quality updates",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate",
                            Name = "DeferQualityUpdatesPeriodInDays",
                            RecommendedValue = 7,
                            EnabledValue = 7, // When toggle is ON, quality updates are deferred for 7 days
                            DisabledValue = 0, // When toggle is OFF, quality updates are not deferred
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description =
                                "Sets the deferral period for security and quality updates to 7 days",
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                },
                new OptimizationSetting
                {
                    Id = "updates-delivery-optimization",
                    Name = "Delivery Optimization (LAN)",
                    Description = "Controls peer-to-peer update distribution",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Delivery Optimization",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization",
                            Name = "DODownloadMode",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, peer-to-peer update distribution is enabled (LAN only)
                            DisabledValue = 0, // When toggle is OFF, peer-to-peer update distribution is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls peer-to-peer update distribution",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "updates-store-auto-download",
                    Name = "Auto Update Microsoft Store Apps",
                    Description = "Controls automatic updates for Microsoft Store apps",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Microsoft Store",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\WindowsStore",
                            Name = "AutoDownload",
                            RecommendedValue = 2,
                            EnabledValue = 4, // When toggle is ON, automatic updates for Microsoft Store apps are enabled
                            DisabledValue = 2, // When toggle is OFF, automatic updates for Microsoft Store apps are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            Description = "Controls automatic updates for Microsoft Store apps",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "updates-app-archiving",
                    Name = "Automatic Archiving of Unused Apps",
                    Description = "Controls automatic archiving of unused apps",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Microsoft Store",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\Appx",
                            Name = "AllowAutomaticAppArchiving",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, automatic archiving of unused apps is enabled
                            DisabledValue = 0, // When toggle is OFF, automatic archiving of unused apps is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls automatic archiving of unused apps",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "updates-restart-options",
                    Name = "Prevent Automatic Restarts",
                    Description =
                        "Prevents automatic restarts after installing updates when users are logged on",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Update Behavior",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU",
                            Name = "NoAutoRebootWithLoggedOnUsers",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, automatic restarts are prevented
                            DisabledValue = 1, // When toggle is OFF, automatic restarts are allowed
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls automatic restart behavior after updates",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "updates-driver-controls",
                    Name = "Do Not Include Drivers with Updates",
                    Description = "Does not include driver updates with Windows quality updates",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Update Content",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate",
                            Name = "ExcludeWUDriversInQualityUpdate",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, driver updates are included
                            DisabledValue = 1, // When toggle is OFF, driver updates are excluded
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description =
                                "Controls whether driver updates are included in Windows quality updates",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "updates-notification-level",
                    Name = "Update Notifications",
                    Description = "Controls the visibility of update notifications",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Update Behavior",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate",
                            Name = "SetUpdateNotificationLevel",
                            RecommendedValue = 1,
                            EnabledValue = 2, // When toggle is ON, show all notifications (2 = default)
                            DisabledValue = 1, // When toggle is OFF, show only restart required notifications (1 = reduced)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            Description = "Controls the visibility level of update notifications",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "updates-metered-connection",
                    Name = "Updates on Metered Connections",
                    Description =
                        "Controls whether updates are downloaded over metered connections",
                    Category = OptimizationCategory.Updates,
                    GroupName = "Update Behavior",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Updates",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\WindowsUpdate\\UX\\Settings",
                            Name = "AllowAutoWindowsUpdateDownloadOverMeteredNetwork",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, updates are downloaded over metered connections
                            DisabledValue = 0, // When toggle is OFF, updates are not downloaded over metered connections
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description =
                                "Controls update download behavior on metered connections",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
            },
        };
    }
}

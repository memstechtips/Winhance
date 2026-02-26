using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Constants;

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
                    Id = "updates-policy-mode",
                    Name = "Windows Update Policy",
                    Description = "Control how Windows updates are installed on your system",
                    GroupName = "Update Policy",
                    Icon = "BookSync",
                    InputType = InputType.Selection,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoUpdate",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoUpdate",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AUOptions",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AUOptions",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "BranchReadinessLevel",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "DeferFeatureUpdates",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "DeferFeatureUpdatesPeriodInDays",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "DeferQualityUpdates",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "DeferQualityUpdatesPeriodInDays",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseFeatureUpdatesStartTime",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseFeatureUpdatesEndTime",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseQualityUpdatesStartTime",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseQualityUpdatesEndTime",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseUpdatesStartTime",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseUpdatesExpiryTime",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PausedQualityDate",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PausedFeatureDate",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "FlightSettingsMaxPauseDays",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAUShutdownOption",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAUShutdownOption",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AlwaysAutoRebootAtScheduledTime",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AlwaysAutoRebootAtScheduledTime",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AutoInstallMinorUpdates",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AutoInstallMinorUpdates",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "UseWUServer",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "UseWUServer",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PausedFeatureStatus",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PausedQualityStatus",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    DisableTooltip = true,
                    ComboBox = new ComboBoxMetadata
                    {
                        DisplayNames = new string[]
                        {
                            "Normal (Windows Default)",
                            "Security Updates Only (Recommended)",
                            "Paused for a long time (Unpause in Settings)",
                            "Disabled (NOT Recommended, Security Risk)",
                        },
                        OptionWarnings = new Dictionary<int, string>
                        {
                            [2] = "WARNING: Pausing updates for a long time leaves your system vulnerable to security threats. Use at your own risk.",
                            [3] = "WARNING: Disabling updates leaves your system vulnerable to security threats and will prevent app installations from the Microsoft Store from completing until updates are enabled. Use at your own risk."
                        },
                        OptionTooltips = new string[]
                        {
                            "Windows default behavior - automatic updates enabled",
                            "Only install critical security updates, defer feature updates by 1 year",
                            "Pause all updates until 2051 - manually unpause in Windows Settings when needed",
                            "Completely disable Windows Update services and block all updates - NOT RECOMMENDED"
                        },
                        ValueMappings = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> // Normal
                            {
                                ["NoAutoUpdate"] = null,
                                ["AUOptions"] = null,
                                ["BranchReadinessLevel"] = null,
                                ["DeferFeatureUpdates"] = null,
                                ["DeferFeatureUpdatesPeriodInDays"] = null,
                                ["DeferQualityUpdates"] = null,
                                ["DeferQualityUpdatesPeriodInDays"] = null,
                                ["PauseFeatureUpdatesStartTime"] = null,
                                ["PauseFeatureUpdatesEndTime"] = null,
                                ["PauseQualityUpdatesStartTime"] = null,
                                ["PauseQualityUpdatesEndTime"] = null,
                                ["PauseUpdatesStartTime"] = null,
                                ["PauseUpdatesExpiryTime"] = null,
                                ["PausedQualityDate"] = null,
                                ["PausedFeatureDate"] = null,
                                ["FlightSettingsMaxPauseDays"] = null,
                                ["NoAUShutdownOption"] = null,
                                ["AlwaysAutoRebootAtScheduledTime"] = null,
                                ["AutoInstallMinorUpdates"] = null,
                                ["UseWUServer"] = null,
                                ["PausedFeatureStatus"] = null,
                                ["PausedQualityStatus"] = null,
                            },
                            [1] = new Dictionary<string, object?> // Security Only
                            {
                                ["NoAutoUpdate"] = null,
                                ["AUOptions"] = 2,
                                ["BranchReadinessLevel"] = 20,
                                ["DeferFeatureUpdates"] = 1,
                                ["DeferFeatureUpdatesPeriodInDays"] = 365,
                                ["DeferQualityUpdates"] = 1,
                                ["DeferQualityUpdatesPeriodInDays"] = 7,
                                ["PauseFeatureUpdatesStartTime"] = null,
                                ["PauseFeatureUpdatesEndTime"] = null,
                                ["PauseQualityUpdatesStartTime"] = null,
                                ["PauseQualityUpdatesEndTime"] = null,
                                ["PauseUpdatesStartTime"] = null,
                                ["PauseUpdatesExpiryTime"] = null,
                                ["PausedQualityDate"] = null,
                                ["PausedFeatureDate"] = null,
                                ["FlightSettingsMaxPauseDays"] = null,
                                ["NoAUShutdownOption"] = null,
                                ["AlwaysAutoRebootAtScheduledTime"] = null,
                                ["AutoInstallMinorUpdates"] = null,
                                ["UseWUServer"] = null,
                                ["PausedFeatureStatus"] = null,
                                ["PausedQualityStatus"] = null,
                            },
                            [2] = new Dictionary<string, object?> // Paused
                            {
                                ["NoAutoUpdate"] = 1,
                                ["AUOptions"] = 1,
                                ["BranchReadinessLevel"] = null,
                                ["DeferFeatureUpdates"] = null,
                                ["DeferFeatureUpdatesPeriodInDays"] = null,
                                ["DeferQualityUpdates"] = null,
                                ["DeferQualityUpdatesPeriodInDays"] = null,
                                ["PauseFeatureUpdatesStartTime"] = "2025-01-01T00:00:00Z",
                                ["PauseFeatureUpdatesEndTime"] = "2051-12-31T00:00:00Z",
                                ["PauseQualityUpdatesStartTime"] = "2025-01-01T00:00:00Z",
                                ["PauseQualityUpdatesEndTime"] = "2051-12-31T00:00:00Z",
                                ["PauseUpdatesStartTime"] = "2025-01-01T00:00:00Z",
                                ["PauseUpdatesExpiryTime"] = "2051-12-31T00:00:00Z",
                                ["PausedQualityDate"] = "2025-01-01T00:00:00Z",
                                ["PausedFeatureDate"] = "2025-01-01T00:00:00Z",
                                ["FlightSettingsMaxPauseDays"] = 10023,
                                ["NoAUShutdownOption"] = 1,
                                ["AlwaysAutoRebootAtScheduledTime"] = 0,
                                ["AutoInstallMinorUpdates"] = 0,
                                ["UseWUServer"] = 0,
                                ["PausedFeatureStatus"] = 1,
                                ["PausedQualityStatus"] = 1,
                            },
                            [3] = new Dictionary<string, object?> // Disabled
                            {
                                ["NoAutoUpdate"] = 1,
                                ["AUOptions"] = 1,
                                ["BranchReadinessLevel"] = null,
                                ["DeferFeatureUpdates"] = null,
                                ["DeferFeatureUpdatesPeriodInDays"] = null,
                                ["DeferQualityUpdates"] = null,
                                ["DeferQualityUpdatesPeriodInDays"] = null,
                                ["PauseFeatureUpdatesStartTime"] = null,
                                ["PauseFeatureUpdatesEndTime"] = null,
                                ["PauseQualityUpdatesStartTime"] = null,
                                ["PauseQualityUpdatesEndTime"] = null,
                                ["PauseUpdatesStartTime"] = null,
                                ["PauseUpdatesExpiryTime"] = null,
                                ["PausedQualityDate"] = null,
                                ["PausedFeatureDate"] = null,
                                ["FlightSettingsMaxPauseDays"] = null,
                                ["NoAUShutdownOption"] = null,
                                ["AlwaysAutoRebootAtScheduledTime"] = null,
                                ["AutoInstallMinorUpdates"] = null,
                                ["UseWUServer"] = 0,
                                ["PausedFeatureStatus"] = null,
                                ["PausedQualityStatus"] = null,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-delivery-optimization",
                    Name = "Delivery Optimization",
                    Description = "Share downloaded updates with other PCs on your network or the internet to reduce bandwidth usage",
                    GroupName = "Delivery & Store",
                    Icon = "ShareVariant",
                    InputType = InputType.Selection,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                            ValueName = "DODownloadMode",
                            RecommendedValue = 99,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                            ValueName = "DODownloadMode",
                            RecommendedValue = 99,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        DisplayNames = new string[]
                        {
                            "Windows Default",
                            "Devices on LAN Only",
                            "Devices on LAN and Internet",
                            "ServiceOption_Disabled",
                        },
                        ValueMappings = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["DODownloadMode"] = null,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["DODownloadMode"] = 1,
                            },
                            [2] = new Dictionary<string, object?>
                            {
                                ["DODownloadMode"] = 3,
                            },
                            [3] = new Dictionary<string, object?>
                            {
                                ["DODownloadMode"] = 99,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-latest-updates",
                    Name = "Get the latest updates as soon as they're available",
                    Description = "Be among the first to get the latest non-security updates, fixes, and improvements as they roll out",
                    GroupName = "Update Behavior",
                    Icon = "BullhornVariant",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "IsContinuousInnovationOptedIn",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-other-products",
                    Name = "Receive updates for other Microsoft products",
                    Description = "Get Microsoft Office and other updates together with Windows updates",
                    GroupName = "Update Behavior",
                    Icon = "ArchiveSync",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "AllowMUUpdateService",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-restart-asap",
                    Name = "Get me up to date",
                    Description = "Restart as soon as possible (even during active hours) to finish updating",
                    GroupName = "Update Behavior",
                    Icon = "Restart",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "IsExpedited",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },         
                new SettingDefinition
                {
                    Id = "updates-restart-options",
                    Name = "Prevent Automatic Restarts",
                    Description = "Prevents automatic restarts after installing updates when users are logged on",
                    GroupName = "Update Behavior",
                    Icon = "RestartOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoRebootWithLoggedOnUsers",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoRebootWithLoggedOnUsers",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-notification-level",
                    Name = "Update Notifications",
                    Description = "Show or hide notifications about available updates and update progress",
                    GroupName = "Update Behavior",
                    Icon = "BellPlus",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "SetUpdateNotificationLevel",
                            RecommendedValue = 1,
                            EnabledValue = 2,
                            DisabledValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "SetUpdateNotificationLevel",
                            RecommendedValue = 1,
                            EnabledValue = 2,
                            DisabledValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-restart-notification",
                    Name = "Notify me when a restart is required to finish updating",
                    Description = "Show notification when your device requires a restart to finish updating",
                    GroupName = "Update Behavior",
                    Icon = "RestartAlert",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "RestartNotificationsAllowed2",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-metered-connection",
                    Name = "Download updates over metered connections",
                    Description = "Allow Windows to download updates when using mobile hotspots or data-limited connections",
                    GroupName = "Update Behavior",
                    Icon = "Connection",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "AllowAutoWindowsUpdateDownloadOverMeteredNetwork",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-driver-controls",
                    Name = "Do Not Include Drivers with Updates",
                    Description = "Prevent Windows from automatically downloading and installing hardware driver updates",
                    GroupName = "Update Behavior",
                    Icon = "PackageVariantClosedMinus",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "ExcludeWUDriversInQualityUpdate",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "ExcludeWUDriversInQualityUpdate",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-store-auto-download",
                    Name = "Auto Update Microsoft Store Apps",
                    Description = "Automatically download and install updates for apps from the Microsoft Store",
                    GroupName = "Delivery & Store",
                    IconPack = "Fluent",
                    Icon = "StoreMicrosoft",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\WindowsStore",
                            ValueName = "AutoDownload",
                            RecommendedValue = 2,
                            EnabledValue = null,
                            DisabledValue = 2,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore",
                            ValueName = "AutoDownload",
                            RecommendedValue = 2,
                            EnabledValue = null,
                            DisabledValue = 2,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

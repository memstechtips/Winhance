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
                    IsSubjectivePreference = true,
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
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoUpdate",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AUOptions",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AUOptions",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "BranchReadinessLevel",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "DeferFeatureUpdates",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "DeferFeatureUpdatesPeriodInDays",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "DeferQualityUpdates",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "DeferQualityUpdatesPeriodInDays",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseFeatureUpdatesStartTime",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseFeatureUpdatesEndTime",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseQualityUpdatesStartTime",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseQualityUpdatesEndTime",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseUpdatesStartTime",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PauseUpdatesExpiryTime",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PausedQualityDate",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PausedFeatureDate",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "FlightSettingsMaxPauseDays",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAUShutdownOption",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAUShutdownOption",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AlwaysAutoRebootAtScheduledTime",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AlwaysAutoRebootAtScheduledTime",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AutoInstallMinorUpdates",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "AutoInstallMinorUpdates",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "UseWUServer",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "UseWUServer",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PausedFeatureStatus",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "PausedQualityStatus",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    DisableTooltip = true,
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption
                            {
                                DisplayName = "Normal (Windows Default)",
                                Tooltip = "Windows default behavior - automatic updates enabled",
                                ValueMappings = new Dictionary<string, object?>
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
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Security Updates Only (Recommended)",
                                Tooltip = "Only install critical security updates, defer feature updates by 1 year",
                                ValueMappings = new Dictionary<string, object?>
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
                                IsRecommended = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Paused for a long time (Unpause in Settings)",
                                Tooltip = "Pause all updates until 2051 - manually unpause in Windows Settings when needed",
                                Warning = "WARNING: Pausing updates for a long time leaves your system vulnerable to security threats. Use at your own risk.",
                                ValueMappings = new Dictionary<string, object?>
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
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Disabled (NOT Recommended, Security Risk)",
                                Tooltip = "Completely disable Windows Update services and block all updates - NOT RECOMMENDED",
                                Warning = "WARNING: Disabling updates leaves your system vulnerable to security threats and will prevent app installations from the Microsoft Store from completing until updates are enabled. Use at your own risk.",
                                ValueMappings = new Dictionary<string, object?>
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
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                            ValueName = "DODownloadMode",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption
                            {
                                DisplayName = "Windows Default",
                                ValueMappings = new Dictionary<string, object?> { ["DODownloadMode"] = null },
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Devices on LAN Only",
                                ValueMappings = new Dictionary<string, object?> { ["DODownloadMode"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Devices on LAN and Internet",
                                ValueMappings = new Dictionary<string, object?> { ["DODownloadMode"] = 3 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "ServiceOption_Disabled",
                                ValueMappings = new Dictionary<string, object?> { ["DODownloadMode"] = 99 },
                                IsRecommended = true,
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
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-other-products",
                    IsSubjectivePreference = true,
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
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },         
                new SettingDefinition
                {
                    Id = "updates-restart-options",
                    RecommendedToggleState = false,
                    Name = "Automatic Restart After Updates",
                    Description = "Allow Windows to automatically restart your PC after installing updates when you are logged in",
                    GroupName = "Update Behavior",
                    Icon = "RestartOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoRebootWithLoggedOnUsers",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoRebootWithLoggedOnUsers",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-notification-level",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
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
                            RecommendedValue = null,
                            EnabledValue = [2],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "SetUpdateNotificationLevel",
                            RecommendedValue = null,
                            EnabledValue = [2],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-restart-notification",
                    IsSubjectivePreference = true,
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
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-driver-controls",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    Name = "Driver Updates via Windows Update",
                    Description = "Include hardware driver updates when downloading and installing Windows Updates",
                    GroupName = "Update Behavior",
                    Icon = "PackageVariantClosedMinus",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "ExcludeWUDriversInQualityUpdate",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "ExcludeWUDriversInQualityUpdate",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-driver-coinstallers",
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    Name = "Driver Co-Installers",
                    Description = "Allows hardware vendors to install companion software alongside device drivers. Disabling this prevents bloatware like Razer Synapse, printer utilities, and other vendor software from being automatically installed when you plug in devices. Your hardware will still work normally with standard drivers.",
                    GroupName = "Update Behavior",
                    Icon = "PackageVariantRemove",
                    InputType = InputType.Toggle,
                    AddedInVersion = "25.04.08",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Installer",
                            ValueName = "DisableCoInstallers",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
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
                            EnabledValue = [4, null],
                            DisabledValue = [2],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore",
                            ValueName = "AutoDownload",
                            RecommendedValue = 2,
                            EnabledValue = [4, null],
                            DisabledValue = [2],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
            },
        };
    }
}

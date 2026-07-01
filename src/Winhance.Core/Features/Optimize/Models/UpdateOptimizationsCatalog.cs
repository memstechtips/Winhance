using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;

namespace Winhance.Core.Features.Optimize.Models;

public static class UpdateOptimizationsCatalog
{
    public const string FeatureId = FeatureIds.Update;
    public const string FeatureName = "Windows Updates";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "updates-policy-mode",
            // Detection is not registry-expressible (Disabled and Paused both write NoAutoUpdate=1/AUOptions=1, and
            // Disabled is enforced by a filesystem DLL rename), so a custom detector reproduces the old UpdateService
            // special-handler precedence. Labels must equal the States below.
            Detector = new UpdatePolicyDetector(
                "Normal (Windows Default)",
                "Security Updates Only (Recommended)",
                "Paused for a long time (Unpause in Settings)",
                "Disabled (NOT Recommended, Security Risk)"),
            Display = new()
            {
                Name = "Windows Update Policy",
                Description = "Control how Windows updates are installed on your system",
                GroupName = "Update Policy",
                Icon = MaterialIcons.BookSync,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("NoAutoUpdate", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" }, "NoAutoUpdate", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("AUOptions", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" }, "AUOptions", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("BranchReadinessLevel", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "BranchReadinessLevel", RegistryValueKind.DWord),
                new RegTarget("DeferFeatureUpdates", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "DeferFeatureUpdates", RegistryValueKind.DWord),
                new RegTarget("DeferFeatureUpdatesPeriodInDays", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "DeferFeatureUpdatesPeriodInDays", RegistryValueKind.DWord),
                new RegTarget("DeferQualityUpdates", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "DeferQualityUpdates", RegistryValueKind.DWord),
                new RegTarget("DeferQualityUpdatesPeriodInDays", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "DeferQualityUpdatesPeriodInDays", RegistryValueKind.DWord),
                new RegTarget("PauseFeatureUpdatesStartTime", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PauseFeatureUpdatesStartTime", RegistryValueKind.String),
                new RegTarget("PauseFeatureUpdatesEndTime", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PauseFeatureUpdatesEndTime", RegistryValueKind.String),
                new RegTarget("PauseQualityUpdatesStartTime", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PauseQualityUpdatesStartTime", RegistryValueKind.String),
                new RegTarget("PauseQualityUpdatesEndTime", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PauseQualityUpdatesEndTime", RegistryValueKind.String),
                new RegTarget("PauseUpdatesStartTime", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PauseUpdatesStartTime", RegistryValueKind.String),
                new RegTarget("PauseUpdatesExpiryTime", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PauseUpdatesExpiryTime", RegistryValueKind.String),
                new RegTarget("PausedQualityDate", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PausedQualityDate", RegistryValueKind.String),
                new RegTarget("PausedFeatureDate", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PausedFeatureDate", RegistryValueKind.String),
                new RegTarget("FlightSettingsMaxPauseDays", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "FlightSettingsMaxPauseDays", RegistryValueKind.DWord),
                new RegTarget("NoAUShutdownOption", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" }, "NoAUShutdownOption", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("AlwaysAutoRebootAtScheduledTime", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" }, "AlwaysAutoRebootAtScheduledTime", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("AutoInstallMinorUpdates", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" }, "AutoInstallMinorUpdates", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("UseWUServer", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" }, "UseWUServer", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("PausedFeatureStatus", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PausedFeatureStatus", RegistryValueKind.DWord),
                new RegTarget("PausedQualityStatus", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "PausedQualityStatus", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Normal (Windows Default)",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["NoAutoUpdate"] = Absent,
                        ["AUOptions"] = Absent,
                        ["BranchReadinessLevel"] = Absent,
                        ["DeferFeatureUpdates"] = Absent,
                        ["DeferFeatureUpdatesPeriodInDays"] = Absent,
                        ["DeferQualityUpdates"] = Absent,
                        ["DeferQualityUpdatesPeriodInDays"] = Absent,
                        ["PauseFeatureUpdatesStartTime"] = Absent,
                        ["PauseFeatureUpdatesEndTime"] = Absent,
                        ["PauseQualityUpdatesStartTime"] = Absent,
                        ["PauseQualityUpdatesEndTime"] = Absent,
                        ["PauseUpdatesStartTime"] = Absent,
                        ["PauseUpdatesExpiryTime"] = Absent,
                        ["PausedQualityDate"] = Absent,
                        ["PausedFeatureDate"] = Absent,
                        ["FlightSettingsMaxPauseDays"] = Absent,
                        ["NoAUShutdownOption"] = Absent,
                        ["AlwaysAutoRebootAtScheduledTime"] = Absent,
                        ["AutoInstallMinorUpdates"] = Absent,
                        ["UseWUServer"] = Absent,
                        ["PausedFeatureStatus"] = Absent,
                        ["PausedQualityStatus"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Security Updates Only (Recommended)",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["NoAutoUpdate"] = Absent,
                        ["AUOptions"] = Of(2),
                        ["BranchReadinessLevel"] = Of(20),
                        ["DeferFeatureUpdates"] = Of(1),
                        ["DeferFeatureUpdatesPeriodInDays"] = Of(365),
                        ["DeferQualityUpdates"] = Of(1),
                        ["DeferQualityUpdatesPeriodInDays"] = Of(7),
                        ["PauseFeatureUpdatesStartTime"] = Absent,
                        ["PauseFeatureUpdatesEndTime"] = Absent,
                        ["PauseQualityUpdatesStartTime"] = Absent,
                        ["PauseQualityUpdatesEndTime"] = Absent,
                        ["PauseUpdatesStartTime"] = Absent,
                        ["PauseUpdatesExpiryTime"] = Absent,
                        ["PausedQualityDate"] = Absent,
                        ["PausedFeatureDate"] = Absent,
                        ["FlightSettingsMaxPauseDays"] = Absent,
                        ["NoAUShutdownOption"] = Absent,
                        ["AlwaysAutoRebootAtScheduledTime"] = Absent,
                        ["AutoInstallMinorUpdates"] = Absent,
                        ["UseWUServer"] = Absent,
                        ["PausedFeatureStatus"] = Absent,
                        ["PausedQualityStatus"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Paused for a long time (Unpause in Settings)",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["NoAutoUpdate"] = Of(1),
                        ["AUOptions"] = Of(1),
                        ["BranchReadinessLevel"] = Absent,
                        ["DeferFeatureUpdates"] = Absent,
                        ["DeferFeatureUpdatesPeriodInDays"] = Absent,
                        ["DeferQualityUpdates"] = Absent,
                        ["DeferQualityUpdatesPeriodInDays"] = Absent,
                        ["PauseFeatureUpdatesStartTime"] = Of("2025-01-01T00:00:00Z"),
                        ["PauseFeatureUpdatesEndTime"] = Of("2051-12-31T00:00:00Z"),
                        ["PauseQualityUpdatesStartTime"] = Of("2025-01-01T00:00:00Z"),
                        ["PauseQualityUpdatesEndTime"] = Of("2051-12-31T00:00:00Z"),
                        ["PauseUpdatesStartTime"] = Of("2025-01-01T00:00:00Z"),
                        ["PauseUpdatesExpiryTime"] = Of("2051-12-31T00:00:00Z"),
                        ["PausedQualityDate"] = Of("2025-01-01T00:00:00Z"),
                        ["PausedFeatureDate"] = Of("2025-01-01T00:00:00Z"),
                        ["FlightSettingsMaxPauseDays"] = Of(10023),
                        ["NoAUShutdownOption"] = Of(1),
                        ["AlwaysAutoRebootAtScheduledTime"] = Of(0),
                        ["AutoInstallMinorUpdates"] = Of(0),
                        ["UseWUServer"] = Of(0),
                        ["PausedFeatureStatus"] = Of(1),
                        ["PausedQualityStatus"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "Disabled (NOT Recommended, Security Risk)",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["NoAutoUpdate"] = Of(1),
                        ["AUOptions"] = Of(1),
                        ["BranchReadinessLevel"] = Absent,
                        ["DeferFeatureUpdates"] = Absent,
                        ["DeferFeatureUpdatesPeriodInDays"] = Absent,
                        ["DeferQualityUpdates"] = Absent,
                        ["DeferQualityUpdatesPeriodInDays"] = Absent,
                        ["PauseFeatureUpdatesStartTime"] = Absent,
                        ["PauseFeatureUpdatesEndTime"] = Absent,
                        ["PauseQualityUpdatesStartTime"] = Absent,
                        ["PauseQualityUpdatesEndTime"] = Absent,
                        ["PauseUpdatesStartTime"] = Absent,
                        ["PauseUpdatesExpiryTime"] = Absent,
                        ["PausedQualityDate"] = Absent,
                        ["PausedFeatureDate"] = Absent,
                        ["FlightSettingsMaxPauseDays"] = Absent,
                        ["NoAUShutdownOption"] = Absent,
                        ["AlwaysAutoRebootAtScheduledTime"] = Absent,
                        ["AutoInstallMinorUpdates"] = Absent,
                        ["UseWUServer"] = Of(0),
                        ["PausedFeatureStatus"] = Absent,
                        ["PausedQualityStatus"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "updates-delivery-optimization",
            Display = new()
            {
                Name = "Delivery Optimization",
                Description = "Share downloaded updates with other PCs on your network or the internet to reduce bandwidth usage",
                GroupName = "Delivery & Store",
                Icon = MaterialIcons.ShareVariant,
            },
            Targets = new Target[]
            {
                new RegTarget("DODownloadMode", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization" }, "DODownloadMode", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Windows Default",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DODownloadMode"] = Absent },
                },
                new SettingState
                {
                    Label = "Devices on LAN Only",
                    Set = new Dictionary<string, StateValue> { ["DODownloadMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Devices on LAN and Internet",
                    Set = new Dictionary<string, StateValue> { ["DODownloadMode"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["DODownloadMode"] = Of(99) },
                },
            },
        },
        new()
        {
            Id = "updates-latest-updates",
            Display = new()
            {
                Name = "Get the latest updates as soon as they're available",
                Description = "Be among the first to get the latest non-security updates, fixes, and improvements as they roll out",
                GroupName = "Update Behavior",
                Icon = MaterialIcons.BullhornVariant,
            },
            Targets = new Target[]
            {
                new RegTarget("IsContinuousInnovationOptedIn", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "IsContinuousInnovationOptedIn", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsContinuousInnovationOptedIn"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IsContinuousInnovationOptedIn"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "updates-other-products",
            Display = new()
            {
                Name = "Receive updates for other Microsoft products",
                Description = "Get Microsoft Office and other updates together with Windows updates",
                GroupName = "Update Behavior",
                Icon = MaterialIcons.ArchiveSync,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("AllowMUUpdateService", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\WindowsUpdate\UX\Settings" }, "AllowMUUpdateService", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["AllowMUUpdateService"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowMUUpdateService"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "updates-restart-asap",
            Display = new()
            {
                Name = "Get me up to date",
                Description = "Restart as soon as possible (even during active hours) to finish updating",
                GroupName = "Update Behavior",
                Icon = MaterialIcons.Restart,
            },
            Targets = new Target[]
            {
                new RegTarget("IsExpedited", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\WindowsUpdate\UX\Settings" }, "IsExpedited", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsExpedited"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IsExpedited"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "updates-restart-options",
            Display = new()
            {
                Name = "Automatic Restart After Updates",
                Description = "Allow Windows to automatically restart your PC after installing updates when you are logged in",
                GroupName = "Update Behavior",
                Icon = MaterialIcons.RestartOff,
            },
            Targets = new Target[]
            {
                new RegTarget("NoAutoRebootWithLoggedOnUsers", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" }, "NoAutoRebootWithLoggedOnUsers", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["NoAutoRebootWithLoggedOnUsers"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NoAutoRebootWithLoggedOnUsers"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "updates-notification-level",
            Display = new()
            {
                Name = "Update Notifications",
                Description = "Show or hide notifications about available updates and update progress",
                GroupName = "Update Behavior",
                Icon = MaterialIcons.BellPlus,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("SetUpdateNotificationLevel", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" }, "SetUpdateNotificationLevel", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["SetUpdateNotificationLevel"] = Of(2) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SetUpdateNotificationLevel"] = Absent },
                },
            },
        },
        new()
        {
            Id = "updates-restart-notification",
            Display = new()
            {
                Name = "Notify me when a restart is required to finish updating",
                Description = "Show notification when your device requires a restart to finish updating",
                GroupName = "Update Behavior",
                Icon = MaterialIcons.RestartAlert,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("RestartNotificationsAllowed2", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\WindowsUpdate\UX\Settings" }, "RestartNotificationsAllowed2", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["RestartNotificationsAllowed2"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["RestartNotificationsAllowed2"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "updates-metered-connection",
            Display = new()
            {
                Name = "Download updates over metered connections",
                Description = "Allow Windows to download updates when using mobile hotspots or data-limited connections",
                GroupName = "Update Behavior",
                Icon = MaterialIcons.Connection,
            },
            Targets = new Target[]
            {
                new RegTarget("AllowAutoWindowsUpdateDownloadOverMeteredNetwork", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings" }, "AllowAutoWindowsUpdateDownloadOverMeteredNetwork", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowAutoWindowsUpdateDownloadOverMeteredNetwork"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowAutoWindowsUpdateDownloadOverMeteredNetwork"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "updates-driver-controls",
            Display = new()
            {
                Name = "Driver Updates via Windows Update",
                Description = "Include hardware driver updates when downloading and installing Windows Updates",
                GroupName = "Update Behavior",
                Icon = MaterialIcons.PackageVariantClosedMinus,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ExcludeWUDriversInQualityUpdate", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" }, "ExcludeWUDriversInQualityUpdate", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ExcludeWUDriversInQualityUpdate"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ExcludeWUDriversInQualityUpdate"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "updates-driver-coinstallers",
            Display = new()
            {
                Name = "Driver Co-Installers",
                Description = "Allows hardware vendors to install companion software alongside device drivers. Disabling this prevents bloatware like Razer Synapse, printer utilities, and other vendor software from being automatically installed when you plug in devices. Your hardware will still work normally with standard drivers.",
                GroupName = "Update Behavior",
                Icon = MaterialIcons.PackageVariantRemove,
                AddedInVersion = "25.04.08",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("DisableCoInstallers", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Installer" }, "DisableCoInstallers", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableCoInstallers"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableCoInstallers"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "updates-store-auto-download",
            Display = new()
            {
                Name = "Auto Update Microsoft Store Apps",
                Description = "Automatically download and install updates for apps from the Microsoft Store",
                GroupName = "Delivery & Store",
                Icon = FluentIcons.StoreMicrosoft,
            },
            Targets = new Target[]
            {
                new RegTarget("AutoDownload", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\WindowsStore", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore" }, "AutoDownload", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AutoDownload"] = Of(4).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AutoDownload"] = Of(2) },
                },
            },
        },
    };
}

using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Optimize.Catalogs;

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
            // Disabled is enforced by a filesystem DLL rename), so a custom detector ranks them. Labels must equal the
            // States below.
            Detector = new UpdatePolicyDetector(
                LocKey.Setting.UpdatesPolicyMode.Option0,
                LocKey.Setting.UpdatesPolicyMode.Option1,
                LocKey.Setting.UpdatesPolicyMode.Option2,
                LocKey.Setting.UpdatesPolicyMode.Option3),
            Display = new()
            {
                Name = LocKey.Setting.UpdatesPolicyMode.Name,
                Description = LocKey.Setting.UpdatesPolicyMode.Description,
                GroupName = LocKey.SettingGroup.UpdatePolicy,
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
                    Label = LocKey.Setting.UpdatesPolicyMode.Option0,
                    Tooltip = LocKey.Setting.UpdatesPolicyMode.OptionTooltip0,
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
                    Label = LocKey.Setting.UpdatesPolicyMode.Option1,
                    Tooltip = LocKey.Setting.UpdatesPolicyMode.OptionTooltip1,
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
                    Label = LocKey.Setting.UpdatesPolicyMode.Option2,
                    Tooltip = LocKey.Setting.UpdatesPolicyMode.OptionTooltip2,
                    Warning = LocKey.Setting.UpdatesPolicyMode.OptionWarning2,
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
                    Label = LocKey.Setting.UpdatesPolicyMode.Option3,
                    Tooltip = LocKey.Setting.UpdatesPolicyMode.OptionTooltip3,
                    Warning = LocKey.Setting.UpdatesPolicyMode.OptionWarning3,
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
                Name = LocKey.Setting.UpdatesDeliveryOptimization.Name,
                Description = LocKey.Setting.UpdatesDeliveryOptimization.Description,
                GroupName = LocKey.SettingGroup.DeliveryStore,
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
                    Label = LocKey.Setting.UpdatesDeliveryOptimization.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DODownloadMode"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.UpdatesDeliveryOptimization.Option1,
                    Set = new Dictionary<string, StateValue> { ["DODownloadMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.UpdatesDeliveryOptimization.Option2,
                    Set = new Dictionary<string, StateValue> { ["DODownloadMode"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
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
                Name = LocKey.Setting.UpdatesLatestUpdates.Name,
                Description = LocKey.Setting.UpdatesLatestUpdates.Description,
                GroupName = LocKey.SettingGroup.UpdateBehavior,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsContinuousInnovationOptedIn"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.UpdatesOtherProducts.Name,
                Description = LocKey.Setting.UpdatesOtherProducts.Description,
                GroupName = LocKey.SettingGroup.UpdateBehavior,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["AllowMUUpdateService"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowMUUpdateService"] = Of(0) },
                    ResetSet = new Dictionary<string, StateValue> { ["AllowMUUpdateService"] = Absent },
                },
            },
        },
        new()
        {
            Id = "updates-restart-asap",
            Display = new()
            {
                Name = LocKey.Setting.UpdatesRestartAsap.Name,
                Description = LocKey.Setting.UpdatesRestartAsap.Description,
                GroupName = LocKey.SettingGroup.UpdateBehavior,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["IsExpedited"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IsExpedited"] = Of(0).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "updates-restart-options",
            Display = new()
            {
                Name = LocKey.Setting.UpdatesRestartOptions.Name,
                Description = LocKey.Setting.UpdatesRestartOptions.Description,
                GroupName = LocKey.SettingGroup.UpdateBehavior,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["NoAutoRebootWithLoggedOnUsers"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.UpdatesNotificationLevel.Name,
                Description = LocKey.Setting.UpdatesNotificationLevel.Description,
                GroupName = LocKey.SettingGroup.UpdateBehavior,
                Icon = MaterialIcons.BellPlus,
                IsSubjectivePreference = true,
            },
            // "Display options for update notifications" (WindowsUpdate.admx) writes THREE values and the names
            // are not interchangeable: the <policy> element's own valueName is the 0/1 enabled FLAG, the <enum>
            // child carries the 0/1/2 LEVEL, and the <boolean> child is the active-hours box. Writing the level
            // into the flag yields a policy that reads as configured and suppresses nothing. class="Machine", so
            // HKLM only; the HKCU copy older builds wrote is kept purely so we can delete it again.
            Targets = new Target[]
            {
                new RegTarget("SetUpdateNotificationLevel", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" }, "SetUpdateNotificationLevel", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("UpdateNotificationLevel", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" }, "UpdateNotificationLevel", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("NoUpdateNotificationsDuringActiveHours", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" }, "NoUpdateNotificationsDuringActiveHours", RegistryValueKind.DWord) { IsGroupPolicy = true, ApplyOnly = true },
                new RegTarget("LegacyHkcuNotificationLevel", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" }, "SetUpdateNotificationLevel", RegistryValueKind.DWord) { IsGroupPolicy = true, ApplyOnly = true },
            },
            // No IsFallback: a machine still carrying the old malformed SetUpdateNotificationLevel=2 matches no
            // state and honestly reads Custom; a fallback would claim it sits at the Windows default. Every state
            // must carry an entry for EVERY target - a missing key is silently skipped by the detection engine and
            // produces a false match, and nothing gates that.
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.UpdatesNotificationLevel.Option0,
                    Roles = new[] { StateRole.WindowsDefault, StateRole.Recommended },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["SetUpdateNotificationLevel"] = Absent,
                        ["UpdateNotificationLevel"] = Absent,
                        ["NoUpdateNotificationsDuringActiveHours"] = Absent,
                        ["LegacyHkcuNotificationLevel"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.UpdatesNotificationLevel.Option1,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["SetUpdateNotificationLevel"] = Of(1),
                        ["UpdateNotificationLevel"] = Of(1),
                        ["NoUpdateNotificationsDuringActiveHours"] = Of(0).OrAbsent(),
                        ["LegacyHkcuNotificationLevel"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.UpdatesNotificationLevel.Option2,
                    Warning = LocKey.Setting.UpdatesNotificationLevel.OptionWarning2,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["SetUpdateNotificationLevel"] = Of(1),
                        ["UpdateNotificationLevel"] = Of(2),
                        ["NoUpdateNotificationsDuringActiveHours"] = Of(0).OrAbsent(),
                        ["LegacyHkcuNotificationLevel"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "updates-restart-notification",
            Display = new()
            {
                Name = LocKey.Setting.UpdatesRestartNotification.Name,
                Description = LocKey.Setting.UpdatesRestartNotification.Description,
                GroupName = LocKey.SettingGroup.UpdateBehavior,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["RestartNotificationsAllowed2"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["RestartNotificationsAllowed2"] = Of(0) },
                    ResetSet = new Dictionary<string, StateValue> { ["RestartNotificationsAllowed2"] = Absent },
                },
            },
        },
        new()
        {
            Id = "updates-metered-connection",
            Display = new()
            {
                Name = LocKey.Setting.UpdatesMeteredConnection.Name,
                Description = LocKey.Setting.UpdatesMeteredConnection.Description,
                GroupName = LocKey.SettingGroup.UpdateBehavior,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["AllowAutoWindowsUpdateDownloadOverMeteredNetwork"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowAutoWindowsUpdateDownloadOverMeteredNetwork"] = Of(0).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "updates-driver-controls",
            Display = new()
            {
                Name = LocKey.Setting.UpdatesDriverControls.Name,
                Description = LocKey.Setting.UpdatesDriverControls.Description,
                GroupName = LocKey.SettingGroup.UpdateBehavior,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ExcludeWUDriversInQualityUpdate"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.UpdatesDriverCoinstallers.Name,
                Description = LocKey.Setting.UpdatesDriverCoinstallers.Description,
                GroupName = LocKey.SettingGroup.UpdateBehavior,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableCoInstallers"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.UpdatesStoreAutoDownload.Name,
                Description = LocKey.Setting.UpdatesStoreAutoDownload.Description,
                GroupName = LocKey.SettingGroup.DeliveryStore,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AutoDownload"] = Of(4).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AutoDownload"] = Of(2) },
                },
            },
        },
    };
}

using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Optimize.Catalogs;

public static class NotificationOptimizationsCatalog
{
    public const string FeatureId = FeatureIds.Notifications;

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "windows-pushnotifications",
            Display = new()
            {
                Name = LocKey.Setting.WindowsPushnotifications.Name,
                Description = LocKey.Setting.WindowsPushnotifications.Description,
                Icon = FluentIcons.AlertUrgent,
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartService("WpnUserService*") },
            Targets = new Target[]
            {
                new RegTarget("ToastEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\PushNotifications" }, "ToastEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ToastEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ToastEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-sound",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsSound.Name,
                Description = LocKey.Setting.NotificationsSound.Description,
                Icon = FluentIcons.Speaker2,
                IsSubjectivePreference = true,
            },
            UiParentId = "windows-pushnotifications",
            EnabledWhen = new("windows-pushnotifications", new[] { LocKey.Common.Enabled }),
            Targets = new Target[]
            {
                new RegTarget("NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" }, "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-toast-above-lock",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsToastAboveLock.Name,
                Description = LocKey.Setting.NotificationsToastAboveLock.Description,
                Icon = FluentIcons.PhoneLock,
                IsSubjectivePreference = true,
            },
            UiParentId = "windows-pushnotifications",
            EnabledWhen = new("windows-pushnotifications", new[] { LocKey.Common.Enabled }),
            Targets = new Target[]
            {
                new RegTarget("NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" }, "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", RegistryValueKind.DWord),
                new RegTarget("LockScreenToastEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\PushNotifications" }, "LockScreenToastEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK"] = Of(1).OrAbsent(),
                        ["LockScreenToastEnabled"] = Of(1).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Links = new[] { new Link("privacy-lock-screen", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK"] = Of(0),
                        ["LockScreenToastEnabled"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "notifications-critical-toast-above-lock",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsCriticalToastAboveLock.Name,
                Description = LocKey.Setting.NotificationsCriticalToastAboveLock.Description,
                Icon = FluentIcons.CallExclamation,
                IsSubjectivePreference = true,
            },
            UiParentId = "windows-pushnotifications",
            EnabledWhen = new("windows-pushnotifications", new[] { LocKey.Common.Enabled }),
            Targets = new Target[]
            {
                new RegTarget("NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" }, "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Links = new[] { new Link("privacy-lock-screen", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-show-bell-icon",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsShowBellIcon.Name,
                Description = LocKey.Setting.NotificationsShowBellIcon.Description,
                Icon = FluentIcons.AlertOn,
                IsSubjectivePreference = true,
            },
            UiParentId = "windows-pushnotifications",
            // NO EnabledWhen, unlike its three siblings: ShowNotificationIcon is a tray-icon
            // preference Explorer honours whether or not toasts are on, so it stays usable while
            // notifications are off. Nesting is where the card is drawn, not a claim about meaning.
            Targets = new Target[]
            {
                new RegTarget("ShowNotificationIcon", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowNotificationIcon", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowNotificationIcon"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowNotificationIcon"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-welcome-experience",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsWelcomeExperience.Name,
                Description = LocKey.Setting.NotificationsWelcomeExperience.Description,
                GroupName = LocKey.SettingGroup.AdditionalSettings,
                Icon = FluentIcons.HandWave,
            },
            Targets = new Target[]
            {
                new RegTarget("SubscribedContent-310093Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SubscribedContent-310093Enabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-310093Enabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-310093Enabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-system-setting-engagement",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsSystemSettingEngagement.Name,
                Description = LocKey.Setting.NotificationsSystemSettingEngagement.Description,
                GroupName = LocKey.SettingGroup.AdditionalSettings,
                Icon = FluentIcons.Wand,
            },
            Targets = new Target[]
            {
                new RegTarget("ScoobeSystemSettingEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement" }, "ScoobeSystemSettingEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ScoobeSystemSettingEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ScoobeSystemSettingEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-tips-suggestions",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsTipsSuggestions.Name,
                Description = LocKey.Setting.NotificationsTipsSuggestions.Description,
                GroupName = LocKey.SettingGroup.AdditionalSettings,
                Icon = FluentIcons.Lightbulb,
            },
            Targets = new Target[]
            {
                new RegTarget("SubscribedContent-338389Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SubscribedContent-338389Enabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-338389Enabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-338389Enabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-system-pane-suggestions",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsSystemPaneSuggestions.Name,
                Description = LocKey.Setting.NotificationsSystemPaneSuggestions.Description,
                GroupName = LocKey.SettingGroup.AdditionalSettings,
                Icon = FluentIcons.CommentBadge,
            },
            Targets = new Target[]
            {
                new RegTarget("SystemPaneSuggestionsEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SystemPaneSuggestionsEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SystemPaneSuggestionsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SystemPaneSuggestionsEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-banner-duration",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsBannerDuration.Name,
                Description = LocKey.Setting.NotificationsBannerDuration.Description,
                GroupName = LocKey.SettingGroup.SystemNotifications,
                Icon = FluentIcons.AlertUrgent,
                IsSubjectivePreference = true,
                AddedInVersion = "26.11.24",
            },
            Availability = new() { Builds = new[] { BuildRange.Between(26300, int.MaxValue) } },
            Targets = new Target[] { new MessageDurationTarget() },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.NotificationsBannerDuration.Option0,
                    Set = new Dictionary<string, StateValue> { ["MessageDuration"] = Of(5) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.NotificationsBannerDuration.Option1,
                    Set = new Dictionary<string, StateValue> { ["MessageDuration"] = Of(7) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.NotificationsBannerDuration.Option2,
                    Set = new Dictionary<string, StateValue> { ["MessageDuration"] = Of(15) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.NotificationsBannerDuration.Option3,
                    Set = new Dictionary<string, StateValue> { ["MessageDuration"] = Of(30) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.NotificationsBannerDuration.Option4,
                    Set = new Dictionary<string, StateValue> { ["MessageDuration"] = Of(60) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.NotificationsBannerDuration.Option5,
                    Set = new Dictionary<string, StateValue> { ["MessageDuration"] = Of(300) },
                },
            },
        },
        new()
        {
            Id = "notifications-usb-errors",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsUsbErrors.Name,
                Description = LocKey.Setting.NotificationsUsbErrors.Description,
                GroupName = LocKey.SettingGroup.SystemNotifications,
                Icon = FluentIcons.Usb,
                AddedInVersion = "26.11.24",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Between(26300, int.MaxValue) } },
            Targets = new Target[]
            {
                new RegTarget("NotifyOnUsbErrors", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Shell\USB" }, "NotifyOnUsbErrors", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Between(26300, 26300) } } },
                    Set = new Dictionary<string, StateValue> { ["NotifyOnUsbErrors"] = Of(1).OrAbsent() },
                    ResetSet = new Dictionary<string, StateValue> { ["NotifyOnUsbErrors"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Set = new Dictionary<string, StateValue> { ["NotifyOnUsbErrors"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-capability-access",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsCapabilityAccess.Name,
                Description = LocKey.Setting.NotificationsCapabilityAccess.Description,
                GroupName = LocKey.SettingGroup.SystemNotifications,
                Icon = FluentIcons.WarningLockOpen,
            },
            Targets = new Target[]
            {
                new RegTarget("Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.CapabilityAccess" }, "Enabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-startup-app",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsStartupApp.Name,
                Description = LocKey.Setting.NotificationsStartupApp.Description,
                GroupName = LocKey.SettingGroup.SystemNotifications,
                Icon = FluentIcons.AlertBadge,
            },
            Targets = new Target[]
            {
                new RegTarget("Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.StartupApp" }, "Enabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(0).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "notifications-clock-change",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsClockChange.Name,
                Description = LocKey.Setting.NotificationsClockChange.Description,
                GroupName = LocKey.SettingGroup.SystemNotifications,
                Icon = FluentIcons.ClockWarning,
            },
            Targets = new Target[]
            {
                new RegTarget("DstNotification", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "DstNotification", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DstNotification"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DstNotification"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-app-location-request",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsAppLocationRequest.Name,
                Description = LocKey.Setting.NotificationsAppLocationRequest.Description,
                GroupName = LocKey.SettingGroup.PrivacyNotifications,
                Icon = FluentIcons.Location,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowGlobalPrompts", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location" }, "ShowGlobalPrompts", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowGlobalPrompts"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowGlobalPrompts"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-windows-security",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsWindowsSecurity.Name,
                Description = LocKey.Setting.NotificationsWindowsSecurity.Description,
                GroupName = LocKey.SettingGroup.SecurityNotifications,
                Icon = FluentIcons.ShieldError,
            },
            Targets = new Target[]
            {
                new RegTarget("DisableNotifications", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows Defender Security Center\Notifications", @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows Defender Security Center\Notifications", @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows Defender Security Center\Notifications" }, "DisableNotifications", RegistryValueKind.DWord),
                new RegTarget("DisableEnhancedNotifications", new[] { @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows Defender Security Center\Notifications", @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows Defender Security Center\Notifications" }, "DisableEnhancedNotifications", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DisableNotifications"] = Of(0).OrAbsent(),
                        ["DisableEnhancedNotifications"] = Of(0).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DisableNotifications"] = Of(1),
                        ["DisableEnhancedNotifications"] = Of(1),
                    },
                },
            },
        },
        new()
        {
            Id = "notifications-security-maintenance",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsSecurityMaintenance.Name,
                Description = LocKey.Setting.NotificationsSecurityMaintenance.Description,
                GroupName = LocKey.SettingGroup.SecurityNotifications,
                Icon = FluentIcons.ShieldBadge,
            },
            Targets = new Target[]
            {
                new RegTarget("Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance" }, "Enabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(0) },
                },
            },
        },
    };
}

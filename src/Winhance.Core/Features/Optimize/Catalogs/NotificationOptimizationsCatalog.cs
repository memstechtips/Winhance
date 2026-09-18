using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Optimize.Catalogs;

public static class NotificationOptimizationsCatalog
{
    public const string FeatureId = FeatureIds.Notifications;
    public const string FeatureName = "Notifications";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "windows-pushnotifications",
            Display = new()
            {
                Name = LocKey.Setting.WindowsPushnotifications.Name,
                Description = LocKey.Setting.WindowsPushnotifications.Description,
                Icon = MaterialIcons.BellAlert,
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
                Icon = MaterialIcons.VolumeHigh,
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
                Icon = MaterialIcons.CellphoneLock,
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
                Icon = MaterialIcons.PhoneAlert,
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
                Icon = MaterialIcons.BellCheck,
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
                Icon = MaterialIcons.HumanGreeting,
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
                Icon = MaterialIcons.AutoFix,
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
                Icon = MaterialIcons.LightbulbOnOutline,
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
                Icon = MaterialIcons.MessageBadge,
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
            Id = "notifications-capability-access",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsCapabilityAccess.Name,
                Description = LocKey.Setting.NotificationsCapabilityAccess.Description,
                GroupName = LocKey.SettingGroup.SystemNotifications,
                Icon = MaterialIcons.LockOpenAlertOutline,
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
                Icon = MaterialIcons.ArchiveAlert,
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
            Id = "notifications-app-location-request",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsAppLocationRequest.Name,
                Description = LocKey.Setting.NotificationsAppLocationRequest.Description,
                GroupName = LocKey.SettingGroup.PrivacyNotifications,
                Icon = MaterialIcons.MapMarker,
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
            Id = "notifications-clock-change",
            Display = new()
            {
                Name = LocKey.Setting.NotificationsClockChange.Name,
                Description = LocKey.Setting.NotificationsClockChange.Description,
                GroupName = LocKey.SettingGroup.SystemNotifications,
                Icon = MaterialIcons.ClockAlertOutline,
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
                Icon = MaterialIcons.ShieldSync,
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

using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;

namespace Winhance.Core.Features.Optimize.Models;

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
                Name = "Show Notifications",
                Description = "Get notifications from apps and other senders in Windows",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ToastEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Allow notifications to play sounds",
                Description = "Play audio alerts when notifications arrive from apps and system senders",
                Icon = MaterialIcons.VolumeHigh,
                IsSubjectivePreference = true,
            },
            UiParentId = "windows-pushnotifications",
            Targets = new Target[]
            {
                new RegTarget("NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" }, "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Show notifications on the lock screen",
                Description = "Display toast notifications on the lock screen when your device is locked",
                Icon = MaterialIcons.CellphoneLock,
                IsSubjectivePreference = true,
            },
            UiParentId = "windows-pushnotifications",
            Links = new[] { new Link("privacy-lock-screen", LinkKind.Requires, "Enabled") },
            Targets = new Target[]
            {
                new RegTarget("NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" }, "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", RegistryValueKind.DWord),
                new RegTarget("LockScreenToastEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\PushNotifications" }, "LockScreenToastEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK"] = Of(1).OrAbsent(),
                        ["LockScreenToastEnabled"] = Of(1).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
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
                Name = "Show reminders and incoming VoIP calls on the lock screen",
                Description = "Display critical notifications like reminders and VoIP calls when your device is locked",
                Icon = MaterialIcons.PhoneAlert,
                IsSubjectivePreference = true,
            },
            UiParentId = "windows-pushnotifications",
            Links = new[] { new Link("privacy-lock-screen", LinkKind.Requires, "Enabled") },
            Targets = new Target[]
            {
                new RegTarget("NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" }, "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-show-bell-icon",
            Display = new()
            {
                Name = "Show notification bell icon",
                Description = "Display the notification bell icon in the system tray",
                Icon = MaterialIcons.BellCheck,
                IsSubjectivePreference = true,
            },
            UiParentId = "windows-pushnotifications",
            Targets = new Target[]
            {
                new RegTarget("ShowNotificationIcon", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowNotificationIcon", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowNotificationIcon"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Show the Windows welcome experience after updates",
                Description = "Show what's new and suggested after updates and when signed in",
                GroupName = "Additional Settings",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-310093Enabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Suggest ways to get the most out of Windows and finish setting up this device",
                Description = "Show suggestions to help you complete device setup and optimize Windows features",
                GroupName = "Additional Settings",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ScoobeSystemSettingEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Get tips and suggestions when using Windows",
                Description = "Show helpful tips and suggestions while using Windows",
                GroupName = "Additional Settings",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-338389Enabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Show suggestions in Notification Center",
                Description = "Display helpful suggestions in the Action Center and Notification Center",
                GroupName = "Additional Settings",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SystemPaneSuggestionsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Capability Access Notifications",
                Description = "Show notifications when apps request access to system capabilities and permissions",
                GroupName = "System Notifications",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Startup App Notifications",
                Description = "Show notifications when apps are added to your Windows startup list",
                GroupName = "System Notifications",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "notifications-app-location-request",
            Display = new()
            {
                Name = "Notify when apps request location",
                Description = "Show notifications when apps attempt to access your location information",
                GroupName = "Privacy Notifications",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowGlobalPrompts"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Clock Change Notifications",
                Description = "Show notifications when daylight saving time changes occur",
                GroupName = "System Notifications",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DstNotification"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Windows Security Notifications",
                Description = "Show all notifications from Windows Security about threats, scans, and protection status",
                GroupName = "Security Notifications",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DisableNotifications"] = Of(0).OrAbsent(),
                        ["DisableEnhancedNotifications"] = Of(0).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Security and Maintenance Notifications",
                Description = "Show notifications from the Security and Maintenance Action Center",
                GroupName = "Security Notifications",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Enabled"] = Of(0) },
                },
            },
        },
    };
}

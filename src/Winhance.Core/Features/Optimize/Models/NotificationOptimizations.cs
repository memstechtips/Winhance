using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class NotificationOptimizations
{
    public static SettingGroup GetNotificationOptimizations()
    {
        return new SettingGroup
        {
            Name = "Notifications",
            FeatureId = FeatureIds.Notification,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "windows-pushnotifications",
                    Name = "Show Notifications",
                    Description = "Get notifications from apps and other senders in Windows",
                    InputType = InputType.Toggle,
                    Icon = "BellAlert",
                    RestartService = "WpnUserService*",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                            ValueName = "ToastEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-sound",
                    Name = "Allow notifications to play sounds",
                    Description = "Play audio alerts when notifications arrive from apps and system senders",
                    Icon = "VolumeHigh",
                    InputType = InputType.Toggle,
                    ParentSettingId = "windows-pushnotifications",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                            ValueName = "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-toast-above-lock",
                    Name = "Show notifications on the lock screen",
                    Description = "Display toast notifications on the lock screen when your device is locked",
                    IconPack = "MaterialDesign",
                    Icon = "ScreenLockLandscapeOutline",
                    InputType = InputType.Toggle,
                    ParentSettingId = "windows-pushnotifications",
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "notifications-toast-above-lock",
                            RequiredSettingId = "privacy-lock-screen",
                            RequiredModule = "PrivacyOptimizations",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                            ValueName = "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                            ValueName = "LockScreenToastEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-critical-toast-above-lock",
                    Name = "Show reminders and incoming VoIP calls on the lock screen",
                    Description = "Display critical notifications like reminders and VoIP calls when your device is locked",
                    Icon = "PhoneAlert",
                    InputType = InputType.Toggle,
                    ParentSettingId = "windows-pushnotifications",
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "notifications-critical-toast-above-lock",
                            RequiredSettingId = "privacy-lock-screen",
                            RequiredModule = "PrivacyOptimizations",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                            ValueName = "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-show-bell-icon",
                    Name = "Show notification bell icon",
                    Description = "Display the notification bell icon in the system tray",
                    Icon = "BellCheck",
                    InputType = InputType.Toggle,
                    ParentSettingId = "windows-pushnotifications",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowNotificationIcon",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-welcome-experience",
                    Name = "Show the Windows welcome experience after updates",
                    Description = "Show what's new and suggested after updates and when signed in",
                    GroupName = "Additional Settings",
                    Icon = "HumanGreeting",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "notifications-welcome-experience",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredModule = "PrivacyOptimizations",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-310093Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-system-setting-engagement",
                    Name = "Suggest ways to get the most out of Windows and finish setting up this device",
                    Description = "Show suggestions to help you complete device setup and optimize Windows features",
                    GroupName = "Additional Settings",
                    IconPack = "MaterialDesign",
                    Icon = "AutoAwesomeRound",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement",
                            ValueName = "ScoobeSystemSettingEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-tips-suggestions",
                    Name = "Get tips and suggestions when using Windows",
                    Description = "Show helpful tips and suggestions while using Windows",
                    GroupName = "Additional Settings",
                    IconPack = "MaterialDesign",
                    Icon = "TipsAndUpdatesOutline",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "notifications-tips-suggestions",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredModule = "PrivacyOptimizations",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338389Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-system-pane-suggestions",
                    Name = "Show suggestions in Notification Center",
                    Description = "Display helpful suggestions in the Action Center and Notification Center",
                    GroupName = "Additional Settings",
                    IconPack = "MaterialDesign",
                    Icon = "Doorbell",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "notifications-system-pane-suggestions",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredModule = "PrivacyOptimizations",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SystemPaneSuggestionsEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-capability-access",
                    Name = "Capability Access Notifications",
                    Description = "Show notifications when apps request access to system capabilities and permissions",
                    GroupName = "System Notifications",
                    Icon = "LockOpenAlertOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.CapabilityAccess",
                            ValueName = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-startup-app",
                    Name = "Startup App Notifications",
                    Description = "Show notifications when apps are added to your Windows startup list",
                    GroupName = "System Notifications",
                    Icon = "ArchiveAlert",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.StartupApp",
                            ValueName = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-app-location-request",
                    Name = "Notify when apps request location",
                    Description = "Show notifications when apps attempt to access your location information",
                    GroupName = "Privacy Notifications",
                    Icon = "MapMarker",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                            ValueName = "ShowGlobalPrompts",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-clock-change",
                    Name = "Clock Change Notifications",
                    Description = "Show notifications when daylight saving time changes occur",
                    GroupName = "System Notifications",
                    Icon = "ClockAlertOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "DstNotification",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-windows-security",
                    Name = "Windows Security Notifications",
                    Description = "Show all notifications from Windows Security about threats, scans, and protection status",
                    GroupName = "Security Notifications",
                    IconPack = "Lucide",
                    Icon = "ShieldAlert",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableEnhancedNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableEnhancedNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-security-maintenance",
                    Name = "Security and Maintenance Notifications",
                    Description = "Show notifications from the Security and Maintenance Action Center",
                    GroupName = "Security Notifications",
                    Icon = "ShieldSync",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance",
                            ValueName = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using System.Collections.Generic;

namespace Winhance.Core.Features.Optimize.Models;

public static class NotificationOptimizations
{
    public static OptimizationGroup GetNotificationOptimizations()
    {
        return new OptimizationGroup
        {
            Name = "Notifications",
            Category = OptimizationCategory.Notifications,
            Settings = new List<OptimizationSetting>
            {
                new OptimizationSetting
                {
                    Id = "notifications-toast",
                    Name = "Windows Notifications",
                    Description = "Controls toast notifications",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PushNotifications",
                            Name = "ToastEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls toast notifications",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        }
                    }
                },
                new OptimizationSetting
                {
                    Id = "notifications-sound",
                    Name = "Notification Sounds",
                    Description = "Controls notification sounds",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings",
                            Name = "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls notification sounds",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        }
                    }
                },
                new OptimizationSetting
                {
                    Id = "notifications-toast-above-lock",
                    Name = "Notifications On Lock Screen",
                    Description = "Controls notifications above lock screen",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings",
                            Name = "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls notifications on lock screen",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        },
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PushNotifications",
                            Name = "LockScreenToastEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls notifications on lock screen",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All
                },
                new OptimizationSetting
                {
                    Id = "notifications-critical-toast-above-lock",
                    Name = "Show Reminders and VoIP Calls Notifications",
                    Description = "Controls critical notifications above lock screen",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings",
                            Name = "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls critical notifications above lock screen",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        }
                    }
                },
                new OptimizationSetting
                {
                    Id = "notifications-security-maintenance",
                    Name = "Security and Maintenance Notifications",
                    Description = "Controls security and maintenance notifications",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\Windows.SystemToast.SecurityAndMaintenance",
                            Name = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls security and maintenance notifications",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        }
                    }
                },
                new OptimizationSetting
                {
                    Id = "notifications-capability-access",
                    Name = "Capability Access Notifications",
                    Description = "Controls capability access notifications",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\Windows.SystemToast.CapabilityAccess",
                            Name = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls capability access notifications",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        }
                    }
                },
                new OptimizationSetting
                {
                    Id = "notifications-startup-app",
                    Name = "Startup App Notifications",
                    Description = "Controls startup app notifications",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\Windows.SystemToast.StartupApp",
                            Name = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls startup app notifications",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        }
                    }
                },
                new OptimizationSetting
                {
                    Id = "notifications-system-setting-engagement",
                    Name = "System Setting Engagement Notifications",
                    Description = "Controls system setting engagement notifications",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\UserProfileEngagement",
                            Name = "ScoobeSystemSettingEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls system setting engagement notifications",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        }
                    }
                },
                new OptimizationSetting
                {
                    Id = "notifications-app-location-request",
                    Name = "Notify when apps request location",
                    Description = "Controls wheter notifications are shown for location requests",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "Privacy Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location",
                            Name = "ShowGlobalPrompts",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls wheter notifications are shown for location requests",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        }
                    }
                },
                new OptimizationSetting
                {
                    Id = "notifications-windows-security",
                    Name = "Windows Security Notifications",
                    Description = "Controls whether Windows Security notifications are shown",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "Security Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\Windows Defender Security Center\\Notifications",
                            Name = "DisableNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description = "Controls whether Windows Security Center notifications are shown",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        },
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows Defender Security Center\\Notifications",
                            Name = "DisableNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description = "Controls whether Windows Defender Security Center notifications are shown",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true
                        },
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows Defender Security Center\\Notifications",
                            Name = "DisableEnhancedNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description = "Controls whether Windows Defender Security Center notifications are shown",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All
                },
                new OptimizationSetting
                {
                    Id = "notifications-clock-change",
                    Name = "Clock Change Notifications",
                    Description = "Controls clock change notifications",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Control Panel\\Desktop",
                            Name = "DstNotification",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls clock change notifications",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true
                        }
                    }
                }
            }
        };
    }
}

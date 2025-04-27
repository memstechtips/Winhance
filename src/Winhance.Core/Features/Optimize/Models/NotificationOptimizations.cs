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
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                    {
                        Category = "Notifications",
                        Hive = RegistryHive.CurrentUser,
                        SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PushNotifications",
                    Name = "ToastEnabled",
                        RecommendedValue = 0,  // For backward compatibility
                        EnabledValue = 1,      // When toggle is ON, toast notifications are enabled
                        DisabledValue = 0,     // When toggle is OFF, toast notifications are disabled
                        ValueType = RegistryValueKind.DWord,
                        DefaultValue = null,   // For backward compatibility
                        Description = "Controls toast notifications"
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
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                    {
                        Category = "Notifications",
                        Hive = RegistryHive.CurrentUser,
                        SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings",
                    Name = "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND",
                        RecommendedValue = 0,  // For backward compatibility
                        EnabledValue = 1,      // When toggle is ON, notification sounds are enabled
                        DisabledValue = 0,     // When toggle is OFF, notification sounds are disabled
                        ValueType = RegistryValueKind.DWord,
                        DefaultValue = null,   // For backward compatibility
                        Description = "Controls notification sounds"
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
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                    {
                        Category = "Notifications",
                        Hive = RegistryHive.CurrentUser,
                        SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings",
                    Name = "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK",
                        RecommendedValue = 0,  // For backward compatibility
                        EnabledValue = 1,      // When toggle is ON, notifications above lock screen are enabled
                        DisabledValue = 0,     // When toggle is OFF, notifications above lock screen are disabled
                        ValueType = RegistryValueKind.DWord,
                        DefaultValue = null,   // For backward compatibility
                        Description = "Controls notifications on lock screen"
                        },
                        new RegistrySetting
                    {
                        Category = "Notifications",
                        Hive = RegistryHive.CurrentUser,
                        SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PushNotifications",
                    Name = "LockScreenToastEnabled",
                        RecommendedValue = 0,  // For backward compatibility
                        EnabledValue = 1,      // When toggle is ON, lock screen notifications are enabled
                        DisabledValue = 0,     // When toggle is OFF, lock screen notifications are disabled
                        ValueType = RegistryValueKind.DWord,
                        DefaultValue = null,   // For backward compatibility
                        Description = "Controls notifications on lock screen"
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
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                    {
                        Category = "Notifications",
                        Hive = RegistryHive.CurrentUser,
                        SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings",
                    Name = "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK",
                        RecommendedValue = 0,  // For backward compatibility
                        EnabledValue = 1,      // When toggle is ON, critical notifications above lock screen are enabled
                        DisabledValue = 0,     // When toggle is OFF, critical notifications above lock screen are disabled
                        ValueType = RegistryValueKind.DWord,
                        DefaultValue = null,   // For backward compatibility
                        Description = "Controls critical notifications above lock screen"
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
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                    {
                        Category = "Notifications",
                        Hive = RegistryHive.CurrentUser,
                        SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\Windows.SystemToast.SecurityAndMaintenance",
                    Name = "Enabled",
                        RecommendedValue = 0,  // For backward compatibility
                        EnabledValue = 1,      // When toggle is ON, security and maintenance notifications are enabled
                        DisabledValue = 0,     // When toggle is OFF, security and maintenance notifications are disabled
                        ValueType = RegistryValueKind.DWord,
                        DefaultValue = null,   // For backward compatibility
                        Description = "Controls security and maintenance notifications"
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
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                    {
                        Category = "Notifications",
                        Hive = RegistryHive.CurrentUser,
                        SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\Windows.SystemToast.CapabilityAccess",
                    Name = "Enabled",
                        RecommendedValue = 0,  // For backward compatibility
                        EnabledValue = 1,      // When toggle is ON, capability access notifications are enabled
                        DisabledValue = 0,     // When toggle is OFF, capability access notifications are disabled
                        ValueType = RegistryValueKind.DWord,
                        DefaultValue = null,   // For backward compatibility
                        Description = "Controls capability access notifications"
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
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                    {
                        Category = "Notifications",
                        Hive = RegistryHive.CurrentUser,
                        SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\Windows.SystemToast.StartupApp",
                    Name = "Enabled",
                        RecommendedValue = 0,  // For backward compatibility
                        EnabledValue = 1,      // When toggle is ON, startup app notifications are enabled
                        DisabledValue = 0,     // When toggle is OFF, startup app notifications are disabled
                        ValueType = RegistryValueKind.DWord,
                        DefaultValue = null,   // For backward compatibility
                        Description = "Controls startup app notifications"
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
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                    {
                        Category = "Notifications",
                        Hive = RegistryHive.CurrentUser,
                        SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\UserProfileEngagement",
                    Name = "ScoobeSystemSettingEnabled",
                        RecommendedValue = 0,  // For backward compatibility
                        EnabledValue = 1,      // When toggle is ON, system setting engagement notifications are enabled
                        DisabledValue = 0,     // When toggle is OFF, system setting engagement notifications are disabled
                        ValueType = RegistryValueKind.DWord,
                        DefaultValue = null,   // For backward compatibility
                        Description = "Controls system setting engagement notifications"
                        }
                    }
                },
                new OptimizationSetting
                {
                    Id = "notifications-clock-change",
                    Name = "Clock Change Notifications",
                    Description = "Controls clock change notifications",
                    Category = OptimizationCategory.Notifications,
                    GroupName = "System Notifications",
                    IsEnabled = false,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Notifications",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Control Panel\\Desktop",
                            Name = "DstNotification",
                            RecommendedValue = 0,  // For backward compatibility
                            EnabledValue = 1,      // When toggle is ON, clock change notifications are enabled
                            DisabledValue = 0,     // When toggle is OFF, clock change notifications are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = null,   // For backward compatibility
                            Description = "Controls clock change notifications"
                        }
                    }
                }
            }
        };
    }
}

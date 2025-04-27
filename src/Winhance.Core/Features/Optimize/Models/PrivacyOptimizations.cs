using Microsoft.Win32;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class PrivacyOptimizations
{
    public static OptimizationGroup GetPrivacyOptimizations()
    {
        return new OptimizationGroup
        {
            Name = "Privacy",
            Category = OptimizationCategory.Privacy,
            Settings = new List<OptimizationSetting>
            {
                // Activity History
                new OptimizationSetting
                {
                    Id = "privacy-activity-history",
                    Name = "Activity History",
                    Description = "Controls activity history tracking",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Activity History",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\System",
                            Name = "PublishUserActivities",
                            EnabledValue = 1,      // When toggle is ON, Activity History is enabled
                            DisabledValue = 0,     // When toggle is OFF, Activity History is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Values for enabling/disabling the setting
                            // When this key doesn't exist, Activity History is enabled
                            AbsenceMeansEnabled = true,
                            // Mark as primary for linked settings
                            IsPrimary = true,
                            // Add RecommendedValue for backward compatibility
                            RecommendedValue = 0
                        }
                    }
                },

                // Personalized Ads (combined setting)
                new OptimizationSetting
                {
                    Id = "privacy-advertising-id",
                    Name = "Personalized Ads",
                    Description = "Controls personalized ads using advertising ID",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Advertising",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo",
                            Name = "Enabled",
                            EnabledValue = 1,      // When toggle is ON, advertising ID is enabled
                            DisabledValue = 0,     // When toggle is OFF, advertising ID is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\AdvertisingInfo",
                            Name = "DisabledByGroupPolicy",
                            EnabledValue = 0,      // When toggle is ON, advertising is NOT disabled by policy
                            DisabledValue = 1,     // When toggle is OFF, advertising is disabled by policy
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 1,
                            DefaultValue = null,
                            // Mark as a Group Policy registry key
                            IsGroupPolicy = true
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All
                },

                // Language List Access
                new OptimizationSetting
                {
                    Id = "privacy-language-list",
                    Name = "Allow Websites Access to Language List",
                    Description = "Let websites show me locally relevant content by accessing my language list",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "General",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Control Panel\\International\\User Profile",
                            Name = "HttpAcceptLanguageOptOut",
                            EnabledValue = 0,      // When toggle is ON, language list access is enabled
                            DisabledValue = 1,     // When toggle is OFF, language list access is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 1,
                            DefaultValue = null,
                            // When this key doesn't exist, language list access is enabled
                            AbsenceMeansEnabled = true
                        }
                    }
                },

                // App Launch Tracking
                new OptimizationSetting
                {
                    Id = "privacy-app-launch-tracking",
                    Name = "App Launch Tracking",
                    Description = "Let Windows improve Start and search results by tracking app launches",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "General",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Start_TrackProgs",
                            EnabledValue = 1,      // When toggle is ON, app launch tracking is enabled
                            DisabledValue = 0,     // When toggle is OFF, app launch tracking is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        }
                    }
                },

                // Show Suggested Content in Settings (combined setting)
                new OptimizationSetting
                {
                    Id = "privacy-settings-content",
                    Name = "Show Suggested Content in Settings",
                    Description = "Controls suggested content in the Settings app",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Settings App",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager",
                            Name = "SubscribedContent-338393Enabled",
                            EnabledValue = 1,      // When toggle is ON, suggested content is enabled
                            DisabledValue = 0,     // When toggle is OFF, suggested content is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager",
                            Name = "SubscribedContent-353694Enabled",
                            EnabledValue = 1,      // When toggle is ON, suggested content is enabled
                            DisabledValue = 0,     // When toggle is OFF, suggested content is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager",
                            Name = "SubscribedContent-353696Enabled",
                            EnabledValue = 1,      // When toggle is ON, suggested content is enabled
                            DisabledValue = 0,     // When toggle is OFF, suggested content is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All
                },

                // Settings App Notifications
                new OptimizationSetting
                {
                    Id = "privacy-settings-notifications",
                    Name = "Settings App Notifications",
                    Description = "Controls notifications in the Settings app and immersive control panel",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Settings App",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\SystemSettings\\AccountNotifications",
                            Name = "EnableAccountNotifications",
                            EnabledValue = 1,      // When toggle is ON, account notifications are enabled
                            DisabledValue = 0,     // When toggle is OFF, account notifications are disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                            IsPrimary = true
                        }
                    }
                },

                // Online Speech Recognition (combined setting)
                new OptimizationSetting
                {
                    Id = "privacy-speech-recognition",
                    Name = "Online Speech Recognition",
                    Description = "Controls online speech recognition",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Speech",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy",
                            Name = "HasAccepted",
                            EnabledValue = 1,      // When toggle is ON, online speech recognition is enabled
                            DisabledValue = 0,     // When toggle is OFF, online speech recognition is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\InputPersonalization",
                            Name = "AllowInputPersonalization",
                            EnabledValue = 1,      // When toggle is ON, input personalization is allowed
                            DisabledValue = 0,     // When toggle is OFF, input personalization is not allowed
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                            // Mark as a Group Policy registry key
                            IsGroupPolicy = true
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All
                },

                // Custom Inking and Typing Dictionary (combined setting)
                new OptimizationSetting
                {
                    Id = "privacy-inking-typing-dictionary",
                    Name = "Custom Inking and Typing Dictionary",
                    Description = "Controls custom inking and typing dictionary (turning off will clear all words in your custom dictionary)",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Inking and Typing",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\CPSS\\Store\\InkingAndTypingPersonalization",
                            Name = "Value",
                            EnabledValue = 1,      // When toggle is ON, custom dictionary is enabled
                            DisabledValue = 0,     // When toggle is OFF, custom dictionary is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                            // Mark as primary for linked settings
                            IsPrimary = true
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Personalization\\Settings",
                            Name = "AcceptedPrivacyPolicy",
                            EnabledValue = 1,      // When toggle is ON, privacy policy is accepted
                            DisabledValue = 0,     // When toggle is OFF, privacy policy is not accepted
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\InputPersonalization",
                            Name = "RestrictImplicitTextCollection",
                            EnabledValue = 0,      // When toggle is ON, text collection is not restricted
                            DisabledValue = 1,     // When toggle is OFF, text collection is restricted
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 1,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\InputPersonalization\\TrainedDataStore",
                            Name = "HarvestContacts",
                            EnabledValue = 1,      // When toggle is ON, contacts harvesting is enabled
                            DisabledValue = 0,     // When toggle is OFF, contacts harvesting is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All
                },

                // Send Diagnostic Data (combined setting)
                new OptimizationSetting
                {
                    Id = "privacy-diagnostics",
                    Name = "Send Diagnostic Data",
                    Description = "Controls diagnostic data collection level",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Diagnostics & Feedback",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Diagnostics\\DiagTrack",
                            Name = "ShowedToastAtLevel",
                            EnabledValue = 3,      // When toggle is ON, full diagnostic data is enabled
                            DisabledValue = 1,     // When toggle is OFF, basic diagnostic data is enabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 1,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection",
                            Name = "AllowTelemetry",
                            EnabledValue = 3,      // When toggle is ON, full telemetry is allowed
                            DisabledValue = 1,     // When toggle is OFF, basic telemetry is allowed
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 1,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection",
                            Name = "MaxTelemetryAllowed",
                            EnabledValue = 3,      // When toggle is ON, full telemetry is allowed
                            DisabledValue = 1,     // When toggle is OFF, basic telemetry is allowed
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 1,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
                            Name = "AllowTelemetry",
                            EnabledValue = 3,      // When toggle is ON, telemetry is allowed by policy
                            DisabledValue = 0,     // When toggle is OFF, telemetry is not allowed by policy
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                            // Mark as primary for linked settings
                            IsPrimary = true
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.Primary
                },

                // Improve Inking and Typing (combined setting)
                new OptimizationSetting
                {
                    Id = "privacy-improve-inking-typing",
                    Name = "Improve Inking and Typing",
                    Description = "Controls inking and typing data collection",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Diagnostics & Feedback",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    // Add dependency on Send Diagnostic Data
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "privacy-improve-inking-typing",
                            RequiredSettingId = "privacy-diagnostics"
                        }
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Input\\TIPC",
                            Name = "Enabled",
                            EnabledValue = 1,      // When toggle is ON, inking and typing improvement is enabled
                            DisabledValue = 0,     // When toggle is OFF, inking and typing improvement is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\CPSS\\Store\\ImproveInkingAndTyping",
                            Name = "Value",
                            EnabledValue = 1,      // When toggle is ON, linguistic data collection is allowed
                            DisabledValue = 0,     // When toggle is OFF, linguistic data collection is not allowed
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                            // Mark as primary for linked settings
                            IsPrimary = true
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All
                },

                // Tailored Experiences (combined setting)
                new OptimizationSetting
                {
                    Id = "privacy-tailored-experiences",
                    Name = "Tailored Experiences",
                    Description = "Controls personalized experiences with diagnostic data",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Diagnostics & Feedback",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Privacy",
                            Name = "TailoredExperiencesWithDiagnosticDataEnabled",
                            EnabledValue = 1,      // When toggle is ON, tailored experiences are enabled
                            DisabledValue = 0,     // When toggle is OFF, tailored experiences are disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                            // Mark as primary for linked settings
                            IsPrimary = true
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Policies\\Microsoft\\Windows\\CloudContent",
                            Name = "DisableTailoredExperiencesWithDiagnosticData",
                            EnabledValue = 0,      // When toggle is ON, tailored experiences are not disabled by policy
                            DisabledValue = 1,     // When toggle is OFF, tailored experiences are disabled by policy
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 1,
                            DefaultValue = null,
                            // Mark as a Group Policy registry key
                            IsGroupPolicy = true
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All
                },

                // Location Services (combined setting)
                new OptimizationSetting
                {
                    Id = "privacy-location-services",
                    Name = "Location Services",
                    Description = "Controls location services",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "App Permissions",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location",
                            Name = "Value",
                            EnabledValue = "Allow",  // When toggle is ON, location services are allowed
                            DisabledValue = "Deny",  // When toggle is OFF, location services are denied
                            ValueType = RegistryValueKind.String,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = "Deny",
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\LocationAndSensors",
                            Name = "DisableLocation",
                            EnabledValue = 0,      // When toggle is ON, location is not disabled by policy
                            DisabledValue = 1,     // When toggle is OFF, location is disabled by policy
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 1,
                            DefaultValue = null,
                            // Mark as a Group Policy registry key
                            IsGroupPolicy = true
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All
                },

                // Camera Access
                new OptimizationSetting
                {
                    Id = "privacy-camera-access",
                    Name = "Camera Access",
                    Description = "Controls camera access",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "App Permissions",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam",
                            Name = "Value",
                            EnabledValue = "Allow",  // When toggle is ON, camera access is allowed
                            DisabledValue = "Deny",  // When toggle is OFF, camera access is denied
                            ValueType = RegistryValueKind.String,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = "Deny",
                            DefaultValue = null,
                        }
                    }
                },

                // Microphone Access
                new OptimizationSetting
                {
                    Id = "privacy-microphone-access",
                    Name = "Microphone Access",
                    Description = "Controls microphone access",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "App Permissions",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone",
                            Name = "Value",
                            EnabledValue = "Allow",  // When toggle is ON, microphone access is allowed
                            DisabledValue = "Deny",  // When toggle is OFF, microphone access is denied
                            ValueType = RegistryValueKind.String,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = "Deny",
                            DefaultValue = null,
                        }
                    }
                },

                // Account Info Access
                new OptimizationSetting
                {
                    Id = "privacy-account-info-access",
                    Name = "Account Info Access",
                    Description = "Controls account information access",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "App Permissions",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\userAccountInformation",
                            Name = "Value",
                            EnabledValue = "Allow",  // When toggle is ON, account info access is allowed
                            DisabledValue = "Deny",  // When toggle is OFF, account info access is denied
                            ValueType = RegistryValueKind.String,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = "Deny",
                            DefaultValue = null,
                        }
                    }
                },

                // App Diagnostic Access
                new OptimizationSetting
                {
                    Id = "privacy-app-diagnostic-access",
                    Name = "App Diagnostic Access",
                    Description = "Controls app diagnostic access",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "App Permissions",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\appDiagnostics",
                            Name = "Value",
                            EnabledValue = "Allow",  // When toggle is ON, app diagnostic access is allowed
                            DisabledValue = "Deny",  // When toggle is OFF, app diagnostic access is denied
                            ValueType = RegistryValueKind.String,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = "Deny",
                            DefaultValue = null,
                        }
                    }
                },

                // Cloud Content Search for Microsoft Account
                new OptimizationSetting
                {
                    Id = "privacy-search-msa-cloud",
                    Name = "Cloud Content Search (Microsoft Account)",
                    Description = "Controls cloud content search for Microsoft account",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Search",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\SearchSettings",
                            Name = "IsMSACloudSearchEnabled",
                            EnabledValue = 1,      // When toggle is ON, cloud search is enabled
                            DisabledValue = 0,     // When toggle is OFF, cloud search is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        }
                    }
                },

                // Cloud Content Search for Work or School Account
                new OptimizationSetting
                {
                    Id = "privacy-search-aad-cloud",
                    Name = "Cloud Content Search (Work/School Account)",
                    Description = "Controls cloud content search for work or school account",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Search",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\SearchSettings",
                            Name = "IsAADCloudSearchEnabled",
                            EnabledValue = 1,      // When toggle is ON, cloud search is enabled
                            DisabledValue = 0,     // When toggle is OFF, cloud search is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        }
                    }
                },

                // Web Search
                new OptimizationSetting
                {
                    Id = "privacy-web-search",
                    Name = "Web Search",
                    Description = "Controls web search in Start menu and taskbar",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Search",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Search",
                            Name = "BingSearchEnabled",
                            EnabledValue = 1,      // When toggle is ON, web search is enabled
                            DisabledValue = 0,     // When toggle is OFF, web search is disabled
                            ValueType = RegistryValueKind.DWord,
                            // Obsolete properties included for backward compatibility
                            RecommendedValue = 0,
                            DefaultValue = null,
                        }
                    }
                }
            }
        };
    }
}

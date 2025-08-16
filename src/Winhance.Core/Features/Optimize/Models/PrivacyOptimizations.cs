using System.Collections.Generic;
using Microsoft.Win32;
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
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\System",
                            Name = "PublishUserActivities",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, Activity History is enabled
                            DisabledValue = 0, // When toggle is OFF, Activity History is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls activity history tracking",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
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
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo",
                            Name = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, advertising ID is enabled
                            DisabledValue = 0, // When toggle is OFF, advertising ID is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls advertising ID for personalized ads",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\AdvertisingInfo",
                            Name = "DisabledByGroupPolicy",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, advertising is NOT disabled by policy
                            DisabledValue = 1, // When toggle is OFF, advertising is disabled by policy
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls advertising ID for personalized ads",
                            IsPrimary = false,
                            AbsenceMeansEnabled = false,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                },
                // Language List Access
                new OptimizationSetting
                {
                    Id = "privacy-language-list",
                    Name = "Allow Websites Access to Language List",
                    Description =
                        "Let websites show me locally relevant content by accessing my language list",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "General",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Control Panel\\International\\User Profile",
                            Name = "HttpAcceptLanguageOptOut",
                            RecommendedValue = 0,
                            EnabledValue = 0, // When toggle is ON, language list access is enabled
                            DisabledValue = 1, // When toggle is OFF, language list access is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls language list access for websites",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                // App Launch Tracking
                new OptimizationSetting
                {
                    Id = "privacy-app-launch-tracking",
                    Name = "App Launch Tracking",
                    Description =
                        "Let Windows improve Start and search results by tracking app launches",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "General",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Start_TrackProgs",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, app launch tracking is enabled
                            DisabledValue = 0, // When toggle is OFF, app launch tracking is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description =
                                "Controls app launch tracking for improved Start and search results",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
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
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager",
                            Name = "SubscribedContent-338393Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, suggested content is enabled
                            DisabledValue = 0, // When toggle is OFF, suggested content is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls suggested content in the Settings app",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager",
                            Name = "SubscribedContent-353694Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, suggested content is enabled
                            DisabledValue = 0, // When toggle is OFF, suggested content is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls suggested content in the Settings app",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager",
                            Name = "SubscribedContent-353696Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, suggested content is enabled
                            DisabledValue = 0, // When toggle is OFF, suggested content is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls suggested content in the Settings app",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                },
                // Settings App Notifications
                new OptimizationSetting
                {
                    Id = "privacy-settings-notifications",
                    Name = "Settings App Notifications",
                    Description =
                        "Controls notifications in the Settings app and immersive control panel",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Settings App",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\SystemSettings\\AccountNotifications",
                            Name = "EnableAccountNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, account notifications are enabled
                            DisabledValue = 0, // When toggle is OFF, account notifications are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description =
                                "Controls notifications in the Settings app and immersive control panel",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
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
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy",
                            Name = "HasAccepted",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, online speech recognition is enabled
                            DisabledValue = 0, // When toggle is OFF, online speech recognition is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls online speech recognition",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\InputPersonalization",
                            Name = "AllowInputPersonalization",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, input personalization is allowed
                            DisabledValue = 0, // When toggle is OFF, input personalization is not allowed
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls input personalization for speech recognition",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                },
                // Custom Inking and Typing Dictionary (combined setting)
                new OptimizationSetting
                {
                    Id = "privacy-inking-typing-dictionary",
                    Name = "Custom Inking and Typing Dictionary",
                    Description =
                        "Controls custom inking and typing dictionary (turning off will clear all words in your custom dictionary)",
                    Category = OptimizationCategory.Privacy,
                    GroupName = "Inking and Typing",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\CPSS\\Store\\InkingAndTypingPersonalization",
                            Name = "Value",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, custom dictionary is enabled
                            DisabledValue = 0, // When toggle is OFF, custom dictionary is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls custom inking and typing dictionary",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\Personalization\\Settings",
                            Name = "AcceptedPrivacyPolicy",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, privacy policy is accepted
                            DisabledValue = 0, // When toggle is OFF, privacy policy is not accepted
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls custom inking and typing dictionary",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\InputPersonalization",
                            Name = "RestrictImplicitTextCollection",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, text collection is not restricted
                            DisabledValue = 1, // When toggle is OFF, text collection is restricted
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls custom inking and typing dictionary",
                            IsPrimary = false,
                            AbsenceMeansEnabled = false,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\InputPersonalization\\TrainedDataStore",
                            Name = "HarvestContacts",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, contacts harvesting is enabled
                            DisabledValue = 0, // When toggle is OFF, contacts harvesting is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls custom inking and typing dictionary",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
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
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Diagnostics\\DiagTrack",
                            Name = "ShowedToastAtLevel",
                            RecommendedValue = 1,
                            EnabledValue = 3, // When toggle is ON, full diagnostic data is enabled
                            DisabledValue = 1, // When toggle is OFF, basic diagnostic data is enabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            Description = "Controls diagnostic data collection level",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection",
                            Name = "AllowTelemetry",
                            RecommendedValue = 1,
                            EnabledValue = 3, // When toggle is ON, full telemetry is allowed
                            DisabledValue = 1, // When toggle is OFF, basic telemetry is allowed
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            Description = "Controls telemetry data collection",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection",
                            Name = "MaxTelemetryAllowed",
                            RecommendedValue = 1,
                            EnabledValue = 3, // When toggle is ON, full telemetry is allowed
                            DisabledValue = 1, // When toggle is OFF, basic telemetry is allowed
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            Description = "Controls telemetry data collection",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection",
                            Name = "AllowTelemetry",
                            RecommendedValue = 1,
                            EnabledValue = 3, // When toggle is ON, telemetry is allowed by policy
                            DisabledValue = 0, // When toggle is OFF, telemetry is not allowed by policy
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            Description = "Controls telemetry data collection",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
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
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "privacy-improve-inking-typing",
                            RequiredSettingId = "privacy-diagnostics",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\Input\\TIPC",
                            Name = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, inking and typing improvement is enabled
                            DisabledValue = 0, // When toggle is OFF, inking and typing improvement is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls inking and typing data collection",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\CPSS\\Store\\ImproveInkingAndTyping",
                            Name = "Value",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, linguistic data collection is allowed
                            DisabledValue = 0, // When toggle is OFF, linguistic data collection is not allowed
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls inking and typing data collection",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
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
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Privacy",
                            Name = "TailoredExperiencesWithDiagnosticDataEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, tailored experiences are enabled
                            DisabledValue = 0, // When toggle is OFF, tailored experiences are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls personalized experiences with diagnostic data",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Policies\\Microsoft\\Windows\\CloudContent",
                            Name = "DisableTailoredExperiencesWithDiagnosticData",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, tailored experiences are not disabled by policy
                            DisabledValue = 1, // When toggle is OFF, tailored experiences are disabled by policy
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls personalized experiences with diagnostic data",
                            IsPrimary = false,
                            AbsenceMeansEnabled = false,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
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
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location",
                            Name = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, location services are allowed
                            DisabledValue = "Deny", // When toggle is OFF, location services are denied
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            Description = "Controls location services",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Privacy",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\LocationAndSensors",
                            Name = "DisableLocation",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, location is not disabled by policy
                            DisabledValue = 1, // When toggle is OFF, location is disabled by policy
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls location services",
                            IsPrimary = false,
                            AbsenceMeansEnabled = false,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
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
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam",
                            Name = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, camera access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, camera access is denied
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            Description = "Controls camera access",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
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
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone",
                            Name = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, microphone access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, microphone access is denied
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            Description = "Controls microphone access",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
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
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\userAccountInformation",
                            Name = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, account info access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, account info access is denied
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            Description = "Controls account information access",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
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
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\appDiagnostics",
                            Name = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, app diagnostic access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, app diagnostic access is denied
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            Description = "Controls app diagnostic access",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
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
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\SearchSettings",
                            Name = "IsMSACloudSearchEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, cloud search is enabled
                            DisabledValue = 0, // When toggle is OFF, cloud search is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls cloud content search for Microsoft account",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                // Cloud Content Search for Work or School Account
                new OptimizationSetting
                {
                    Id = "privacy-search-aad-cloud",
                    Name = "Cloud Content Search (Work/School Acc)",
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
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\SearchSettings",
                            Name = "IsAADCloudSearchEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, cloud search is enabled
                            DisabledValue = 0, // When toggle is OFF, cloud search is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description =
                                "Controls cloud content search for work or school account",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
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
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Search",
                            Name = "BingSearchEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, web search is enabled
                            DisabledValue = 0, // When toggle is OFF, web search is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls web search in Start menu and taskbar",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
            },
        };
    }
}

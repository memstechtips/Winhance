using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class PrivacyOptimizations
{
    public static SettingGroup GetPrivacyOptimizations()
    {
        return new SettingGroup
        {
            Name = "Privacy",
            FeatureId = FeatureIds.Privacy,
            Settings = new List<SettingDefinition>
            {
                // Activity History
                new SettingDefinition
                {
                    Id = "privacy-activity-history",
                    Name = "Activity History",
                    Description = "Controls activity history tracking",
                    GroupName = "Activity History",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                            ValueName = "PublishUserActivities",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, Activity History is enabled
                            DisabledValue = 0, // When toggle is OFF, Activity History is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Personalized Ads (combined setting)
                new SettingDefinition
                {
                    Id = "privacy-advertising-id",
                    Name = "Personalized Ads",
                    Description = "Controls personalized ads using advertising ID",
                    GroupName = "Advertising",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
                            ValueName = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, advertising ID is enabled
                            DisabledValue = 0, // When toggle is OFF, advertising ID is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
                            ValueName = "DisabledByGroupPolicy",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, advertising is NOT disabled by policy
                            DisabledValue = 1, // When toggle is OFF, advertising is disabled by policy
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Language List Access
                new SettingDefinition
                {
                    Id = "privacy-language-list",
                    Name = "Allow Websites Access to Language List",
                    Description =
                        "Let websites show me locally relevant content by accessing my language list",
                    GroupName = "General",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\International\User Profile",
                            ValueName = "HttpAcceptLanguageOptOut",
                            RecommendedValue = 0,
                            EnabledValue = 0, // When toggle is ON, language list access is enabled
                            DisabledValue = 1, // When toggle is OFF, language list access is disabled
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // App Launch Tracking
                new SettingDefinition
                {
                    Id = "privacy-app-launch-tracking",
                    Name = "App Launch Tracking",
                    Description =
                        "Let Windows improve Start and search results by tracking app launches",
                    GroupName = "General",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_TrackProgs",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, app launch tracking is enabled
                            DisabledValue = 0, // When toggle is OFF, app launch tracking is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Show Suggested Content in Settings (combined setting)
                new SettingDefinition
                {
                    Id = "privacy-settings-content",
                    Name = "Show Suggested Content in Settings",
                    Description = "Controls suggested content in the Settings app",
                    GroupName = "Settings App",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338393Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, suggested content is enabled
                            DisabledValue = 0, // When toggle is OFF, suggested content is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353694Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, suggested content is enabled
                            DisabledValue = 0, // When toggle is OFF, suggested content is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353696Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, suggested content is enabled
                            DisabledValue = 0, // When toggle is OFF, suggested content is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Settings App Notifications
                new SettingDefinition
                {
                    Id = "privacy-settings-notifications",
                    Name = "Settings App Notifications",
                    Description =
                        "Controls notifications in the Settings app and immersive control panel",
                    GroupName = "Settings App",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications",
                            ValueName = "EnableAccountNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, account notifications are enabled
                            DisabledValue = 0, // When toggle is OFF, account notifications are disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Online Speech Recognition (combined setting)
                new SettingDefinition
                {
                    Id = "privacy-speech-recognition",
                    Name = "Online Speech Recognition",
                    Description = "Controls online speech recognition",
                    GroupName = "Speech",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy",
                            ValueName = "HasAccepted",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, online speech recognition is enabled
                            DisabledValue = 0, // When toggle is OFF, online speech recognition is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization",
                            ValueName = "AllowInputPersonalization",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, input personalization is allowed
                            DisabledValue = 0, // When toggle is OFF, input personalization is not allowed
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Custom Inking and Typing Dictionary (combined setting)
                new SettingDefinition
                {
                    Id = "privacy-inking-typing-dictionary",
                    Name = "Custom Inking and Typing Dictionary",
                    Description =
                        "Controls custom inking and typing dictionary (turning off will clear all words in your custom dictionary)",
                    GroupName = "Inking and Typing",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, custom dictionary is enabled
                            DisabledValue = 0, // When toggle is OFF, custom dictionary is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings",
                            ValueName = "AcceptedPrivacyPolicy",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, privacy policy is accepted
                            DisabledValue = 0, // When toggle is OFF, privacy policy is not accepted
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization",
                            ValueName = "RestrictImplicitTextCollection",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, text collection is not restricted
                            DisabledValue = 1, // When toggle is OFF, text collection is restricted
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization\\TrainedDataStore",
                            ValueName = "HarvestContacts",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, contacts harvesting is enabled
                            DisabledValue = 0, // When toggle is OFF, contacts harvesting is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Send Diagnostic Data (combined setting)
                new SettingDefinition
                {
                    Id = "privacy-diagnostics",
                    Name = "Send Diagnostic Data",
                    Description = "Controls diagnostic data collection level",
                    GroupName = "Diagnostics & Feedback",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack",
                            ValueName = "ShowedToastAtLevel",
                            RecommendedValue = 1,
                            EnabledValue = 3, // When toggle is ON, full diagnostic data is enabled
                            DisabledValue = 1, // When toggle is OFF, basic diagnostic data is enabled
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 1,
                            EnabledValue = 3, // When toggle is ON, full telemetry is allowed
                            DisabledValue = 1, // When toggle is OFF, basic telemetry is allowed
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\\DataCollection",
                            ValueName = "MaxTelemetryAllowed",
                            RecommendedValue = 1,
                            EnabledValue = 3, // When toggle is ON, full telemetry is allowed
                            DisabledValue = 1, // When toggle is OFF, basic telemetry is allowed
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 1,
                            EnabledValue = 3, // When toggle is ON, telemetry is allowed by policy
                            DisabledValue = 0, // When toggle is OFF, telemetry is not allowed by policy
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Improve Inking and Typing (combined setting)
                new SettingDefinition
                {
                    Id = "privacy-improve-inking-typing",
                    Name = "Improve Inking and Typing",
                    Description = "Controls inking and typing data collection",
                    GroupName = "Diagnostics & Feedback",
                    InputType = InputType.Toggle,
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
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Input\TIPC",
                            ValueName = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, inking and typing improvement is enabled
                            DisabledValue = 0, // When toggle is OFF, inking and typing improvement is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\ImproveInkingAndTyping",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, linguistic data collection is allowed
                            DisabledValue = 0, // When toggle is OFF, linguistic data collection is not allowed
                            DefaultValue = 1, // Default value when registry key exists but no value is set,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Tailored Experiences (combined setting)
                new SettingDefinition
                {
                    Id = "privacy-tailored-experiences",
                    Name = "Tailored Experiences",
                    Description = "Controls personalized experiences with diagnostic data",
                    GroupName = "Diagnostics & Feedback",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Privacy",
                            ValueName = "TailoredExperiencesWithDiagnosticDataEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, tailored experiences are enabled
                            DisabledValue = 0, // When toggle is OFF, tailored experiences are disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent",
                            ValueName = "DisableTailoredExperiencesWithDiagnosticData",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, tailored experiences are not disabled by policy
                            DisabledValue = 1, // When toggle is OFF, tailored experiences are disabled by policy
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Location Services (combined setting)
                new SettingDefinition
                {
                    Id = "privacy-location-services",
                    Name = "Location Services",
                    Description = "Controls location services",
                    GroupName = "App Permissions",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, location services are allowed
                            DisabledValue = "Deny", // When toggle is OFF, location services are denied
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                            ValueName = "DisableLocation",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, location is not disabled by policy
                            DisabledValue = 1, // When toggle is OFF, location is disabled by policy
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Camera Access
                new SettingDefinition
                {
                    Id = "privacy-camera-access",
                    Name = "Camera Access",
                    Description = "Controls camera access",
                    GroupName = "App Permissions",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, camera access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, camera access is denied
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                // Microphone Access
                new SettingDefinition
                {
                    Id = "privacy-microphone-access",
                    Name = "Microphone Access",
                    Description = "Controls microphone access",
                    GroupName = "App Permissions",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, microphone access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, microphone access is denied
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                // Account Info Access
                new SettingDefinition
                {
                    Id = "privacy-account-info-access",
                    Name = "Account Info Access",
                    Description = "Controls account information access",
                    GroupName = "App Permissions",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userAccountInformation",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, account info access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, account info access is denied
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                // App Diagnostic Access
                new SettingDefinition
                {
                    Id = "privacy-app-diagnostic-access",
                    Name = "App Diagnostic Access",
                    Description = "Controls app diagnostic access",
                    GroupName = "App Permissions",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, app diagnostic access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, app diagnostic access is denied
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                // Cloud Content Search for Microsoft Account
                new SettingDefinition
                {
                    Id = "privacy-search-msa-cloud",
                    Name = "Cloud Content Search (Microsoft Account)",
                    Description = "Controls cloud content search for Microsoft account",
                    GroupName = "Search",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\\CurrentVersion\\SearchSettings",
                            ValueName = "IsMSACloudSearchEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, cloud search is enabled
                            DisabledValue = 0, // When toggle is OFF, cloud search is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Cloud Content Search for Work or School Account
                new SettingDefinition
                {
                    Id = "privacy-search-aad-cloud",
                    Name = "Cloud Content Search (Work/School Acc)",
                    Description = "Controls cloud content search for work or school account",
                    GroupName = "Search",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\\Windows\\CurrentVersion\\SearchSettings",
                            ValueName = "IsAADCloudSearchEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, cloud search is enabled
                            DisabledValue = 0, // When toggle is OFF, cloud search is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

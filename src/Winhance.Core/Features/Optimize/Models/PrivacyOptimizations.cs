using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class PrivacyAndSecurityOptimizations
{
    public static SettingGroup GetPrivacyAndSecurityOptimizations()
    {
        return new SettingGroup
        {
            Name = "Privacy & Security",
            FeatureId = FeatureIds.Privacy,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "security-uac-level",
                    Name = "User Account Control Level",
                    Description = "Controls UAC notification level and secure desktop behavior",
                    GroupName = "Security",
                    Icon = "ShieldAccount",
                    InputType = InputType.Selection,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                            ValueName = "ConsentPromptBehaviorAdmin",
                            RecommendedValue = 5,
                            EnabledValue = 5,
                            DisabledValue = 0,
                            DefaultValue = 5,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                            ValueName = "PromptOnSecureDesktop",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "Prompt for Credentials",
                            "Always notify",
                            "Notify when apps try to make changes",
                            "Notify when apps try to make changes (no dim)",
                            "Never notify",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["ConsentPromptBehaviorAdmin"] = 1,
                                ["PromptOnSecureDesktop"] = 1,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["ConsentPromptBehaviorAdmin"] = 2,
                                ["PromptOnSecureDesktop"] = 1,
                            },
                            [2] = new Dictionary<string, object?>
                            {
                                ["ConsentPromptBehaviorAdmin"] = 5,
                                ["PromptOnSecureDesktop"] = 1,
                            },
                            [3] = new Dictionary<string, object?>
                            {
                                ["ConsentPromptBehaviorAdmin"] = 5,
                                ["PromptOnSecureDesktop"] = 0,
                            },
                            [4] = new Dictionary<string, object?>
                            {
                                ["ConsentPromptBehaviorAdmin"] = 0,
                                ["PromptOnSecureDesktop"] = 0,
                            },
                        },
                        [CustomPropertyKeys.SupportsCustomState] = true,
                        [CustomPropertyKeys.CustomStateDisplayName] = "Custom (User Defined)",
                    },
                },
                new SettingDefinition
                {
                    Id = "security-workplace-join-messages",
                    Name = "Block Workplace Join Messages",
                    Description = "Blocks the 'Allow my organization to manage my device' and 'No, sign in to this app only' pop-up messages",
                    GroupName = "Security",
                    InputType = InputType.Toggle,
                    Icon = "OfficeBuilding",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin",
                            ValueName = "BlockAADWorkplaceJoin",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin",
                            ValueName = "BlockAADWorkplaceJoin",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "security-bitlocker-auto-encryption",
                    Name = "Prevent BitLocker Auto Encryption",
                    Description = "Prevents Windows from automatically encrypting your device with BitLocker without user consent",
                    GroupName = "Security",
                    InputType = InputType.Toggle,
                    IconPack = "MaterialDesign",
                    Icon = "EnhancedEncryptionRound",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\BitLocker",
                            ValueName = "PreventDeviceEncryption",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "security-wifi-sense",
                    Name = "WiFi-Sense",
                    Description = "Allow sharing WiFi passwords with contacts and automatically connecting to suggested open hotspots",
                    GroupName = "Security",
                    InputType = InputType.Toggle,
                    Icon = "WifiOff",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "security-automatic-maintenance",
                    Name = "Automatic Maintenance",
                    Description = "Choose if Windows should run automatic system maintenance tasks during idle time",
                    GroupName = "Security",
                    InputType = InputType.Toggle,
                    Icon = "ProgressWrench",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance",
                            ValueName = "MaintenanceDisabled",
                            RecommendedValue = 1,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "security-error-reporting",
                    Name = "Windows Error Reporting",
                    Description = "Choose if Windows should collect and send crash reports and error information to Microsoft",
                    GroupName = "Security",
                    InputType = InputType.Toggle,
                    IconPack = "Lucide",
                    Icon = "Bug",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
                            ValueName = "Disabled",
                            RecommendedValue = 1,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
                            ValueName = "Disabled",
                            RecommendedValue = 1,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "security-remote-assistance",
                    Name = "Remote Assistance",
                    Description = "Choose if other people can connect to your computer remotely to provide technical support",
                    GroupName = "Security",
                    Icon = "RemoteDesktop",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Remote Assistance",
                            ValueName = "fAllowToGetHelp",
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
                    Id = "privacy-ads-promotional-master",
                    Name = "Ads, Suggestions and Promotional Content",
                    Description = "Controls all advertising, suggestions, and promotional content throughout Windows",
                    GroupName = "Content Delivery & Advertising",
                    Icon = "AdvertisementsOff",
                    InputType = InputType.Selection,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Winhance\Settings",
                            ValueName = "AdsPromotionalContentMode",
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 2,
                            RecommendedValue = 1,
                            IsPrimary = true,
                        },
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "Allow",
                            "Deny",
                            "Custom",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["AdsPromotionalContentMode"] = 0,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["AdsPromotionalContentMode"] = 1,
                            },
                            [2] = new Dictionary<string, object?>
                            {
                                ["AdsPromotionalContentMode"] = 2,
                            },
                        },
                        [CustomPropertyKeys.SettingPresets] = new Dictionary<int, Dictionary<string, bool>>
                        {
                            [0] = new Dictionary<string, bool>
                            {
                                ["privacy-content-delivery-allowed"] = true,
                                ["privacy-subscribed-content"] = true,
                                ["privacy-feature-management"] = true,
                                ["privacy-soft-landing"] = true,
                                ["privacy-oem-preinstalled-apps"] = true,
                                ["privacy-preinstalled-apps"] = true,
                                ["privacy-preinstalled-apps-ever"] = true,
                                ["privacy-silent-installed-apps"] = true,
                                ["privacy-rotating-lock-screen"] = true,
                                ["privacy-lock-screen-overlay"] = true,
                                ["privacy-lock-screen-slideshow"] = true,
                                ["privacy-settings-content"] = true,
                                ["privacy-timeline-suggestions"] = true,
                                ["notifications-welcome-experience"] = true,
                                ["notifications-tips-suggestions"] = true,
                                ["notifications-system-pane-suggestions"] = true,
                                ["start-show-suggestions"] = true,
                            },
                            [1] = new Dictionary<string, bool>
                            {
                                ["privacy-content-delivery-allowed"] = false,
                                ["privacy-subscribed-content"] = false,
                                ["privacy-feature-management"] = false,
                                ["privacy-soft-landing"] = false,
                                ["privacy-oem-preinstalled-apps"] = false,
                                ["privacy-preinstalled-apps"] = false,
                                ["privacy-preinstalled-apps-ever"] = false,
                                ["privacy-silent-installed-apps"] = false,
                                ["privacy-rotating-lock-screen"] = false,
                                ["privacy-lock-screen-overlay"] = false,
                                ["privacy-lock-screen-slideshow"] = false,
                                ["privacy-settings-content"] = false,
                                ["privacy-timeline-suggestions"] = false,
                                ["notifications-welcome-experience"] = false,
                                ["notifications-tips-suggestions"] = false,
                                ["notifications-system-pane-suggestions"] = false,
                                ["start-show-suggestions"] = false,
                            },
                        },
                        [CustomPropertyKeys.CrossGroupChildSettings] = new Dictionary<string, string>
                        {
                            ["privacy-rotating-lock-screen"] = "Spotlight",
                            ["privacy-lock-screen-overlay"] = "Fun Facts & Tips",
                            ["privacy-lock-screen-slideshow"] = "Slideshow",
                            ["privacy-settings-content"] = "Suggested Content",
                            ["privacy-timeline-suggestions"] = "Timeline Suggestions",
                            ["notifications-welcome-experience"] = "Welcome Experience",
                            ["notifications-tips-suggestions"] = "Tips & Suggestions",
                            ["notifications-system-pane-suggestions"] = "Notification Center Suggestions",
                            ["start-show-suggestions"] = "Start Suggestions",
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-content-delivery-allowed",
                    Name = "Content Delivery",
                    Description = "Allows Windows to deliver promotional content and automatically install suggested apps",
                    GroupName = "Content Delivery & Advertising",
                    Icon = "PackageVariant",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-content-delivery-allowed",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "ContentDeliveryAllowed",
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
                    Id = "privacy-subscribed-content",
                    Name = "Subscribed Content",
                    Description = "Enables promotional content subscriptions from Microsoft and partners throughout Windows",
                    GroupName = "Content Delivery & Advertising",
                    Icon = "BookmarkMultiple",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-subscribed-content",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContentEnabled",
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
                    Id = "privacy-feature-management",
                    Name = "Feature Management",
                    Description = "Enables Windows feature management functionality for promotional features and automatic app installations",
                    GroupName = "Content Delivery & Advertising",
                    IconPack = "MaterialDesign",
                    Icon = "InstallDesktopRound",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-feature-management",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "FeatureManagementEnabled",
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
                    Id = "privacy-soft-landing",
                    Name = "Soft Landing Experiences",
                    Description = "Displays tips and notifications about Windows features as you use the operating system",
                    GroupName = "Content Delivery & Advertising",
                    IconPack = "MaterialDesign",
                    Icon = "TipsAndUpdates",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-soft-landing",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SoftLandingEnabled",
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
                    Id = "privacy-oem-preinstalled-apps",
                    Name = "OEM Pre-installed Apps",
                    Description = "Prevents OEM manufacturers from automatically installing bloatware apps",
                    GroupName = "Content Delivery & Advertising",
                    Icon = "PackageDown",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-oem-preinstalled-apps",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "OemPreInstalledAppsEnabled",
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
                    Id = "privacy-preinstalled-apps",
                    Name = "Pre-installed Suggested Apps",
                    Description = "Prevents Microsoft from automatically installing suggested apps",
                    GroupName = "Content Delivery & Advertising",
                    Icon = "PackageVariantPlus",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-preinstalled-apps",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "PreInstalledAppsEnabled",
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
                    Id = "privacy-preinstalled-apps-ever",
                    Name = "Pre-installed Apps History Tracking",
                    Description = "Disables tracking of whether pre-installed apps were ever enabled",
                    GroupName = "Content Delivery & Advertising",
                    Icon = "ClipboardTextClockOutline",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-preinstalled-apps-ever",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "PreInstalledAppsEverEnabled",
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
                    Id = "privacy-silent-installed-apps",
                    Name = "Silent App Installation",
                    Description = "Prevents apps from being silently installed in the background",
                    GroupName = "Content Delivery & Advertising",
                    Icon = "CubeOffOutline",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-silent-installed-apps",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SilentInstalledAppsEnabled",
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
                    Id = "privacy-lock-screen",
                    Name = "Lock Screen",
                    Description = "Allows users to lock their computer using Windows+L, Start menu, or Ctrl+Alt+Del screen",
                    GroupName = "Lock Screen",
                    InputType = InputType.Toggle,
                    Icon = "MonitorLock",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon",
                            ValueName = "DisableLockWorkstation",
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
                    Id = "privacy-rotating-lock-screen",
                    Name = "Windows Spotlight on Lock Screen",
                    Description = "Displays rotating Windows Spotlight images on your lock screen instead of a static background",
                    GroupName = "Lock Screen",
                    IconPack = "Lucide",
                    Icon = "Wallpaper",
                    InputType = InputType.Toggle,
                    ParentSettingId = "privacy-lock-screen",
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-rotating-lock-screen",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "RotatingLockScreenEnabled",
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
                    Id = "privacy-lock-screen-overlay",
                    Name = "Lock Screen Fun Facts and Tips",
                    Description = "Displays fun facts, tips, and tricks as an overlay on your lock screen",
                    GroupName = "Lock Screen",
                    Icon = "MonitorShimmer",
                    InputType = InputType.Toggle,
                    ParentSettingId = "privacy-lock-screen",
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-lock-screen-overlay",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "RotatingLockScreenOverlayEnabled",
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
                    Id = "privacy-lock-screen-slideshow",
                    Name = "Lock Screen Slideshow",
                    Description = "Enables slideshow option for lock screen background",
                    GroupName = "Lock Screen",
                    IconPack = "Lucide",
                    Icon = "MonitorPlay",
                    InputType = InputType.Toggle,
                    ParentSettingId = "privacy-lock-screen",
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-lock-screen-slideshow",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SlideshowEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-advertising-id",
                    Name = "Let apps show me personalized ads by using my advertising ID",
                    Description = "Windows generates a unique advertising ID that apps use to track your activity and deliver personalized ads based on your behavior across different apps",
                    GroupName = "General",
                    Icon = "Advertisements",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
                            ValueName = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
                            ValueName = "DisabledByGroupPolicy",
                            RecommendedValue = 1,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
                            ValueName = "DisabledByGroupPolicy",
                            RecommendedValue = 1,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-language-list",
                    Name = "Let websites show me locally relevant content by accessing my language list",
                    Description = "Allows websites to access your language preferences so they can automatically display content in your preferred language without requiring manual configuration on each site",
                    GroupName = "General",
                    Icon = "Translate",
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
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-app-launch-tracking",
                    Name = "Let Windows improve Start and search results by tracking app launches",
                    Description = "Windows records which apps you use most frequently to personalize your Start menu and improve search results, making your most-used apps more accessible",
                    GroupName = "General",
                    Icon = "ArchiveSearch",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_TrackProgs",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, app launch tracking is enabled
                            DisabledValue = 0, // When toggle is OFF, app launch tracking is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-settings-content",
                    Name = "Show me suggested content in the Settings app",
                    Description = "Microsoft displays promotional content, tips, and feature suggestions within the Windows Settings app to help you discover new features and functionality",
                    GroupName = "General",
                    Icon = "StarCog",
                    InputType = InputType.Toggle,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-settings-content",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338393Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353694Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353696Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                // Settings App Notifications
                new SettingDefinition
                {
                    Id = "privacy-settings-notifications",
                    Name = "Settings App Notifications",
                    Description = "Shows account notifications in the Settings app, including prompts to reauthenticate, backup your device, and manage subscriptions",
                    GroupName = "General",
                    Icon = "BellCog",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications",
                            ValueName = "EnableAccountNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, account notifications are enabled
                            DisabledValue = 0, // When toggle is OFF, account notifications are disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-speech-recognition",
                    Name = "Online Speech Recognition",
                    Description = "Use your voice for apps using Microsoft's online speech recognition technology",
                    GroupName = "Speech",
                    Icon = "MicrophoneQuestion",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy",
                            ValueName = "HasAccepted",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\InputPersonalization",
                            ValueName = "AllowInputPersonalization",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization",
                            ValueName = "AllowInputPersonalization",
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
                    Id = "privacy-narrator-online-services",
                    Name = "Narrator Online Services",
                    Description = "Allow Narrator to use Microsoft cloud services for features like intelligent image descriptions and enhanced voice models",
                    GroupName = "Speech",
                    Icon = "CloudQuestion",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam",
                            ValueName = "OnlineServicesEnabled",
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
                    Id = "privacy-narrator-scripting",
                    Name = "Narrator Scripting Support",
                    Description = "Allow Narrator to execute scripts for automation and custom functionality",
                    GroupName = "Speech",
                    Icon = "ScriptText",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam",
                            ValueName = "ScriptingEnabled",
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
                    Id = "privacy-inking-typing-dictionary",
                    Name = "Custom Inking and Typing Dictionary",
                    Description = "Uses your typing history and handwriting patterns to create a custom dictionary (turning off will clear all words in your custom dictionary)",
                    GroupName = "Inking and typing personalization",
                    IconPack = "Lucide",
                    Icon = "BookA",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, custom dictionary is enabled
                            DisabledValue = 0, // When toggle is OFF, custom dictionary is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings",
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
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization\TrainedDataStore",
                            ValueName = "HarvestContacts",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, contacts harvesting is enabled
                            DisabledValue = 0, // When toggle is OFF, contacts harvesting is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-diagnostics",
                    Name = "Send Diagnostic Data",
                    Description = "Send diagnostic data to Microsoft to help improve Windows and keep it secure",
                    GroupName = "Diagnostics & Feedback",
                    IconPack = "Lucide",
                    Icon = "SquareActivity",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack",
                            ValueName = "ShowedToastAtLevel",
                            RecommendedValue = 1,
                            EnabledValue = 3,
                            DisabledValue = 1,
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 1,
                            EnabledValue = 3,
                            DisabledValue = 0,
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 1,
                            EnabledValue = 3,
                            DisabledValue = 1,
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 1,
                            EnabledValue = 3,
                            DisabledValue = 1,
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "MaxTelemetryAllowed",
                            RecommendedValue = 1,
                            EnabledValue = 3,
                            DisabledValue = 1,
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "MaxTelemetryAllowed",
                            RecommendedValue = 1,
                            EnabledValue = 3,
                            DisabledValue = 1,
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 1,
                            EnabledValue = 3,
                            DisabledValue = 0,
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Improve Inking and Typing (combined setting)
                new SettingDefinition
                {
                    Id = "privacy-improve-inking-typing",
                    Name = "Improve inking and typing",
                    Description = "Send optional inking and typing diagnostic data to Microsoft",
                    GroupName = "Diagnostics & Feedback",
                    IconPack = "Lucide",
                    Icon = "PencilLine",
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
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\ImproveInkingAndTyping",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, linguistic data collection is allowed
                            DisabledValue = 0, // When toggle is OFF, linguistic data collection is not allowed
                            DefaultValue = 1, // Default value when registry key exists but no value is set,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-tailored-experiences",
                    Name = "Tailored Experiences",
                    Description = "Let Microsoft use your diagnostic data to show personalized tips, ads and recommendations",
                    GroupName = "Diagnostics & Feedback",
                    Icon = "AccountCog",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Privacy",
                            ValueName = "TailoredExperiencesWithDiagnosticDataEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent",
                            ValueName = "DisableTailoredExperiencesWithDiagnosticData",
                            RecommendedValue = 1,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\CloudContent",
                            ValueName = "DisableTailoredExperiencesWithDiagnosticData",
                            RecommendedValue = 1,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-feedback-frequency",
                    Name = "Allow Windows to ask you for feedback",
                    Description = "Let Windows ask you to provide feedback on experiences in Windows",
                    GroupName = "Diagnostics & Feedback",
                    Icon = "AccountQuestion",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "DoNotShowFeedbackNotifications",
                            RecommendedValue = 1,
                            EnabledValue = null,
                            DisabledValue = 1,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "DoNotShowFeedbackNotifications",
                            RecommendedValue = 1,
                            EnabledValue = null,
                            DisabledValue = 1,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-activity-history",
                    Name = "Activity History",
                    Description = "Allows you to jump back into what you were doing with apps, docs, or other activities on startup",
                    GroupName = "Activity History",
                    IconPack = "MaterialDesign",
                    Icon = "WorkHistoryRound",
                    InputType = InputType.Toggle,
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\System",
                            ValueName = "PublishUserActivities",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                            ValueName = "PublishUserActivities",
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
                    Id = "privacy-timeline-suggestions",
                    Name = "Timeline Suggestions",
                    Description = "Shows suggestions in the Windows 10 Timeline feature",
                    GroupName = "Activity History",
                    Icon = "TimelineAlert",
                    InputType = InputType.Toggle,
                    IsWindows10Only = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "privacy-timeline-suggestions",
                            RequiredSettingId = "privacy-ads-promotional-master",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353698Enabled",
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
                    Id = "privacy-search-history",
                    Name = "Search history on this device",
                    Description = "Improves search results by allowing Windows Search to store your search history locally on this device (Does not clear existing history)",
                    GroupName = "Search permissions",
                    Icon = "MagnifyScan",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings",
                            ValueName = "IsDeviceSearchHistoryEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, history is enabled
                            DisabledValue = 0, // When toggle is OFF, history is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-search-highlights",
                    Name = "Show search highlights",
                    Description = "See content suggestions in search",
                    GroupName = "Search permissions",
                    IconPack = "MaterialDesign",
                    Icon = "SavedSearchRound",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings",
                            ValueName = "IsDynamicSearchBoxEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, search highlights is enabled
                            DisabledValue = 0, // When toggle is OFF, search highlights is disabled
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-search-msa-cloud",
                    Name = "Cloud Content Search for Microsoft account",
                    Description = "Allow Windows Search to show results from apps and services that you are signed in to with your Microsoft account",
                    GroupName = "Search permissions",
                    Icon = "CloudSearch",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings",
                            ValueName = "IsMSACloudSearchEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, cloud search is enabled
                            DisabledValue = 0, // When toggle is OFF, cloud search is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-search-aad-cloud",
                    Name = "Cloud Content Search for Work or School account",
                    Description = "Allow Windows Search to show results from apps and services that you are signed in to with your work or school account",
                    GroupName = "Search permissions",
                    Icon = "BriefcaseSearch",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings",
                            ValueName = "IsAADCloudSearchEnabled",
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
                    Id = "privacy-allow-cortana",
                    Name = "Allow Cortana",
                    Description = "Enables Microsoft's Cortana virtual assistant for voice commands and searches",
                    GroupName = "Search permissions",
                    InputType = InputType.Toggle,
                    IconPack = "Lucide",
                    Icon = "BotMessageSquare",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                            ValueName = "AllowCortana",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                            ValueName = "AllowCortana",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-location-services",
                    Name = "Location Services",
                    Description = "Allows Windows and apps to access your device location for location-based features",
                    GroupName = "App Permissions",
                    Icon = "MapMarker",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow",
                            DisabledValue = "Deny",
                            DefaultValue = "Allow",
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                            ValueName = "DisableLocation",
                            RecommendedValue = 1,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                            ValueName = "DisableLocation",
                            RecommendedValue = 1,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-camera-access",
                    Name = "Camera Access",
                    Description = "Allow apps to have camera access",
                    GroupName = "App Permissions",
                    Icon = "Camera",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, camera access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, camera access is denied
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-microphone-access",
                    Name = "Microphone Access",
                    Description = "Allow apps to have microphone access",
                    GroupName = "App Permissions",
                    Icon = "Microphone",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow", // When toggle is ON, microphone access is allowed
                            DisabledValue = "Deny", // When toggle is OFF, microphone access is denied
                            DefaultValue = "Allow", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-account-info-access",
                    Name = "Account Info Access",
                    Description = "Allow apps to have account info access",
                    GroupName = "App Permissions",
                    Icon = "AccountLockOpen",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userAccountInformation",
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
                    Description = "Allow apps to have app diagnostic access",
                    GroupName = "App Permissions",
                    Icon = "Stethoscope",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = "Allow",
                            DisabledValue = "Deny",
                            DefaultValue = "Allow",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-onedrive-auto-backup",
                    Name = "Disable OneDrive Automatic Backups",
                    Description = "Prevents OneDrive from automatically backing up important folders (Documents, Pictures, Desktop, etc.)",
                    GroupName = "App Permissions",
                    InputType = InputType.Toggle,
                    Icon = "CloudOff",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\OneDrive",
                            ValueName = "KFMBlockOptIn",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\OneDrive",
                            ValueName = "KFMBlockOptIn",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

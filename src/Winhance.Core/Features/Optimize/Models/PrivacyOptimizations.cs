using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Constants;

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
                    IsSubjectivePreference = true,
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
                            RecommendedValue = null,
                            EnabledValue = [5],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                            ValueName = "PromptOnSecureDesktop",
                            RecommendedValue = null,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption
                            {
                                DisplayName = "Prompt for Credentials",
                                ValueMappings = new Dictionary<string, object?>
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 1,
                                    ["PromptOnSecureDesktop"] = 1,
                                },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Always notify",
                                ValueMappings = new Dictionary<string, object?>
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 2,
                                    ["PromptOnSecureDesktop"] = 1,
                                },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Notify when apps try to make changes",
                                ValueMappings = new Dictionary<string, object?>
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 5,
                                    ["PromptOnSecureDesktop"] = 1,
                                },
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Notify when apps try to make changes (no dim)",
                                ValueMappings = new Dictionary<string, object?>
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 5,
                                    ["PromptOnSecureDesktop"] = 0,
                                },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Never notify",
                                ValueMappings = new Dictionary<string, object?>
                                {
                                    ["ConsentPromptBehaviorAdmin"] = 0,
                                    ["PromptOnSecureDesktop"] = 0,
                                },
                                IsRecommended = true,
                            },
                        },
                        SupportsCustomState = true,
                        CustomStateDisplayName = "Custom (User Defined)",
                    },
                },
                new SettingDefinition
                {
                    Id = "security-workplace-join-messages",
                    RecommendedToggleState = false,
                    Name = "Workplace Join Message Prompts",
                    Description = "Show 'Allow my organization to manage my device' prompts throughout Windows",
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
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin",
                            ValueName = "BlockAADWorkplaceJoin",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "security-bitlocker-auto-encryption",
                    Name = "BitLocker Auto Encryption",
                    Description = "Controls whether Windows can automatically encrypt drives with BitLocker. Has no effect if BitLocker encryption is already active on your device",
                    GroupName = "Security",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "LockClosedKey",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\BitLocker",
                            ValueName = "PreventDeviceEncryption",
                            RecommendedValue = 0,
                            EnabledValue = [0],
                            DisabledValue = [1],
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
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                            EnabledValue = [0],
                            DisabledValue = [1],
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
                    IconPack = "Fluent",
                    Icon = "Bug",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
                            ValueName = "Disabled",
                            RecommendedValue = 1,
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
                            ValueName = "Disabled",
                            RecommendedValue = 1,
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
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
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "security-smart-app-control",
                    IsSubjectivePreference = true,
                    Name = "Smart App Control",
                    Description = "Controls the Smart App Control feature which blocks untrusted and potentially dangerous applications",
                    GroupName = "Security",
                    Icon = "ShieldCheck",
                    InputType = InputType.Selection,
                    AddedInVersion = "26.04.01",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\CI\Policy",
                            ValueName = "VerifiedAndReputablePolicyState",
                            RecommendedValue = null,
                            EnabledValue = [2],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption
                            {
                                DisplayName = "Off",
                                ValueMappings = new Dictionary<string, object?> { ["VerifiedAndReputablePolicyState"] = 0 },
                                IsRecommended = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "On (Enforced)",
                                ValueMappings = new Dictionary<string, object?> { ["VerifiedAndReputablePolicyState"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Evaluation Mode",
                                ValueMappings = new Dictionary<string, object?> { ["VerifiedAndReputablePolicyState"] = 2 },
                                IsDefault = true,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "security-developer-mode",
                    RecommendedToggleState = false,
                    Name = "Developer Mode",
                    Description = "Allows the installation of apps from any source, including loose files",
                    GroupName = "Security",
                    Icon = "CodeBraces",
                    InputType = InputType.Toggle,
                    AddedInVersion = "26.04.08",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\AppModelUnlock",
                            ValueName = "AllowDevelopmentWithoutDevLicense",
                            RecommendedValue = null,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "security-powershell-execution-policy",
                    IsSubjectivePreference = true,
                    Name = "PowerShell Execution Policy",
                    Description = "Controls whether PowerShell scripts are allowed to run and under what conditions for both the current user and the local machine",
                    GroupName = "Security",
                    Icon = "PowerShell",
                    InputType = InputType.Selection,
                    AddedInVersion = "26.04.08",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell",
                            ValueName = "ExecutionPolicy",
                            RecommendedValue = null,
                            EnabledValue = ["Restricted"],
                            DisabledValue = ["RemoteSigned"],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell",
                            ValueName = "ExecutionPolicy",
                            RecommendedValue = null,
                            EnabledValue = ["Restricted"],
                            DisabledValue = ["RemoteSigned"],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption
                            {
                                DisplayName = "Restricted",
                                ValueMappings = new Dictionary<string, object?> { ["ExecutionPolicy"] = "Restricted" },
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "AllSigned",
                                ValueMappings = new Dictionary<string, object?> { ["ExecutionPolicy"] = "AllSigned" },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "RemoteSigned",
                                ValueMappings = new Dictionary<string, object?> { ["ExecutionPolicy"] = "RemoteSigned" },
                                IsRecommended = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Unrestricted",
                                ValueMappings = new Dictionary<string, object?> { ["ExecutionPolicy"] = "Unrestricted" },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Bypass",
                                ValueMappings = new Dictionary<string, object?> { ["ExecutionPolicy"] = "Bypass" },
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-ads-promotional-master",
                    IsSubjectivePreference = true,
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
                            DefaultValue = null,
                            RecommendedValue = null,
                            IsPrimary = true,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption
                            {
                                DisplayName = "Allow",
                                ValueMappings = new Dictionary<string, object?> { ["AdsPromotionalContentMode"] = 0 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Deny",
                                ValueMappings = new Dictionary<string, object?> { ["AdsPromotionalContentMode"] = 1 },
                                IsRecommended = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Custom",
                                ValueMappings = new Dictionary<string, object?> { ["AdsPromotionalContentMode"] = 2 },
                                IsDefault = true,
                            },
                        },
                    },
                    SettingPresets = new Dictionary<int, Dictionary<string, bool>>
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
                            ["privacy-settings-content"] = false,
                            ["privacy-timeline-suggestions"] = false,
                            ["notifications-welcome-experience"] = false,
                            ["notifications-tips-suggestions"] = false,
                            ["notifications-system-pane-suggestions"] = false,
                            ["start-show-suggestions"] = false,
                        },
                    },
                    CrossGroupChildSettings = new Dictionary<string, string>
                    {
                        ["privacy-rotating-lock-screen"] = "Setting_privacy-ads-promotional-master_Child_Spotlight",
                        ["privacy-lock-screen-overlay"] = "Setting_privacy-ads-promotional-master_Child_FunFactsTips",
                        ["privacy-settings-content"] = "Setting_privacy-ads-promotional-master_Child_SuggestedContent",
                        ["privacy-timeline-suggestions"] = "Setting_privacy-ads-promotional-master_Child_TimelineSuggestions",
                        ["notifications-welcome-experience"] = "Setting_privacy-ads-promotional-master_Child_WelcomeExperience",
                        ["notifications-tips-suggestions"] = "Setting_privacy-ads-promotional-master_Child_TipsSuggestions",
                        ["notifications-system-pane-suggestions"] = "Setting_privacy-ads-promotional-master_Child_NotificationCenterSuggestions",
                        ["start-show-suggestions"] = "Setting_privacy-ads-promotional-master_Child_StartSuggestions",
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
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-feature-management",
                    Name = "Feature Management",
                    Description = "Enables Windows feature management functionality for promotional features and automatic app installations",
                    GroupName = "Content Delivery & Advertising",
                    Icon = "MonitorArrowDown",
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
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                    Icon = "LightbulbOn",
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
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
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
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                            RecommendedValue = 1,
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-rotating-lock-screen",
                    Name = "Windows Spotlight on Lock Screen",
                    Description = "Displays rotating Windows Spotlight images on your lock screen instead of a static background. Winhance automatically sets the Start Menu Recommended Section to Show when this setting is enabled as it is required",
                    GroupName = "Lock Screen",
                    IconPack = "Fluent",
                    Icon = "ImageCircle",
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
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresSpecificValue,
                            DependentSettingId = "privacy-rotating-lock-screen",
                            RequiredSettingId = "start-recommended-section",
                            RequiredValue = "Show",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "RotatingLockScreenEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
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
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338387Enabled",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
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
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
                            ValueName = "Enabled",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\AdvertisingInfo",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
                            ValueName = "DisabledByGroupPolicy",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
                            ValueName = "DisabledByGroupPolicy",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
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
                            RecommendedValue = 1,
                            EnabledValue = [null], // When toggle is ON, language list access is enabled
                            DisabledValue = [1], // When toggle is OFF, language list access is disabled
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
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
                            EnabledValue = [null], // When toggle is ON, app launch tracking is enabled
                            DisabledValue = [0], // When toggle is OFF, app launch tracking is disabled
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-settings-content",
                    Name = "Show me suggested content in the Settings app",
                    Description = "Displays promotional content, tips, and feature suggestions within the Windows Settings app. Winhance automatically sets the Start Menu Recommended Section to Show when this setting is enabled as it is required",
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
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresSpecificValue,
                            DependentSettingId = "privacy-settings-content",
                            RequiredSettingId = "start-recommended-section",
                            RequiredValue = "Show",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338393Enabled",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353694Enabled",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353696Enabled",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
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
                            EnabledValue = [null], // When toggle is ON, account notifications are enabled
                            DisabledValue = [0], // When toggle is OFF, account notifications are disabled
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
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
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\InputPersonalization",
                            ValueName = "AllowInputPersonalization",
                            RecommendedValue = null,
                            EnabledValue = [1],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization",
                            ValueName = "AllowInputPersonalization",
                            RecommendedValue = null,
                            EnabledValue = [1],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
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
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
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
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-inking-typing-dictionary",
                    Name = "Custom Inking and Typing Dictionary",
                    Description = "Uses your typing history and handwriting patterns to create a custom dictionary (turning off will clear all words in your custom dictionary)",
                    GroupName = "Inking and typing personalization",
                    IconPack = "Fluent",
                    Icon = "BookDefault",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = [1], // When toggle is ON, custom dictionary is enabled
                            DisabledValue = [0], // When toggle is OFF, custom dictionary is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings",
                            ValueName = "AcceptedPrivacyPolicy",
                            RecommendedValue = 0,
                            EnabledValue = [1], // When toggle is ON, privacy policy is accepted
                            DisabledValue = [0], // When toggle is OFF, privacy policy is not accepted
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization",
                            ValueName = "RestrictImplicitTextCollection",
                            RecommendedValue = 1,
                            EnabledValue = [0], // When toggle is ON, text collection is not restricted
                            DisabledValue = [1], // When toggle is OFF, text collection is restricted
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization\TrainedDataStore",
                            ValueName = "HarvestContacts",
                            RecommendedValue = 0,
                            EnabledValue = [1], // When toggle is ON, contacts harvesting is enabled
                            DisabledValue = [0], // When toggle is OFF, contacts harvesting is disabled
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
                    IconPack = "Fluent",
                    Icon = "PulseSquare",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack",
                            ValueName = "ShowedToastAtLevel",
                            RecommendedValue = 1,
                            EnabledValue = [3],
                            DisabledValue = [1],
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 0,
                            EnabledValue = [3],
                            DisabledValue = [0],
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 0,
                            EnabledValue = [3],
                            DisabledValue = [0],
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 0,
                            EnabledValue = [3],
                            DisabledValue = [0],
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "MaxTelemetryAllowed",
                            RecommendedValue = 0,
                            EnabledValue = [3],
                            DisabledValue = [0],
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "MaxTelemetryAllowed",
                            RecommendedValue = 0,
                            EnabledValue = [3],
                            DisabledValue = [0],
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "AllowTelemetry",
                            RecommendedValue = 0,
                            EnabledValue = [3],
                            DisabledValue = [0],
                            DefaultValue = 3,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AppCompat",
                            ValueName = "AITEnable",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppCompat",
                            ValueName = "AITEnable",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
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
                    IconPack = "Fluent",
                    Icon = "PenSparkle",
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
                            EnabledValue = [1], // When toggle is ON, inking and typing improvement is enabled
                            DisabledValue = [0], // When toggle is OFF, inking and typing improvement is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\ImproveInkingAndTyping",
                            ValueName = "Value",
                            RecommendedValue = 0,
                            EnabledValue = [1], // When toggle is ON, linguistic data collection is allowed
                            DisabledValue = [0], // When toggle is OFF, linguistic data collection is not allowed
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
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent",
                            ValueName = "DisableTailoredExperiencesWithDiagnosticData",
                            RecommendedValue = null,
                            EnabledValue = [0],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\CloudContent",
                            ValueName = "DisableTailoredExperiencesWithDiagnosticData",
                            RecommendedValue = null,
                            EnabledValue = [0],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-feedback-frequency",
                    Name = "Allow Windows to ask you for feedback",
                    Description = "Let Windows ask you to provide feedback on experiences in Windows",
                    GroupName = "Diagnostics & Feedback",
                    IconPack = "Fluent",
                    Icon = "PersonFeedback",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "DoNotShowFeedbackNotifications",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "DoNotShowFeedbackNotifications",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Siuf\Rules",
                            ValueName = "NumberOfSIUFInPeriod",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-activity-history",
                    Name = "Activity History",
                    Description = "Allows you to jump back into what you were doing with apps, docs, or other activities on startup",
                    GroupName = "Activity History",
                    IconPack = "Fluent",
                    Icon = "Timeline",
                    InputType = InputType.Toggle,
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\System",
                            ValueName = "PublishUserActivities",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                            ValueName = "PublishUserActivities",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
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
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
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
                            EnabledValue = [null], // When toggle is ON, history is enabled
                            DisabledValue = [0], // When toggle is OFF, history is disabled
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-search-highlights",
                    Name = "Show search highlights",
                    Description = "See content suggestions in search",
                    GroupName = "Search permissions",
                    IconPack = "Fluent",
                    Icon = "SearchSparkle",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings",
                            ValueName = "IsDynamicSearchBoxEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [null], // When toggle is ON, search highlights is enabled
                            DisabledValue = [0], // When toggle is OFF, search highlights is disabled
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
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
                            EnabledValue = [null], // When toggle is ON, cloud search is enabled
                            DisabledValue = [0], // When toggle is OFF, cloud search is disabled
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
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
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
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
                    IconPack = "Fluent",
                    Icon = "BotSparkle",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                            ValueName = "AllowCortana",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                            ValueName = "AllowCortana",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
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
                            EnabledValue = ["Allow", null],
                            DisabledValue = ["Deny"],
                            DefaultValue = "Allow",
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                            ValueName = "DisableLocation",
                            RecommendedValue = null,
                            EnabledValue = [0],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                            ValueName = "DisableLocation",
                            RecommendedValue = null,
                            EnabledValue = [0],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
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
                            RecommendedValue = "Allow",
                            EnabledValue = ["Allow", null], // When toggle is ON, camera access is allowed
                            DisabledValue = ["Deny"], // When toggle is OFF, camera access is denied
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
                            RecommendedValue = "Allow",
                            EnabledValue = ["Allow", null], // When toggle is ON, microphone access is allowed
                            DisabledValue = ["Deny"], // When toggle is OFF, microphone access is denied
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
                            EnabledValue = ["Allow", null], // When toggle is ON, account info access is allowed
                            DisabledValue = ["Deny"], // When toggle is OFF, account info access is denied
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
                            EnabledValue = ["Allow", null],
                            DisabledValue = ["Deny"],
                            DefaultValue = "Allow",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-onedrive-auto-backup",
                    RecommendedToggleState = false,
                    Name = "OneDrive Automatic Backups",
                    Description = "Controls whether OneDrive automatically backs up your Documents, Pictures, and Desktop folders. Has no effect if OneDrive backups are already active on your device",
                    GroupName = "App Permissions",
                    InputType = InputType.Toggle,
                    Icon = "CloudOff",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\OneDrive",
                            ValueName = "KFMBlockOptIn",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\OneDrive",
                            ValueName = "KFMBlockOptIn",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                // =====================
                // Windows AI
                // =====================
                new SettingDefinition
                {
                    Id = "privacy-turn-off-copilot",
                    Name = "Windows Copilot",
                    Description = "Controls whether Windows Copilot is available system-wide via group policy for both current user and local machine",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "Robot",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsCopilot",
                            ValueName = "TurnOffWindowsCopilot",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\WindowsCopilot",
                            ValueName = "TurnOffWindowsCopilot",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-ai-data-analysis",
                    Name = "AI Data Analysis",
                    Description = "Controls whether Windows AI can analyze user data for personalization and recommendations",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "DatabaseOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                            ValueName = "DisableAIDataAnalysis",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-block-recall-enablement",
                    Name = "Recall Enablement",
                    Description = "Controls whether Windows Recall can be enabled via policy",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "Cancel",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                            ValueName = "AllowRecallEnablement",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-recall-snapshots",
                    Name = "Recall Saving Snapshots",
                    Description = "Allows Windows Recall to save screenshots of your activity for later recall",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "CameraOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                            ValueName = "TurnOffSavingSnapshots",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-click-to-do",
                    Name = "Click to Do",
                    Description = "Controls whether the Click to Do AI feature is available in Windows",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "CursorDefaultClickOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                            ValueName = "DisableClickToDo",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-settings-agent",
                    Name = "AI Settings Agent",
                    Description = "Controls whether the AI-powered Settings Agent is available in Windows",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "CogOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                            ValueName = "DisableSettingsAgent",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-agent-connectors",
                    Name = "AI Agent Connectors",
                    Description = "Controls whether AI agents can use connectors to access external services",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "VectorPolylineRemove",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                            ValueName = "DisableAgentConnectors",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-agent-workspaces",
                    Name = "AI Agent Workspaces",
                    Description = "Controls whether AI Agent Workspaces are available in Windows",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "DesktopClassic",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                            ValueName = "DisableAgentWorkspaces",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-remote-agent-connectors",
                    Name = "Remote AI Agent Connectors",
                    Description = "Controls whether AI agents can use remote connectors to access remote services",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "LanDisconnect",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                            ValueName = "DisableRemoteAgentConnectors",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-copilot-hardware-key",
                    Name = "Copilot Hardware Key",
                    Description = "Controls whether the dedicated Copilot key on keyboards opens Copilot",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "KeyboardOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CopilotKey",
                            ValueName = "SetCopilotHardwareKey",
                            RecommendedValue = "",
                            EnabledValue = [null],
                            DisabledValue = [""],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-copilot-runtime",
                    Name = "Copilot Runtime",
                    Description = "Controls whether the Copilot runtime is allowed to run via policy",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "RobotOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                            ValueName = "AllowCopilotRuntime",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-copilot-unavailable",
                    Name = "Copilot Availability",
                    Description = "Controls whether Copilot is available in the Windows Shell",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "RobotOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Copilot",
                            ValueName = "IsCopilotAvailable",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-bing-chat",
                    Name = "Bing Chat Eligibility",
                    Description = "Controls whether the user is eligible for Bing Chat and Copilot in Search",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "ChatRemove",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Copilot\BingChat",
                            ValueName = "IsUserEligible",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-deny-generative-ai-access",
                    Name = "Generative AI Access",
                    Description = "Controls whether apps can access the generative AI capability on your device",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "ShieldLock",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\generativeAI",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = ["Allow", null],
                            DisabledValue = ["Deny"],
                            DefaultValue = "Allow",
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                            ValueName = "LetAppsAccessGenerativeAI",
                            RecommendedValue = 2,
                            EnabledValue = [0, null],
                            DisabledValue = [2],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-deny-system-ai-models",
                    Name = "System AI Models Access",
                    Description = "Controls whether apps can access system AI models on your device and collect usage data",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "ShieldLock",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\systemAIModels",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = ["Allow", null],
                            DisabledValue = ["Deny"],
                            DefaultValue = "Allow",
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                            ValueName = "LetAppsAccessSystemAIModels",
                            RecommendedValue = 2,
                            EnabledValue = [0, null],
                            DisabledValue = [2],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\systemAIModels",
                            ValueName = "RecordUsageData",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-deny-copilot-microphone",
                    Name = "Copilot Microphone Access",
                    Description = "Controls whether Copilot and Office Hub apps have microphone permission",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "MicrophoneOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\Microsoft.Copilot_8wekyb3d8bbwe",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = ["Allow", null],
                            DisabledValue = ["Deny"],
                            DefaultValue = "Allow",
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\Microsoft.MicrosoftOfficeHub_8wekyb3d8bbwe",
                            ValueName = "Value",
                            RecommendedValue = "Deny",
                            EnabledValue = ["Allow", null],
                            DisabledValue = ["Deny"],
                            DefaultValue = "Allow",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-paint-ai-image-creator",
                    Name = "Paint AI Image Creator",
                    Description = "Controls whether the AI Image Creator feature is available in Microsoft Paint",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "ImageOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint",
                            ValueName = "DisableImageCreator",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-paint-ai-cocreator",
                    Name = "Paint AI Cocreator",
                    Description = "Controls whether the AI Cocreator feature is available in Microsoft Paint",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "PaletteOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint",
                            ValueName = "DisableCocreator",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-paint-generative-fill",
                    Name = "Paint Generative Fill",
                    Description = "Controls whether the AI Generative Fill feature is available in Microsoft Paint",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "FormatPaint",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint",
                            ValueName = "DisableGenerativeFill",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-paint-generative-erase",
                    Name = "Paint Generative Erase",
                    Description = "Controls whether the AI Generative Erase feature is available in Microsoft Paint",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "EraserVariant",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint",
                            ValueName = "DisableGenerativeErase",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-paint-remove-background",
                    Name = "Paint Remove Background",
                    Description = "Controls whether the AI Remove Background feature is available in Microsoft Paint",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "ImageRemove",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint",
                            ValueName = "DisableRemoveBackground",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-input-insights",
                    Name = "Input Insights",
                    Description = "Controls whether Windows Input Insights can track typing patterns and provide suggestions",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "KeyboardOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\input\Settings",
                            ValueName = "InsightsEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-copilot-nudges",
                    Name = "Copilot Nudges",
                    Description = "Controls whether Copilot promotional nudges and background task notifications are shown",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "BellOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowCopilotNudges",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-disable-consumer-ai-content",
                    Name = "AI Consumer Content",
                    Description = "Controls whether AI-driven consumer account content recommendations are shown",
                    GroupName = "Windows AI",
                    AddedInVersion = "26.04.10",
                    Icon = "AccountOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                            ValueName = "DisableConsumerAccountStateContent",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                // =====================
                // Microsoft Edge AI
                // =====================
                new SettingDefinition
                {
                    Id = "privacy-edge-copilot-cdp-page-context",
                    Name = "Edge Copilot CDP Page Context",
                    Description = "Controls whether Copilot can use CDP to access page content in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "WebOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "CopilotCDPPageContext",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-copilot-page-context",
                    Name = "Edge Copilot Page Context",
                    Description = "Controls whether Copilot can read page content in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "FileEyeOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "CopilotPageContext",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-copilot-sidebar",
                    Name = "Edge Copilot Sidebar",
                    Description = "Controls whether the Copilot sidebar is available in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "DockRight",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "HubsSidebarEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-entra-copilot",
                    Name = "Edge Entra Copilot Page Context",
                    Description = "Controls whether Entra Copilot can access page context in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "ShieldOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "EdgeEntraCopilotPageContext",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-m365-copilot-icon",
                    Name = "Edge M365 Copilot Chat Icon",
                    Description = "Controls whether the Microsoft 365 Copilot chat icon is shown in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "ChatMinus",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "Microsoft365CopilotChatIconEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-ai-history-search",
                    Name = "Edge AI History Search",
                    Description = "Controls whether AI-powered history search is available in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "History",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "EdgeHistoryAISearchEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-inline-compose",
                    Name = "Edge Inline AI Compose",
                    Description = "Controls whether AI-powered inline compose suggestions are available in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "PenOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "ComposeInlineEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-local-ai-model",
                    Name = "Edge Local AI Model Settings",
                    Description = "Controls whether local AI model settings are available in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "DatabaseOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "GenAILocalFoundationalModelSettings",
                            RecommendedValue = 1,
                            EnabledValue = [0, null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-builtin-ai-apis",
                    Name = "Edge Built-in AI APIs",
                    Description = "Controls whether built-in AI APIs are available in Microsoft Edge for websites to use",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "Api",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "BuiltInAIAPIsEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-ai-themes",
                    Name = "Edge AI Generated Themes",
                    Description = "Controls whether AI-generated themes are available in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "PaletteOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "AIGenThemesEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-devtools-ai",
                    Name = "Edge DevTools AI",
                    Description = "Controls whether AI features are available in Edge DevTools",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "CodeBracesBox",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "DevToolsGenAiSettings",
                            RecommendedValue = 2,
                            EnabledValue = [0, null],
                            DisabledValue = [2],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-edge-share-history-copilot",
                    Name = "Edge Share History with Copilot",
                    Description = "Controls whether browsing history is shared with Copilot search in Microsoft Edge",
                    GroupName = "Microsoft Edge AI",
                    AddedInVersion = "26.04.10",
                    Icon = "ShareOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge",
                            ValueName = "ShareBrowsingHistoryWithCopilotSearchAllowed",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                // =====================
                // Microsoft Office AI
                // =====================
                new SettingDefinition
                {
                    Id = "privacy-office-ai-training",
                    Name = "Office AI Training",
                    Description = "Controls whether Office collects AI training data from your usage",
                    GroupName = "Microsoft Office AI",
                    AddedInVersion = "26.04.10",
                    Icon = "SchoolOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\office\16.0\common\ai\training",
                            ValueName = "optionalconnectedexperiencesenabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-office-connected-services",
                    Name = "Office Connected Services",
                    Description = "Controls whether Office connected experiences and AI-powered services are available",
                    GroupName = "Microsoft Office AI",
                    AddedInVersion = "26.04.10",
                    Icon = "CloudOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\office\16.0\common\privacy",
                            ValueName = "controllerconnectedservicesenabled",
                            RecommendedValue = 2,
                            EnabledValue = [0, null],
                            DisabledValue = [2],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\office\16.0\common\privacy",
                            ValueName = "usercontentdisabled",
                            RecommendedValue = 2,
                            EnabledValue = [0, null],
                            DisabledValue = [2],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-word-copilot",
                    Name = "Word Copilot",
                    Description = "Controls whether Copilot AI features are available in Microsoft Word",
                    GroupName = "Microsoft Office AI",
                    AddedInVersion = "26.04.10",
                    Icon = "FileWord",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Word\Options",
                            ValueName = "EnableCopilot",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-excel-copilot",
                    Name = "Excel Copilot",
                    Description = "Controls whether Copilot AI features are available in Microsoft Excel",
                    GroupName = "Microsoft Office AI",
                    AddedInVersion = "26.04.10",
                    Icon = "FileExcel",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Excel\Options",
                            ValueName = "EnableCopilot",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-onenote-copilot",
                    Name = "OneNote Copilot",
                    Description = "Controls whether Copilot AI features, Copilot notebooks, and Copilot skittle are available in Microsoft OneNote",
                    GroupName = "Microsoft Office AI",
                    AddedInVersion = "26.04.10",
                    Icon = "NotebookEdit",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\OneNote\Options\Other",
                            ValueName = "EnableCopilot",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\OneNote\Options\Other",
                            ValueName = "EnableCopilotNotebooks",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\OneNote\Options\Other",
                            ValueName = "EnableCopilotSkittle",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "privacy-office-content-safety-ai",
                    Name = "Office AI Content Safety",
                    Description = "Controls whether AI content safety features for alt text, rewrite, and summarization are available in Office apps",
                    GroupName = "Microsoft Office AI",
                    AddedInVersion = "26.04.10",
                    Icon = "TextBoxRemove",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\office\16.0\common\ai",
                            ValueName = "contentsafetyserviceenabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                    },
                },
            },
        };
    }
}

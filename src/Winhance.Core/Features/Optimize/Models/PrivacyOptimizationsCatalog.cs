using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;

namespace Winhance.Core.Features.Optimize.Models;

public static class PrivacyOptimizationsCatalog
{
    public const string FeatureId = FeatureIds.Privacy;
    public const string FeatureName = "Privacy & Security";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "security-uac-level",
            Display = new()
            {
                Name = "User Account Control Level",
                Description = "Controls UAC notification level and secure desktop behavior",
                GroupName = "Security",
                Icon = MaterialIcons.ShieldAccount,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ConsentPromptBehaviorAdmin", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" }, "ConsentPromptBehaviorAdmin", RegistryValueKind.DWord),
                new RegTarget("PromptOnSecureDesktop", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" }, "PromptOnSecureDesktop", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Prompt for Credentials",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ConsentPromptBehaviorAdmin"] = Of(1),
                        ["PromptOnSecureDesktop"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "Always notify",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ConsentPromptBehaviorAdmin"] = Of(2),
                        ["PromptOnSecureDesktop"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "Notify when apps try to make changes",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ConsentPromptBehaviorAdmin"] = Of(5),
                        ["PromptOnSecureDesktop"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "Notify when apps try to make changes (no dim)",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ConsentPromptBehaviorAdmin"] = Of(5),
                        ["PromptOnSecureDesktop"] = Of(0),
                    },
                },
                new SettingState
                {
                    Label = "Never notify",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ConsentPromptBehaviorAdmin"] = Of(0),
                        ["PromptOnSecureDesktop"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "security-workplace-join-messages",
            Display = new()
            {
                Name = "Workplace Join Message Prompts",
                Description = "Show 'Allow my organization to manage my device' prompts throughout Windows",
                GroupName = "Security",
                Icon = MaterialIcons.OfficeBuilding,
            },
            Targets = new Target[]
            {
                new RegTarget("BlockAADWorkplaceJoin", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin" }, "BlockAADWorkplaceJoin", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["BlockAADWorkplaceJoin"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["BlockAADWorkplaceJoin"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "security-bitlocker-auto-encryption",
            Display = new()
            {
                Name = "BitLocker Auto Encryption",
                Description = "Controls whether Windows can automatically encrypt drives with BitLocker. Has no effect if BitLocker encryption is already active on your device",
                GroupName = "Security",
                Icon = FluentIcons.LockClosedKey,
                IsSubjectivePreference = true,
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("PreventDeviceEncryption", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\BitLocker" }, "PreventDeviceEncryption", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["PreventDeviceEncryption"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["PreventDeviceEncryption"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "security-wifi-sense",
            Display = new()
            {
                Name = "WiFi-Sense",
                Description = "Allow sharing WiFi passwords with contacts and automatically connecting to suggested open hotspots",
                GroupName = "Security",
                Icon = MaterialIcons.WifiOff,
            },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting", @"HKEY_LOCAL_MACHINE\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots" }, "Value", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "security-automatic-maintenance",
            Display = new()
            {
                Name = "Automatic Maintenance",
                Description = "Choose if Windows should run automatic system maintenance tasks during idle time",
                GroupName = "Security",
                Icon = MaterialIcons.ProgressWrench,
            },
            Targets = new Target[]
            {
                new RegTarget("MaintenanceDisabled", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance" }, "MaintenanceDisabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MaintenanceDisabled"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MaintenanceDisabled"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "security-error-reporting",
            Display = new()
            {
                Name = "Windows Error Reporting",
                Description = "Choose if Windows should collect and send crash reports and error information to Microsoft",
                GroupName = "Security",
                Icon = FluentIcons.Bug,
            },
            Targets = new Target[]
            {
                new RegTarget("Disabled", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting" }, "Disabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Disabled"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Disabled"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "security-remote-assistance",
            Display = new()
            {
                Name = "Remote Assistance",
                Description = "Choose if other people can connect to your computer remotely to provide technical support",
                GroupName = "Security",
                Icon = MaterialIcons.RemoteDesktop,
            },
            Targets = new Target[]
            {
                new RegTarget("fAllowToGetHelp", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Remote Assistance" }, "fAllowToGetHelp", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["fAllowToGetHelp"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["fAllowToGetHelp"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "security-smart-app-control",
            Display = new()
            {
                Name = "Smart App Control",
                Description = "Controls the Smart App Control feature which blocks untrusted and potentially dangerous applications",
                GroupName = "Security",
                Icon = MaterialIcons.ShieldCheck,
                AddedInVersion = "26.04.01",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Between(22621, int.MaxValue) } },
            Targets = new Target[]
            {
                new RegTarget("VerifiedAndReputablePolicyState", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\CI\Policy" }, "VerifiedAndReputablePolicyState", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Off",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["VerifiedAndReputablePolicyState"] = Of(0) },
                },
                new SettingState
                {
                    Label = "On (Enforced)",
                    Set = new Dictionary<string, StateValue> { ["VerifiedAndReputablePolicyState"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Evaluation Mode",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["VerifiedAndReputablePolicyState"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "security-developer-mode",
            Display = new()
            {
                Name = "Developer Mode",
                Description = "Allows the installation of apps from any source, including loose files",
                GroupName = "Security",
                Icon = MaterialIcons.CodeBraces,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("AllowDevelopmentWithoutDevLicense", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\AppModelUnlock" }, "AllowDevelopmentWithoutDevLicense", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["AllowDevelopmentWithoutDevLicense"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowDevelopmentWithoutDevLicense"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "security-powershell-execution-policy",
            Display = new()
            {
                Name = "PowerShell Execution Policy",
                Description = "Controls whether PowerShell scripts are allowed to run and under what conditions for both the current user and the local machine",
                GroupName = "Security",
                Icon = MaterialIcons.Powershell,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ExecutionPolicy", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", @"HKEY_LOCAL_MACHINE\Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell" }, "ExecutionPolicy", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Restricted",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("Restricted").OrAbsent() },
                },
                new SettingState
                {
                    Label = "AllSigned",
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("AllSigned") },
                },
                new SettingState
                {
                    Label = "RemoteSigned",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("RemoteSigned") },
                },
                new SettingState
                {
                    Label = "Unrestricted",
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("Unrestricted") },
                },
                new SettingState
                {
                    Label = "Bypass",
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("Bypass") },
                },
            },
        },
        new()
        {
            Id = "privacy-ads-promotional-master",
            Display = new()
            {
                Name = "Ads, Suggestions and Promotional Content",
                Description = "Controls all advertising, suggestions, and promotional content throughout Windows",
                GroupName = "Content Delivery & Advertising",
                Icon = MaterialIcons.AdvertisementsOff,
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
            Targets = new Target[]
            {
                new RegTarget("AdsPromotionalContentMode", new[] { @"HKEY_CURRENT_USER\Software\Winhance\Settings" }, "AdsPromotionalContentMode", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Allow",
                    Set = new Dictionary<string, StateValue> { ["AdsPromotionalContentMode"] = Of(0) },
                    Controls = new Dictionary<string, string>
                    {
                        ["privacy-content-delivery-allowed"] = "Enabled",
                        ["privacy-subscribed-content"] = "Enabled",
                        ["privacy-feature-management"] = "Enabled",
                        ["privacy-soft-landing"] = "Enabled",
                        ["privacy-oem-preinstalled-apps"] = "Enabled",
                        ["privacy-preinstalled-apps"] = "Enabled",
                        ["privacy-preinstalled-apps-ever"] = "Enabled",
                        ["privacy-silent-installed-apps"] = "Enabled",
                        ["privacy-rotating-lock-screen"] = "Enabled",
                        ["privacy-lock-screen-overlay"] = "Enabled",
                        ["privacy-settings-content"] = "Enabled",
                        ["privacy-timeline-suggestions"] = "Enabled",
                        ["notifications-welcome-experience"] = "Enabled",
                        ["notifications-tips-suggestions"] = "Enabled",
                        ["notifications-system-pane-suggestions"] = "Enabled",
                        ["start-show-suggestions"] = "Enabled",
                    },
                },
                new SettingState
                {
                    Label = "Deny",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["AdsPromotionalContentMode"] = Of(1) },
                    Controls = new Dictionary<string, string>
                    {
                        ["privacy-content-delivery-allowed"] = "Disabled",
                        ["privacy-subscribed-content"] = "Disabled",
                        ["privacy-feature-management"] = "Disabled",
                        ["privacy-soft-landing"] = "Disabled",
                        ["privacy-oem-preinstalled-apps"] = "Disabled",
                        ["privacy-preinstalled-apps"] = "Disabled",
                        ["privacy-preinstalled-apps-ever"] = "Disabled",
                        ["privacy-silent-installed-apps"] = "Disabled",
                        ["privacy-rotating-lock-screen"] = "Disabled",
                        ["privacy-lock-screen-overlay"] = "Disabled",
                        ["privacy-settings-content"] = "Disabled",
                        ["privacy-timeline-suggestions"] = "Disabled",
                        ["notifications-welcome-experience"] = "Disabled",
                        ["notifications-tips-suggestions"] = "Disabled",
                        ["notifications-system-pane-suggestions"] = "Disabled",
                        ["start-show-suggestions"] = "Disabled",
                    },
                },
                new SettingState
                {
                    Label = "Custom",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AdsPromotionalContentMode"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "privacy-content-delivery-allowed",
            Display = new()
            {
                Name = "Content Delivery",
                Description = "Allows Windows to deliver promotional content and automatically install suggested apps",
                GroupName = "Content Delivery & Advertising",
                Icon = MaterialIcons.PackageVariant,
            },
            Targets = new Target[]
            {
                new RegTarget("ContentDeliveryAllowed", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "ContentDeliveryAllowed", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ContentDeliveryAllowed"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ContentDeliveryAllowed"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-subscribed-content",
            Display = new()
            {
                Name = "Subscribed Content",
                Description = "Enables promotional content subscriptions from Microsoft and partners throughout Windows",
                GroupName = "Content Delivery & Advertising",
                Icon = MaterialIcons.BookmarkMultiple,
            },
            Targets = new Target[]
            {
                new RegTarget("SubscribedContentEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SubscribedContentEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SubscribedContentEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SubscribedContentEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-feature-management",
            Display = new()
            {
                Name = "Feature Management",
                Description = "Enables Windows feature management functionality for promotional features and automatic app installations",
                GroupName = "Content Delivery & Advertising",
                Icon = MaterialIcons.MonitorArrowDown,
            },
            Targets = new Target[]
            {
                new RegTarget("FeatureManagementEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "FeatureManagementEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["FeatureManagementEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["FeatureManagementEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-soft-landing",
            Display = new()
            {
                Name = "Soft Landing Experiences",
                Description = "Displays tips and notifications about Windows features as you use the operating system",
                GroupName = "Content Delivery & Advertising",
                Icon = MaterialIcons.LightbulbOn,
            },
            Targets = new Target[]
            {
                new RegTarget("SoftLandingEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SoftLandingEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SoftLandingEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SoftLandingEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-oem-preinstalled-apps",
            Display = new()
            {
                Name = "OEM Pre-installed Apps",
                Description = "Prevents OEM manufacturers from automatically installing bloatware apps",
                GroupName = "Content Delivery & Advertising",
                Icon = MaterialIcons.PackageDown,
            },
            Targets = new Target[]
            {
                new RegTarget("OemPreInstalledAppsEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "OemPreInstalledAppsEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["OemPreInstalledAppsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["OemPreInstalledAppsEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-preinstalled-apps",
            Display = new()
            {
                Name = "Pre-installed Suggested Apps",
                Description = "Prevents Microsoft from automatically installing suggested apps",
                GroupName = "Content Delivery & Advertising",
                Icon = MaterialIcons.PackageVariantPlus,
            },
            Targets = new Target[]
            {
                new RegTarget("PreInstalledAppsEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "PreInstalledAppsEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["PreInstalledAppsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["PreInstalledAppsEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-preinstalled-apps-ever",
            Display = new()
            {
                Name = "Pre-installed Apps History Tracking",
                Description = "Disables tracking of whether pre-installed apps were ever enabled",
                GroupName = "Content Delivery & Advertising",
                Icon = MaterialIcons.ClipboardTextClockOutline,
            },
            Targets = new Target[]
            {
                new RegTarget("PreInstalledAppsEverEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "PreInstalledAppsEverEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["PreInstalledAppsEverEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["PreInstalledAppsEverEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-silent-installed-apps",
            Display = new()
            {
                Name = "Silent App Installation",
                Description = "Prevents apps from being silently installed in the background",
                GroupName = "Content Delivery & Advertising",
                Icon = MaterialIcons.CubeOffOutline,
            },
            Targets = new Target[]
            {
                new RegTarget("SilentInstalledAppsEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SilentInstalledAppsEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SilentInstalledAppsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SilentInstalledAppsEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-lock-screen",
            Display = new()
            {
                Name = "Lock Screen",
                Description = "Allows users to lock their computer using Windows+L, Start menu, or Ctrl+Alt+Del screen",
                GroupName = "Lock Screen",
                Icon = MaterialIcons.MonitorLock,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("DisableLockWorkstation", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon" }, "DisableLockWorkstation", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableLockWorkstation"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableLockWorkstation"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-rotating-lock-screen",
            Display = new()
            {
                Name = "Windows Spotlight on Lock Screen",
                Description = "Displays rotating Windows Spotlight images on your lock screen instead of a static background. Winhance automatically sets the Start Menu Recommended Section to Show when this setting is enabled as it is required",
                GroupName = "Lock Screen",
                Icon = FluentIcons.ImageCircle,
            },
            UiParentId = "privacy-lock-screen",
            Targets = new Target[]
            {
                new RegTarget("RotatingLockScreenEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "RotatingLockScreenEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Links = new[] { new Link("start-recommended-section", LinkKind.Requires, "Show") },
                    Set = new Dictionary<string, StateValue> { ["RotatingLockScreenEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["RotatingLockScreenEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-lock-screen-overlay",
            Display = new()
            {
                Name = "Lock Screen Fun Facts and Tips",
                Description = "Displays fun facts, tips, and tricks as an overlay on your lock screen",
                GroupName = "Lock Screen",
                Icon = MaterialIcons.MonitorShimmer,
            },
            UiParentId = "privacy-lock-screen",
            Targets = new Target[]
            {
                new RegTarget("RotatingLockScreenOverlayEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "RotatingLockScreenOverlayEnabled", RegistryValueKind.DWord),
                new RegTarget("SubscribedContent-338387Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SubscribedContent-338387Enabled", RegistryValueKind.DWord) { ApplyOnly = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["RotatingLockScreenOverlayEnabled"] = Of(1).OrAbsent(),
                        ["SubscribedContent-338387Enabled"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["RotatingLockScreenOverlayEnabled"] = Of(0),
                        ["SubscribedContent-338387Enabled"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-advertising-id",
            Display = new()
            {
                Name = "Let apps show me personalized ads by using my advertising ID",
                Description = "Windows generates a unique advertising ID that apps use to track your activity and deliver personalized ads based on your behavior across different apps",
                GroupName = "General",
                Icon = MaterialIcons.Advertisements,
            },
            Targets = new Target[]
            {
                new RegTarget("Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo" }, "Enabled", RegistryValueKind.DWord),
                new RegTarget("Value", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\AdvertisingInfo" }, "Value", RegistryValueKind.DWord) { ApplyOnly = true },
                new RegTarget("DisabledByGroupPolicy", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo" }, "DisabledByGroupPolicy", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Enabled"] = Of(1).OrAbsent(),
                        ["Value"] = Of(1),
                        ["DisabledByGroupPolicy"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Enabled"] = Of(0),
                        ["Value"] = Of(0),
                        ["DisabledByGroupPolicy"] = Of(1),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-language-list",
            Display = new()
            {
                Name = "Let websites show me locally relevant content by accessing my language list",
                Description = "Allows websites to access your language preferences so they can automatically display content in your preferred language without requiring manual configuration on each site",
                GroupName = "General",
                Icon = MaterialIcons.Translate,
            },
            Targets = new Target[]
            {
                new RegTarget("HttpAcceptLanguageOptOut", new[] { @"HKEY_CURRENT_USER\Control Panel\International\User Profile" }, "HttpAcceptLanguageOptOut", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HttpAcceptLanguageOptOut"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HttpAcceptLanguageOptOut"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-app-launch-tracking",
            Display = new()
            {
                Name = "Let Windows improve Start and search results by tracking app launches",
                Description = "Windows records which apps you use most frequently to personalize your Start menu and improve search results, making your most-used apps more accessible",
                GroupName = "General",
                Icon = MaterialIcons.ArchiveSearch,
            },
            Targets = new Target[]
            {
                new RegTarget("Start_TrackProgs", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "Start_TrackProgs", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start_TrackProgs"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Start_TrackProgs"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-settings-content",
            Display = new()
            {
                Name = "Show me suggested content in the Settings app",
                Description = "Displays promotional content, tips, and feature suggestions within the Windows Settings app. Winhance automatically sets the Start Menu Recommended Section to Show when this setting is enabled as it is required",
                GroupName = "General",
                Icon = MaterialIcons.StarCog,
            },
            Targets = new Target[]
            {
                new RegTarget("SubscribedContent-338393Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SubscribedContent-338393Enabled", RegistryValueKind.DWord),
                new RegTarget("SubscribedContent-353694Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SubscribedContent-353694Enabled", RegistryValueKind.DWord),
                new RegTarget("SubscribedContent-353696Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SubscribedContent-353696Enabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Links = new[] { new Link("start-recommended-section", LinkKind.Requires, "Show") },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["SubscribedContent-338393Enabled"] = Absent,
                        ["SubscribedContent-353694Enabled"] = Absent,
                        ["SubscribedContent-353696Enabled"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["SubscribedContent-338393Enabled"] = Of(0),
                        ["SubscribedContent-353694Enabled"] = Of(0),
                        ["SubscribedContent-353696Enabled"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-settings-notifications",
            Display = new()
            {
                Name = "Settings App Notifications",
                Description = "Shows account notifications in the Settings app, including prompts to reauthenticate, backup your device, and manage subscriptions",
                GroupName = "General",
                Icon = MaterialIcons.BellCog,
            },
            Targets = new Target[]
            {
                new RegTarget("EnableAccountNotifications", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications" }, "EnableAccountNotifications", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableAccountNotifications"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableAccountNotifications"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-speech-recognition",
            Display = new()
            {
                Name = "Online Speech Recognition",
                Description = "Use your voice for apps using Microsoft's online speech recognition technology",
                GroupName = "Speech",
                Icon = MaterialIcons.MicrophoneQuestion,
            },
            Targets = new Target[]
            {
                new RegTarget("HasAccepted", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy" }, "HasAccepted", RegistryValueKind.DWord),
                new RegTarget("AllowInputPersonalization", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\InputPersonalization", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization" }, "AllowInputPersonalization", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["HasAccepted"] = Of(1),
                        ["AllowInputPersonalization"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["HasAccepted"] = Of(0),
                        ["AllowInputPersonalization"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-narrator-online-services",
            Display = new()
            {
                Name = "Narrator Online Services",
                Description = "Allow Narrator to use Microsoft cloud services for features like intelligent image descriptions and enhanced voice models",
                GroupName = "Speech",
                Icon = MaterialIcons.CloudQuestion,
            },
            Targets = new Target[]
            {
                new RegTarget("OnlineServicesEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam" }, "OnlineServicesEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["OnlineServicesEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["OnlineServicesEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-narrator-scripting",
            Display = new()
            {
                Name = "Narrator Scripting Support",
                Description = "Allow Narrator to execute scripts for automation and custom functionality",
                GroupName = "Speech",
                Icon = MaterialIcons.ScriptText,
            },
            Targets = new Target[]
            {
                new RegTarget("ScriptingEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam" }, "ScriptingEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ScriptingEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ScriptingEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-inking-typing-dictionary",
            Display = new()
            {
                Name = "Custom Inking and Typing Dictionary",
                Description = "Uses your typing history and handwriting patterns to create a custom dictionary (turning off will clear all words in your custom dictionary)",
                GroupName = "Inking and typing personalization",
                Icon = FluentIcons.BookDefault,
            },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization" }, "Value", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("AcceptedPrivacyPolicy", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings" }, "AcceptedPrivacyPolicy", RegistryValueKind.DWord),
                new RegTarget("RestrictImplicitTextCollection", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization" }, "RestrictImplicitTextCollection", RegistryValueKind.DWord) { ApplyOnly = true },
                new RegTarget("HarvestContacts", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization\TrainedDataStore" }, "HarvestContacts", RegistryValueKind.DWord) { ApplyOnly = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of(1),
                        ["AcceptedPrivacyPolicy"] = Of(1),
                        ["RestrictImplicitTextCollection"] = Of(0),
                        ["HarvestContacts"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of(0),
                        ["AcceptedPrivacyPolicy"] = Of(0),
                        ["RestrictImplicitTextCollection"] = Of(1),
                        ["HarvestContacts"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-diagnostics",
            Display = new()
            {
                Name = "Send Diagnostic Data",
                Description = "Send diagnostic data to Microsoft to help improve Windows and keep it secure",
                GroupName = "Diagnostics & Feedback",
                Icon = FluentIcons.PulseSquare,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowedToastAtLevel", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack" }, "ShowedToastAtLevel", RegistryValueKind.DWord),
                new RegTarget("AllowTelemetry", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection", @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection" }, "AllowTelemetry", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("MaxTelemetryAllowed", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" }, "MaxTelemetryAllowed", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("AITEnable", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AppCompat", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppCompat" }, "AITEnable", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShowedToastAtLevel"] = Of(3).OrAbsent(),
                        ["AllowTelemetry"] = Of(3),
                        ["MaxTelemetryAllowed"] = Of(3),
                        ["AITEnable"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShowedToastAtLevel"] = Of(1).OrAbsent(),
                        ["AllowTelemetry"] = OneOf(0, 1),
                        ["MaxTelemetryAllowed"] = OneOf(0, 1),
                        ["AITEnable"] = Of(0).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-improve-inking-typing",
            Display = new()
            {
                Name = "Improve inking and typing",
                Description = "Send optional inking and typing diagnostic data to Microsoft",
                GroupName = "Diagnostics & Feedback",
                Icon = FluentIcons.PenSparkle,
            },
            Targets = new Target[]
            {
                new RegTarget("Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Input\TIPC" }, "Enabled", RegistryValueKind.DWord),
                new RegTarget("Value", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\ImproveInkingAndTyping" }, "Value", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Enabled"] = Of(1).OrAbsent(),
                        ["Value"] = Of(1).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Links = new[] { new Link("privacy-diagnostics", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Enabled"] = Of(0),
                        ["Value"] = Of(0).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-tailored-experiences",
            Display = new()
            {
                Name = "Tailored Experiences",
                Description = "Let Microsoft use your diagnostic data to show personalized tips, ads and recommendations",
                GroupName = "Diagnostics & Feedback",
                Icon = MaterialIcons.AccountCog,
            },
            Targets = new Target[]
            {
                new RegTarget("TailoredExperiencesWithDiagnosticDataEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Privacy" }, "TailoredExperiencesWithDiagnosticDataEnabled", RegistryValueKind.DWord),
                new RegTarget("DisableTailoredExperiencesWithDiagnosticData", new[] { @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent", @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\CloudContent" }, "DisableTailoredExperiencesWithDiagnosticData", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["TailoredExperiencesWithDiagnosticDataEnabled"] = OneOf(1, 2),
                        ["DisableTailoredExperiencesWithDiagnosticData"] = Of(0),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["TailoredExperiencesWithDiagnosticDataEnabled"] = Of(0),
                        ["DisableTailoredExperiencesWithDiagnosticData"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-feedback-frequency",
            Display = new()
            {
                Name = "Allow Windows to ask you for feedback",
                Description = "Let Windows ask you to provide feedback on experiences in Windows",
                GroupName = "Diagnostics & Feedback",
                Icon = FluentIcons.PersonFeedback,
            },
            Targets = new Target[]
            {
                new RegTarget("DoNotShowFeedbackNotifications", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection" }, "DoNotShowFeedbackNotifications", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("NumberOfSIUFInPeriod", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Siuf\Rules" }, "NumberOfSIUFInPeriod", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DoNotShowFeedbackNotifications"] = Absent,
                        ["NumberOfSIUFInPeriod"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DoNotShowFeedbackNotifications"] = Of(1),
                        ["NumberOfSIUFInPeriod"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-activity-history",
            Display = new()
            {
                Name = "Activity History",
                Description = "Allows you to jump back into what you were doing with apps, docs, or other activities on startup",
                GroupName = "Activity History",
                Icon = FluentIcons.Timeline,
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows10 } },
            Targets = new Target[]
            {
                new RegTarget("PublishUserActivities", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\System", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System" }, "PublishUserActivities", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["PublishUserActivities"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["PublishUserActivities"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-timeline-suggestions",
            Display = new()
            {
                Name = "Timeline Suggestions",
                Description = "Shows suggestions in the Windows 10 Timeline feature",
                GroupName = "Activity History",
                Icon = MaterialIcons.TimelineAlert,
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows10 } },
            Targets = new Target[]
            {
                new RegTarget("SubscribedContent-353698Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SubscribedContent-353698Enabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-353698Enabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-353698Enabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-search-history",
            Display = new()
            {
                Name = "Search history on this device",
                Description = "Improves search results by allowing Windows Search to store your search history locally on this device (Does not clear existing history)",
                GroupName = "Search permissions",
                Icon = MaterialIcons.MagnifyScan,
            },
            Targets = new Target[]
            {
                new RegTarget("IsDeviceSearchHistoryEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings" }, "IsDeviceSearchHistoryEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsDeviceSearchHistoryEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IsDeviceSearchHistoryEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-search-highlights",
            Display = new()
            {
                Name = "Show search highlights",
                Description = "See content suggestions in search",
                GroupName = "Search permissions",
                Icon = FluentIcons.SearchSparkle,
            },
            Targets = new Target[]
            {
                new RegTarget("IsDynamicSearchBoxEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings" }, "IsDynamicSearchBoxEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsDynamicSearchBoxEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IsDynamicSearchBoxEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-search-msa-cloud",
            Display = new()
            {
                Name = "Cloud Content Search for Microsoft account",
                Description = "Allow Windows Search to show results from apps and services that you are signed in to with your Microsoft account",
                GroupName = "Search permissions",
                Icon = MaterialIcons.CloudSearch,
            },
            Targets = new Target[]
            {
                new RegTarget("IsMSACloudSearchEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings" }, "IsMSACloudSearchEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsMSACloudSearchEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IsMSACloudSearchEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-search-aad-cloud",
            Display = new()
            {
                Name = "Cloud Content Search for Work or School account",
                Description = "Allow Windows Search to show results from apps and services that you are signed in to with your work or school account",
                GroupName = "Search permissions",
                Icon = MaterialIcons.BriefcaseSearch,
            },
            Targets = new Target[]
            {
                new RegTarget("IsAADCloudSearchEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings" }, "IsAADCloudSearchEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsAADCloudSearchEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IsAADCloudSearchEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-allow-cortana",
            Display = new()
            {
                Name = "Allow Cortana",
                Description = "Enables Microsoft's Cortana virtual assistant for voice commands and searches",
                GroupName = "Search permissions",
                Icon = FluentIcons.BotSparkle,
            },
            Targets = new Target[]
            {
                new RegTarget("AllowCortana", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Windows Search" }, "AllowCortana", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowCortana"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowCortana"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-location-services",
            Display = new()
            {
                Name = "Location Services",
                Description = "Allows Windows and apps to access your device location for location-based features",
                GroupName = "App Permissions",
                Icon = MaterialIcons.MapMarker,
            },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location" }, "Value", RegistryValueKind.String),
                new RegTarget("DisableLocation", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors" }, "DisableLocation", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of("Allow").OrAbsent(),
                        ["DisableLocation"] = Of(0),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of("Deny"),
                        ["DisableLocation"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-camera-access",
            Display = new()
            {
                Name = "Camera Access",
                Description = "Allow apps to have camera access",
                GroupName = "App Permissions",
                Icon = MaterialIcons.Camera,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam" }, "Value", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Allow").OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Deny") },
                },
            },
        },
        new()
        {
            Id = "privacy-microphone-access",
            Display = new()
            {
                Name = "Microphone Access",
                Description = "Allow apps to have microphone access",
                GroupName = "App Permissions",
                Icon = MaterialIcons.Microphone,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone" }, "Value", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Allow").OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Deny") },
                },
            },
        },
        new()
        {
            Id = "privacy-account-info-access",
            Display = new()
            {
                Name = "Account Info Access",
                Description = "Allow apps to have account info access",
                GroupName = "App Permissions",
                Icon = MaterialIcons.AccountLockOpen,
            },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userAccountInformation" }, "Value", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Allow").OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Deny") },
                },
            },
        },
        new()
        {
            Id = "privacy-app-diagnostic-access",
            Display = new()
            {
                Name = "App Diagnostic Access",
                Description = "Allow apps to have app diagnostic access",
                GroupName = "App Permissions",
                Icon = MaterialIcons.Stethoscope,
            },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics" }, "Value", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Allow").OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Deny") },
                },
            },
        },
        new()
        {
            Id = "privacy-onedrive-auto-backup",
            Display = new()
            {
                Name = "OneDrive Automatic Backups",
                Description = "Controls whether OneDrive automatically backs up your Documents, Pictures, and Desktop folders. Has no effect if OneDrive backups are already active on your device",
                GroupName = "App Permissions",
                Icon = MaterialIcons.CloudOff,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("KFMBlockOptIn", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\OneDrive", @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\OneDrive" }, "KFMBlockOptIn", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["KFMBlockOptIn"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["KFMBlockOptIn"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-turn-off-copilot",
            Display = new()
            {
                Name = "Windows Copilot",
                Description = "Controls whether Windows Copilot is available system-wide via group policy for both current user and local machine",
                GroupName = "Windows AI",
                Icon = MaterialIcons.Robot,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("TurnOffWindowsCopilot", new[] { @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsCopilot", @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\WindowsCopilot" }, "TurnOffWindowsCopilot", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["TurnOffWindowsCopilot"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TurnOffWindowsCopilot"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-ai-data-analysis",
            Display = new()
            {
                Name = "AI Data Analysis",
                Description = "Controls whether Windows AI can analyze user data for personalization and recommendations",
                GroupName = "Windows AI",
                Icon = MaterialIcons.DatabaseOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableAIDataAnalysis", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" }, "DisableAIDataAnalysis", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableAIDataAnalysis"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableAIDataAnalysis"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-block-recall-enablement",
            Display = new()
            {
                Name = "Recall Enablement",
                Description = "Controls whether Windows Recall can be enabled via policy",
                GroupName = "Windows AI",
                Icon = MaterialIcons.Cancel,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("AllowRecallEnablement", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" }, "AllowRecallEnablement", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowRecallEnablement"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowRecallEnablement"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-recall-snapshots",
            Display = new()
            {
                Name = "Recall Saving Snapshots",
                Description = "Allows Windows Recall to save screenshots of your activity for later recall",
                GroupName = "Windows AI",
                Icon = MaterialIcons.CameraOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("TurnOffSavingSnapshots", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" }, "TurnOffSavingSnapshots", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TurnOffSavingSnapshots"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TurnOffSavingSnapshots"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-click-to-do",
            Display = new()
            {
                Name = "Click to Do",
                Description = "Controls whether the Click to Do AI feature is available in Windows",
                GroupName = "Windows AI",
                Icon = MaterialIcons.CursorDefaultClickOutline,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableClickToDo", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" }, "DisableClickToDo", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableClickToDo"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableClickToDo"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-settings-agent",
            Display = new()
            {
                Name = "AI Settings Agent",
                Description = "Controls whether the AI-powered Settings Agent is available in Windows",
                GroupName = "Windows AI",
                Icon = MaterialIcons.CogOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableSettingsAgent", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" }, "DisableSettingsAgent", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableSettingsAgent"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableSettingsAgent"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-agent-connectors",
            Display = new()
            {
                Name = "AI Agent Connectors",
                Description = "Controls whether AI agents can use connectors to access external services",
                GroupName = "Windows AI",
                Icon = MaterialIcons.VectorPolylineRemove,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableAgentConnectors", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" }, "DisableAgentConnectors", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableAgentConnectors"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableAgentConnectors"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-agent-workspaces",
            Display = new()
            {
                Name = "AI Agent Workspaces",
                Description = "Controls whether AI Agent Workspaces are available in Windows",
                GroupName = "Windows AI",
                Icon = MaterialIcons.DesktopClassic,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableAgentWorkspaces", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" }, "DisableAgentWorkspaces", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableAgentWorkspaces"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableAgentWorkspaces"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-remote-agent-connectors",
            Display = new()
            {
                Name = "Remote AI Agent Connectors",
                Description = "Controls whether AI agents can use remote connectors to access remote services",
                GroupName = "Windows AI",
                Icon = MaterialIcons.LanDisconnect,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableRemoteAgentConnectors", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" }, "DisableRemoteAgentConnectors", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableRemoteAgentConnectors"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableRemoteAgentConnectors"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-copilot-hardware-key",
            Display = new()
            {
                Name = "Copilot Hardware Key",
                Description = "Controls whether the dedicated Copilot key on keyboards opens Copilot",
                GroupName = "Windows AI",
                Icon = MaterialIcons.KeyboardOutline,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("SetCopilotHardwareKey", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CopilotKey" }, "SetCopilotHardwareKey", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SetCopilotHardwareKey"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SetCopilotHardwareKey"] = Of("") },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-copilot-runtime",
            Display = new()
            {
                Name = "Copilot Runtime",
                Description = "Controls whether the Copilot runtime is allowed to run via policy",
                GroupName = "Windows AI",
                Icon = MaterialIcons.RobotOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("AllowCopilotRuntime", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI" }, "AllowCopilotRuntime", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowCopilotRuntime"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowCopilotRuntime"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-copilot-unavailable",
            Display = new()
            {
                Name = "Copilot Availability",
                Description = "Controls whether Copilot is available in the Windows Shell",
                GroupName = "Windows AI",
                Icon = MaterialIcons.RobotOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("IsCopilotAvailable", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Copilot" }, "IsCopilotAvailable", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["IsCopilotAvailable"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IsCopilotAvailable"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-bing-chat",
            Display = new()
            {
                Name = "Bing Chat Eligibility",
                Description = "Controls whether the user is eligible for Bing Chat and Copilot in Search",
                GroupName = "Windows AI",
                Icon = MaterialIcons.ChatRemove,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("IsUserEligible", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Copilot\BingChat" }, "IsUserEligible", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["IsUserEligible"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IsUserEligible"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-deny-generative-ai-access",
            Display = new()
            {
                Name = "Generative AI Access",
                Description = "Controls whether apps can access the generative AI capability on your device",
                GroupName = "Windows AI",
                Icon = MaterialIcons.ShieldLock,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\generativeAI" }, "Value", RegistryValueKind.String),
                new RegTarget("LetAppsAccessGenerativeAI", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy" }, "LetAppsAccessGenerativeAI", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of("Allow").OrAbsent(),
                        ["LetAppsAccessGenerativeAI"] = Of(0).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of("Deny"),
                        ["LetAppsAccessGenerativeAI"] = Of(2),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-deny-system-ai-models",
            Display = new()
            {
                Name = "System AI Models Access",
                Description = "Controls whether apps can access system AI models on your device and collect usage data",
                GroupName = "Windows AI",
                Icon = MaterialIcons.ShieldLock,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\systemAIModels" }, "Value", RegistryValueKind.String),
                new RegTarget("LetAppsAccessSystemAIModels", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy" }, "LetAppsAccessSystemAIModels", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("RecordUsageData", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\systemAIModels" }, "RecordUsageData", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of("Allow").OrAbsent(),
                        ["LetAppsAccessSystemAIModels"] = Of(0).OrAbsent(),
                        ["RecordUsageData"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of("Deny"),
                        ["LetAppsAccessSystemAIModels"] = Of(2),
                        ["RecordUsageData"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-deny-copilot-microphone",
            Display = new()
            {
                Name = "Copilot Microphone Access",
                Description = "Controls whether Copilot and Office Hub apps have microphone permission",
                GroupName = "Windows AI",
                Icon = MaterialIcons.MicrophoneOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("Value", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\Microsoft.Copilot_8wekyb3d8bbwe", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\Microsoft.MicrosoftOfficeHub_8wekyb3d8bbwe" }, "Value", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = OneOf("Allow", "Prompt").OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Deny") },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-paint-ai-image-creator",
            Display = new()
            {
                Name = "Paint AI Image Creator",
                Description = "Controls whether the AI Image Creator feature is available in Microsoft Paint",
                GroupName = "Windows AI",
                Icon = MaterialIcons.ImageOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableImageCreator", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint" }, "DisableImageCreator", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableImageCreator"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableImageCreator"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-paint-ai-cocreator",
            Display = new()
            {
                Name = "Paint AI Cocreator",
                Description = "Controls whether the AI Cocreator feature is available in Microsoft Paint",
                GroupName = "Windows AI",
                Icon = MaterialIcons.PaletteOutline,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableCocreator", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint" }, "DisableCocreator", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableCocreator"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableCocreator"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-paint-generative-fill",
            Display = new()
            {
                Name = "Paint Generative Fill",
                Description = "Controls whether the AI Generative Fill feature is available in Microsoft Paint",
                GroupName = "Windows AI",
                Icon = MaterialIcons.FormatPaint,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableGenerativeFill", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint" }, "DisableGenerativeFill", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableGenerativeFill"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableGenerativeFill"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-paint-generative-erase",
            Display = new()
            {
                Name = "Paint Generative Erase",
                Description = "Controls whether the AI Generative Erase feature is available in Microsoft Paint",
                GroupName = "Windows AI",
                Icon = MaterialIcons.EraserVariant,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableGenerativeErase", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint" }, "DisableGenerativeErase", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableGenerativeErase"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableGenerativeErase"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-paint-remove-background",
            Display = new()
            {
                Name = "Paint Remove Background",
                Description = "Controls whether the AI Remove Background feature is available in Microsoft Paint",
                GroupName = "Windows AI",
                Icon = MaterialIcons.ImageRemove,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableRemoveBackground", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint" }, "DisableRemoveBackground", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableRemoveBackground"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableRemoveBackground"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-input-insights",
            Display = new()
            {
                Name = "Input Insights",
                Description = "Controls whether Windows Input Insights can track typing patterns and provide suggestions",
                GroupName = "Windows AI",
                Icon = MaterialIcons.KeyboardOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("InsightsEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\input\Settings" }, "InsightsEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["InsightsEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["InsightsEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-copilot-nudges",
            Display = new()
            {
                Name = "Copilot Nudges",
                Description = "Controls whether Copilot promotional nudges and background task notifications are shown",
                GroupName = "Windows AI",
                Icon = MaterialIcons.BellOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("ShowCopilotNudges", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowCopilotNudges", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowCopilotNudges"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowCopilotNudges"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-disable-consumer-ai-content",
            Display = new()
            {
                Name = "AI Consumer Content",
                Description = "Controls whether AI-driven consumer account content recommendations are shown",
                GroupName = "Windows AI",
                Icon = MaterialIcons.AccountOff,
                AddedInVersion = "26.04.10",
            },
            Availability = new Availability { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("DisableConsumerAccountStateContent", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent" }, "DisableConsumerAccountStateContent", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableConsumerAccountStateContent"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableConsumerAccountStateContent"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-copilot-cdp-page-context",
            Display = new()
            {
                Name = "Edge Copilot CDP Page Context",
                Description = "Controls whether Copilot can use CDP to access page content in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.WebOff,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("CopilotCDPPageContext", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "CopilotCDPPageContext", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["CopilotCDPPageContext"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["CopilotCDPPageContext"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-copilot-page-context",
            Display = new()
            {
                Name = "Edge Copilot Page Context",
                Description = "Controls whether Copilot can read page content in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.FileEyeOutline,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("CopilotPageContext", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "CopilotPageContext", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["CopilotPageContext"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["CopilotPageContext"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-copilot-sidebar",
            Display = new()
            {
                Name = "Edge Copilot Sidebar",
                Description = "Controls whether the Copilot sidebar is available in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.DockRight,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("HubsSidebarEnabled", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "HubsSidebarEnabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HubsSidebarEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HubsSidebarEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-entra-copilot",
            Display = new()
            {
                Name = "Edge Entra Copilot Page Context",
                Description = "Controls whether Entra Copilot can access page context in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.ShieldOff,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("EdgeEntraCopilotPageContext", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "EdgeEntraCopilotPageContext", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EdgeEntraCopilotPageContext"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EdgeEntraCopilotPageContext"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-m365-copilot-icon",
            Display = new()
            {
                Name = "Edge M365 Copilot Chat Icon",
                Description = "Controls whether the Microsoft 365 Copilot chat icon is shown in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.ChatMinus,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("Microsoft365CopilotChatIconEnabled", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "Microsoft365CopilotChatIconEnabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Microsoft365CopilotChatIconEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Microsoft365CopilotChatIconEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-ai-history-search",
            Display = new()
            {
                Name = "Edge AI History Search",
                Description = "Controls whether AI-powered history search is available in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.History,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("EdgeHistoryAISearchEnabled", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "EdgeHistoryAISearchEnabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EdgeHistoryAISearchEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EdgeHistoryAISearchEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-inline-compose",
            Display = new()
            {
                Name = "Edge Inline AI Compose",
                Description = "Controls whether AI-powered inline compose suggestions are available in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.PenOff,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("ComposeInlineEnabled", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "ComposeInlineEnabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ComposeInlineEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ComposeInlineEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-local-ai-model",
            Display = new()
            {
                Name = "Edge Local AI Model Settings",
                Description = "Controls whether local AI model settings are available in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.DatabaseOff,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("GenAILocalFoundationalModelSettings", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "GenAILocalFoundationalModelSettings", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["GenAILocalFoundationalModelSettings"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["GenAILocalFoundationalModelSettings"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-builtin-ai-apis",
            Display = new()
            {
                Name = "Edge Built-in AI APIs",
                Description = "Controls whether built-in AI APIs are available in Microsoft Edge for websites to use",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.Api,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("BuiltInAIAPIsEnabled", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "BuiltInAIAPIsEnabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["BuiltInAIAPIsEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["BuiltInAIAPIsEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-ai-themes",
            Display = new()
            {
                Name = "Edge AI Generated Themes",
                Description = "Controls whether AI-generated themes are available in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.PaletteOutline,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("AIGenThemesEnabled", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "AIGenThemesEnabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AIGenThemesEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AIGenThemesEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-devtools-ai",
            Display = new()
            {
                Name = "Edge DevTools AI",
                Description = "Controls whether AI features are available in Edge DevTools",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.CodeBracesBox,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("DevToolsGenAiSettings", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "DevToolsGenAiSettings", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DevToolsGenAiSettings"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DevToolsGenAiSettings"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "privacy-edge-share-history-copilot",
            Display = new()
            {
                Name = "Edge Share History with Copilot",
                Description = "Controls whether browsing history is shared with Copilot search in Microsoft Edge",
                GroupName = "Microsoft Edge AI",
                Icon = MaterialIcons.ShareOff,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("ShareBrowsingHistoryWithCopilotSearchAllowed", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge" }, "ShareBrowsingHistoryWithCopilotSearchAllowed", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShareBrowsingHistoryWithCopilotSearchAllowed"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShareBrowsingHistoryWithCopilotSearchAllowed"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-office-ai-training",
            Display = new()
            {
                Name = "Office AI Training",
                Description = "Controls whether Office collects AI training data from your usage",
                GroupName = "Microsoft Office AI",
                Icon = MaterialIcons.SchoolOutline,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("optionalconnectedexperiencesenabled", new[] { @"HKEY_CURRENT_USER\Software\Policies\Microsoft\office\16.0\common\ai\training" }, "optionalconnectedexperiencesenabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["optionalconnectedexperiencesenabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["optionalconnectedexperiencesenabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-office-connected-services",
            Display = new()
            {
                Name = "Office Connected Services",
                Description = "Controls whether Office connected experiences and AI-powered services are available",
                GroupName = "Microsoft Office AI",
                Icon = MaterialIcons.CloudOff,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("controllerconnectedservicesenabled", new[] { @"HKEY_CURRENT_USER\Software\Policies\Microsoft\office\16.0\common\privacy" }, "controllerconnectedservicesenabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("usercontentdisabled", new[] { @"HKEY_CURRENT_USER\Software\Policies\Microsoft\office\16.0\common\privacy" }, "usercontentdisabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["controllerconnectedservicesenabled"] = Of(0).OrAbsent(),
                        ["usercontentdisabled"] = Of(0).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["controllerconnectedservicesenabled"] = Of(2),
                        ["usercontentdisabled"] = Of(2),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-word-copilot",
            Display = new()
            {
                Name = "Word Copilot",
                Description = "Controls whether Copilot AI features are available in Microsoft Word",
                GroupName = "Microsoft Office AI",
                Icon = MaterialIcons.FileWord,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("EnableCopilot", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Word\Options" }, "EnableCopilot", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableCopilot"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableCopilot"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-excel-copilot",
            Display = new()
            {
                Name = "Excel Copilot",
                Description = "Controls whether Copilot AI features are available in Microsoft Excel",
                GroupName = "Microsoft Office AI",
                Icon = MaterialIcons.FileExcel,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("EnableCopilot", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Excel\Options" }, "EnableCopilot", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableCopilot"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableCopilot"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "privacy-onenote-copilot",
            Display = new()
            {
                Name = "OneNote Copilot",
                Description = "Controls whether Copilot AI features, Copilot notebooks, and Copilot skittle are available in Microsoft OneNote",
                GroupName = "Microsoft Office AI",
                Icon = MaterialIcons.NotebookEdit,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("EnableCopilot", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\OneNote\Options\Other" }, "EnableCopilot", RegistryValueKind.DWord),
                new RegTarget("EnableCopilotNotebooks", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\OneNote\Options\Other" }, "EnableCopilotNotebooks", RegistryValueKind.DWord),
                new RegTarget("EnableCopilotSkittle", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\OneNote\Options\Other" }, "EnableCopilotSkittle", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["EnableCopilot"] = Absent,
                        ["EnableCopilotNotebooks"] = Absent,
                        ["EnableCopilotSkittle"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["EnableCopilot"] = Of(0),
                        ["EnableCopilotNotebooks"] = Of(0),
                        ["EnableCopilotSkittle"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "privacy-office-content-safety-ai",
            Display = new()
            {
                Name = "Office AI Content Safety",
                Description = "Controls whether AI content safety features for alt text, rewrite, and summarization are available in Office apps",
                GroupName = "Microsoft Office AI",
                Icon = MaterialIcons.TextBoxRemove,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[]
            {
                new RegTarget("contentsafetyserviceenabled", new[] { @"HKEY_CURRENT_USER\Software\Policies\Microsoft\office\16.0\common\ai" }, "contentsafetyserviceenabled", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["contentsafetyserviceenabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["contentsafetyserviceenabled"] = Of(0) },
                },
            },
        },
    };
}

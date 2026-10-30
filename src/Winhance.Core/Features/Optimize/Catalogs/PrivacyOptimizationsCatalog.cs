using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Optimize.Catalogs;

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
                Name = LocKey.Setting.SecurityUacLevel.Name,
                Description = LocKey.Setting.SecurityUacLevel.Description,
                GroupName = LocKey.SettingGroup.Security,
                Icon = MaterialIcons.ShieldAccount,
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
                    Label = LocKey.Setting.SecurityUacLevel.Option0,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ConsentPromptBehaviorAdmin"] = Of(1),
                        ["PromptOnSecureDesktop"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SecurityUacLevel.Option1,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ConsentPromptBehaviorAdmin"] = Of(2),
                        ["PromptOnSecureDesktop"] = Of(1),
                    },
                },
                new SettingState
                {
                    // Recommended is the Windows default. "Never notify" (0/0) elevates everything
                    // silently, which is what #743 objected to - it stays available, we just stop
                    // applying it for people who press Apply Recommended.
                    Label = LocKey.Setting.SecurityUacLevel.Option2,
                    Roles = new[] { StateRole.WindowsDefault, StateRole.Recommended },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ConsentPromptBehaviorAdmin"] = Of(5),
                        ["PromptOnSecureDesktop"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SecurityUacLevel.Option3,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ConsentPromptBehaviorAdmin"] = Of(5),
                        ["PromptOnSecureDesktop"] = Of(0),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SecurityUacLevel.Option4,
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
                Name = LocKey.Setting.SecurityWorkplaceJoinMessages.Name,
                Description = LocKey.Setting.SecurityWorkplaceJoinMessages.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["BlockAADWorkplaceJoin"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SecurityBitlockerAutoEncryption.Name,
                Description = LocKey.Setting.SecurityBitlockerAutoEncryption.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["PreventDeviceEncryption"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SecurityWifiSense.Name,
                Description = LocKey.Setting.SecurityWifiSense.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SecurityAutomaticMaintenance.Name,
                Description = LocKey.Setting.SecurityAutomaticMaintenance.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MaintenanceDisabled"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SecurityErrorReporting.Name,
                Description = LocKey.Setting.SecurityErrorReporting.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Disabled"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SecurityRemoteAssistance.Name,
                Description = LocKey.Setting.SecurityRemoteAssistance.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["fAllowToGetHelp"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SecuritySmartAppControl.Name,
                Description = LocKey.Setting.SecuritySmartAppControl.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Setting.SecuritySmartAppControl.Option0,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["VerifiedAndReputablePolicyState"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SecuritySmartAppControl.Option1,
                    Set = new Dictionary<string, StateValue> { ["VerifiedAndReputablePolicyState"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SecuritySmartAppControl.Option2,
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
                Name = LocKey.Setting.SecurityDeveloperMode.Name,
                Description = LocKey.Setting.SecurityDeveloperMode.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["AllowDevelopmentWithoutDevLicense"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowDevelopmentWithoutDevLicense"] = Of(0) },
                    ResetSet = new Dictionary<string, StateValue> { ["AllowDevelopmentWithoutDevLicense"] = Absent },
                },
            },
        },
        new()
        {
            Id = "security-powershell-execution-policy",
            Display = new()
            {
                Name = LocKey.Setting.SecurityPowershellExecutionPolicy.Name,
                Description = LocKey.Setting.SecurityPowershellExecutionPolicy.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Setting.SecurityPowershellExecutionPolicy.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("Restricted").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SecurityPowershellExecutionPolicy.Option1,
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("AllSigned") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SecurityPowershellExecutionPolicy.Option2,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("RemoteSigned") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SecurityPowershellExecutionPolicy.Option3,
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("Unrestricted") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.SecurityPowershellExecutionPolicy.Option4,
                    Set = new Dictionary<string, StateValue> { ["ExecutionPolicy"] = Of("Bypass") },
                },
            },
        },
        new()
        {
            Id = "privacy-ads-promotional-master",
            Display = new()
            {
                Name = LocKey.Setting.PrivacyAdsPromotionalMaster.Name,
                Description = LocKey.Setting.PrivacyAdsPromotionalMaster.Description,
                GroupName = LocKey.SettingGroup.ContentDeliveryAdvertising,
                Icon = MaterialIcons.AdvertisementsOff,
                CrossGroupChildSettings = new Dictionary<string, LocKey>
                {
                    ["privacy-rotating-lock-screen"] = LocKey.Setting.PrivacyAdsPromotionalMaster.ChildSpotlight,
                    ["privacy-lock-screen-overlay"] = LocKey.Setting.PrivacyAdsPromotionalMaster.ChildFunFactsTips,
                    ["privacy-settings-content"] = LocKey.Setting.PrivacyAdsPromotionalMaster.ChildSuggestedContent,
                    ["privacy-timeline-suggestions"] = LocKey.Setting.PrivacyAdsPromotionalMaster.ChildTimelineSuggestions,
                    ["notifications-welcome-experience"] = LocKey.Setting.PrivacyAdsPromotionalMaster.ChildWelcomeExperience,
                    ["notifications-tips-suggestions"] = LocKey.Setting.PrivacyAdsPromotionalMaster.ChildTipsSuggestions,
                    ["notifications-system-pane-suggestions"] = LocKey.Setting.PrivacyAdsPromotionalMaster.ChildNotificationCenterSuggestions,
                    ["start-show-suggestions"] = LocKey.Setting.PrivacyAdsPromotionalMaster.ChildStartSuggestions,
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
                    Label = LocKey.Setting.PrivacyAdsPromotionalMaster.Option0,
                    Set = new Dictionary<string, StateValue> { ["AdsPromotionalContentMode"] = Of(0) },
                    Controls = new Dictionary<string, LocKey>
                    {
                        ["privacy-content-delivery-allowed"] = LocKey.Common.Enabled,
                        ["privacy-subscribed-content"] = LocKey.Common.Enabled,
                        ["privacy-feature-management"] = LocKey.Common.Enabled,
                        ["privacy-soft-landing"] = LocKey.Common.Enabled,
                        ["privacy-oem-preinstalled-apps"] = LocKey.Common.Enabled,
                        ["privacy-preinstalled-apps"] = LocKey.Common.Enabled,
                        ["privacy-preinstalled-apps-ever"] = LocKey.Common.Enabled,
                        ["privacy-silent-installed-apps"] = LocKey.Common.Enabled,
                        ["privacy-rotating-lock-screen"] = LocKey.Common.Enabled,
                        ["privacy-lock-screen-overlay"] = LocKey.Common.Enabled,
                        ["privacy-settings-content"] = LocKey.Common.Enabled,
                        ["privacy-timeline-suggestions"] = LocKey.Common.Enabled,
                        ["notifications-welcome-experience"] = LocKey.Common.Enabled,
                        ["notifications-tips-suggestions"] = LocKey.Common.Enabled,
                        ["notifications-system-pane-suggestions"] = LocKey.Common.Enabled,
                        ["start-show-suggestions"] = LocKey.Common.Enabled,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.PrivacyAdsPromotionalMaster.Option1,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["AdsPromotionalContentMode"] = Of(1) },
                    Controls = new Dictionary<string, LocKey>
                    {
                        ["privacy-content-delivery-allowed"] = LocKey.Common.Disabled,
                        ["privacy-subscribed-content"] = LocKey.Common.Disabled,
                        ["privacy-feature-management"] = LocKey.Common.Disabled,
                        ["privacy-soft-landing"] = LocKey.Common.Disabled,
                        ["privacy-oem-preinstalled-apps"] = LocKey.Common.Disabled,
                        ["privacy-preinstalled-apps"] = LocKey.Common.Disabled,
                        ["privacy-preinstalled-apps-ever"] = LocKey.Common.Disabled,
                        ["privacy-silent-installed-apps"] = LocKey.Common.Disabled,
                        ["privacy-rotating-lock-screen"] = LocKey.Common.Disabled,
                        ["privacy-lock-screen-overlay"] = LocKey.Common.Disabled,
                        ["privacy-settings-content"] = LocKey.Common.Disabled,
                        ["privacy-timeline-suggestions"] = LocKey.Common.Disabled,
                        ["notifications-welcome-experience"] = LocKey.Common.Disabled,
                        ["notifications-tips-suggestions"] = LocKey.Common.Disabled,
                        ["notifications-system-pane-suggestions"] = LocKey.Common.Disabled,
                        ["start-show-suggestions"] = LocKey.Common.Disabled,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.PrivacyAdsPromotionalMaster.Option2,
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
                Name = LocKey.Setting.PrivacyContentDeliveryAllowed.Name,
                Description = LocKey.Setting.PrivacyContentDeliveryAllowed.Description,
                GroupName = LocKey.SettingGroup.ContentDeliveryAdvertising,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ContentDeliveryAllowed"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySubscribedContent.Name,
                Description = LocKey.Setting.PrivacySubscribedContent.Description,
                GroupName = LocKey.SettingGroup.ContentDeliveryAdvertising,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SubscribedContentEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyFeatureManagement.Name,
                Description = LocKey.Setting.PrivacyFeatureManagement.Description,
                GroupName = LocKey.SettingGroup.ContentDeliveryAdvertising,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["FeatureManagementEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySoftLanding.Name,
                Description = LocKey.Setting.PrivacySoftLanding.Description,
                GroupName = LocKey.SettingGroup.ContentDeliveryAdvertising,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SoftLandingEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyOemPreinstalledApps.Name,
                Description = LocKey.Setting.PrivacyOemPreinstalledApps.Description,
                GroupName = LocKey.SettingGroup.ContentDeliveryAdvertising,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["OemPreInstalledAppsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyPreinstalledApps.Name,
                Description = LocKey.Setting.PrivacyPreinstalledApps.Description,
                GroupName = LocKey.SettingGroup.ContentDeliveryAdvertising,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["PreInstalledAppsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyPreinstalledAppsEver.Name,
                Description = LocKey.Setting.PrivacyPreinstalledAppsEver.Description,
                GroupName = LocKey.SettingGroup.ContentDeliveryAdvertising,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["PreInstalledAppsEverEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySilentInstalledApps.Name,
                Description = LocKey.Setting.PrivacySilentInstalledApps.Description,
                GroupName = LocKey.SettingGroup.ContentDeliveryAdvertising,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SilentInstalledAppsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyLockScreen.Name,
                Description = LocKey.Setting.PrivacyLockScreen.Description,
                GroupName = LocKey.SettingGroup.LockScreen,
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
                    // Recommended is the Windows default. Disabling this takes Win+L away entirely (#749).
                    // Of(0) is the right write, not a leftover: every clean-install probe in docs/probe-data
                    // has DisableLockWorkstation PRESENT at 0, so writing it restores what Windows ships.
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault, StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["DisableLockWorkstation"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyRotatingLockScreen.Name,
                Description = LocKey.Setting.PrivacyRotatingLockScreen.Description,
                GroupName = LocKey.SettingGroup.LockScreen,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Links = new[] { new Link("start-recommended-section", LinkKind.Requires, LocKey.Setting.StartRecommendedSection.Option0) },
                    Set = new Dictionary<string, StateValue> { ["RotatingLockScreenEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyLockScreenOverlay.Name,
                Description = LocKey.Setting.PrivacyLockScreenOverlay.Description,
                GroupName = LocKey.SettingGroup.LockScreen,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["RotatingLockScreenOverlayEnabled"] = Of(1).OrAbsent(),
                        ["SubscribedContent-338387Enabled"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyAdvertisingId.Name,
                Description = LocKey.Setting.PrivacyAdvertisingId.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyLanguageList.Name,
                Description = LocKey.Setting.PrivacyLanguageList.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HttpAcceptLanguageOptOut"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyAppLaunchTracking.Name,
                Description = LocKey.Setting.PrivacyAppLaunchTracking.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start_TrackProgs"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySettingsContent.Name,
                Description = LocKey.Setting.PrivacySettingsContent.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Links = new[] { new Link("start-recommended-section", LinkKind.Requires, LocKey.Setting.StartRecommendedSection.Option0) },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["SubscribedContent-338393Enabled"] = Absent,
                        ["SubscribedContent-353694Enabled"] = Absent,
                        ["SubscribedContent-353696Enabled"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySettingsNotifications.Name,
                Description = LocKey.Setting.PrivacySettingsNotifications.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableAccountNotifications"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySpeechRecognition.Name,
                Description = LocKey.Setting.PrivacySpeechRecognition.Description,
                GroupName = LocKey.SettingGroup.Speech,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["HasAccepted"] = Of(1),
                        ["AllowInputPersonalization"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["HasAccepted"] = Of(0),
                        ["AllowInputPersonalization"] = Absent,
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["HasAccepted"] = Absent,
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
                Name = LocKey.Setting.PrivacyNarratorOnlineServices.Name,
                Description = LocKey.Setting.PrivacyNarratorOnlineServices.Description,
                GroupName = LocKey.SettingGroup.Speech,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["OnlineServicesEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyNarratorScripting.Name,
                Description = LocKey.Setting.PrivacyNarratorScripting.Description,
                GroupName = LocKey.SettingGroup.Speech,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ScriptingEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyInkingTypingDictionary.Name,
                Description = LocKey.Setting.PrivacyInkingTypingDictionary.Description,
                GroupName = LocKey.SettingGroup.InkingAndTypingPersonalization,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDiagnostics.Name,
                Description = LocKey.Setting.PrivacyDiagnostics.Description,
                GroupName = LocKey.SettingGroup.DiagnosticsFeedback,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyImproveInkingTyping.Name,
                Description = LocKey.Setting.PrivacyImproveInkingTyping.Description,
                GroupName = LocKey.SettingGroup.DiagnosticsFeedback,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Enabled"] = Of(1).OrAbsent(),
                        ["Value"] = Of(1).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Links = new[] { new Link("privacy-diagnostics", LinkKind.Requires, LocKey.Common.Enabled) },
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
                Name = LocKey.Setting.PrivacyTailoredExperiences.Name,
                Description = LocKey.Setting.PrivacyTailoredExperiences.Description,
                GroupName = LocKey.SettingGroup.DiagnosticsFeedback,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["TailoredExperiencesWithDiagnosticDataEnabled"] = OneOf(1, 2),
                        ["DisableTailoredExperiencesWithDiagnosticData"] = Of(0).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue> { ["DisableTailoredExperiencesWithDiagnosticData"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyFeedbackFrequency.Name,
                Description = LocKey.Setting.PrivacyFeedbackFrequency.Description,
                GroupName = LocKey.SettingGroup.DiagnosticsFeedback,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DoNotShowFeedbackNotifications"] = Absent,
                        ["NumberOfSIUFInPeriod"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyActivityHistory.Name,
                Description = LocKey.Setting.PrivacyActivityHistory.Description,
                GroupName = LocKey.SettingGroup.ActivityHistory,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["PublishUserActivities"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyTimelineSuggestions.Name,
                Description = LocKey.Setting.PrivacyTimelineSuggestions.Description,
                GroupName = LocKey.SettingGroup.ActivityHistory,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-353698Enabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySearchHistory.Name,
                Description = LocKey.Setting.PrivacySearchHistory.Description,
                GroupName = LocKey.SettingGroup.SearchPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsDeviceSearchHistoryEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySearchHighlights.Name,
                Description = LocKey.Setting.PrivacySearchHighlights.Description,
                GroupName = LocKey.SettingGroup.SearchPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsDynamicSearchBoxEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySearchMsaCloud.Name,
                Description = LocKey.Setting.PrivacySearchMsaCloud.Description,
                GroupName = LocKey.SettingGroup.SearchPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsMSACloudSearchEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacySearchAadCloud.Name,
                Description = LocKey.Setting.PrivacySearchAadCloud.Description,
                GroupName = LocKey.SettingGroup.SearchPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IsAADCloudSearchEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyAllowCortana.Name,
                Description = LocKey.Setting.PrivacyAllowCortana.Description,
                GroupName = LocKey.SettingGroup.SearchPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowCortana"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyLocationServices.Name,
                Description = LocKey.Setting.PrivacyLocationServices.Description,
                GroupName = LocKey.SettingGroup.AppPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of("Allow").OrAbsent(),
                        ["DisableLocation"] = Of(0),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyCameraAccess.Name,
                Description = LocKey.Setting.PrivacyCameraAccess.Description,
                GroupName = LocKey.SettingGroup.AppPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Allow").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyMicrophoneAccess.Name,
                Description = LocKey.Setting.PrivacyMicrophoneAccess.Description,
                GroupName = LocKey.SettingGroup.AppPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Allow").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyAccountInfoAccess.Name,
                Description = LocKey.Setting.PrivacyAccountInfoAccess.Description,
                GroupName = LocKey.SettingGroup.AppPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Allow").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyAppDiagnosticAccess.Name,
                Description = LocKey.Setting.PrivacyAppDiagnosticAccess.Description,
                GroupName = LocKey.SettingGroup.AppPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = Of("Allow").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyOnedriveAutoBackup.Name,
                Description = LocKey.Setting.PrivacyOnedriveAutoBackup.Description,
                GroupName = LocKey.SettingGroup.AppPermissions,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["KFMBlockOptIn"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyTurnOffCopilot.Name,
                Description = LocKey.Setting.PrivacyTurnOffCopilot.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["TurnOffWindowsCopilot"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableAiDataAnalysis.Name,
                Description = LocKey.Setting.PrivacyDisableAiDataAnalysis.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableAIDataAnalysis"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyBlockRecallEnablement.Name,
                Description = LocKey.Setting.PrivacyBlockRecallEnablement.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowRecallEnablement"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableRecallSnapshots.Name,
                Description = LocKey.Setting.PrivacyDisableRecallSnapshots.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TurnOffSavingSnapshots"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableClickToDo.Name,
                Description = LocKey.Setting.PrivacyDisableClickToDo.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableClickToDo"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableSettingsAgent.Name,
                Description = LocKey.Setting.PrivacyDisableSettingsAgent.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableSettingsAgent"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableAgentConnectors.Name,
                Description = LocKey.Setting.PrivacyDisableAgentConnectors.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableAgentConnectors"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableAgentWorkspaces.Name,
                Description = LocKey.Setting.PrivacyDisableAgentWorkspaces.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableAgentWorkspaces"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableRemoteAgentConnectors.Name,
                Description = LocKey.Setting.PrivacyDisableRemoteAgentConnectors.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableRemoteAgentConnectors"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableCopilotHardwareKey.Name,
                Description = LocKey.Setting.PrivacyDisableCopilotHardwareKey.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SetCopilotHardwareKey"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableCopilotRuntime.Name,
                Description = LocKey.Setting.PrivacyDisableCopilotRuntime.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowCopilotRuntime"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyCopilotUnavailable.Name,
                Description = LocKey.Setting.PrivacyCopilotUnavailable.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["IsCopilotAvailable"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableBingChat.Name,
                Description = LocKey.Setting.PrivacyDisableBingChat.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["IsUserEligible"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDenyGenerativeAiAccess.Name,
                Description = LocKey.Setting.PrivacyDenyGenerativeAiAccess.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Value"] = Of("Allow").OrAbsent(),
                        ["LetAppsAccessGenerativeAI"] = Of(0).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDenySystemAiModels.Name,
                Description = LocKey.Setting.PrivacyDenySystemAiModels.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDenyCopilotMicrophone.Name,
                Description = LocKey.Setting.PrivacyDenyCopilotMicrophone.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Value"] = OneOf("Allow", "Prompt").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisablePaintAiImageCreator.Name,
                Description = LocKey.Setting.PrivacyDisablePaintAiImageCreator.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableImageCreator"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisablePaintAiCocreator.Name,
                Description = LocKey.Setting.PrivacyDisablePaintAiCocreator.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableCocreator"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisablePaintGenerativeFill.Name,
                Description = LocKey.Setting.PrivacyDisablePaintGenerativeFill.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableGenerativeFill"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisablePaintGenerativeErase.Name,
                Description = LocKey.Setting.PrivacyDisablePaintGenerativeErase.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableGenerativeErase"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisablePaintRemoveBackground.Name,
                Description = LocKey.Setting.PrivacyDisablePaintRemoveBackground.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableRemoveBackground"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableInputInsights.Name,
                Description = LocKey.Setting.PrivacyDisableInputInsights.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["InsightsEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableCopilotNudges.Name,
                Description = LocKey.Setting.PrivacyDisableCopilotNudges.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowCopilotNudges"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyDisableConsumerAiContent.Name,
                Description = LocKey.Setting.PrivacyDisableConsumerAiContent.Description,
                GroupName = LocKey.SettingGroup.WindowsAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableConsumerAccountStateContent"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeCopilotCdpPageContext.Name,
                Description = LocKey.Setting.PrivacyEdgeCopilotCdpPageContext.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["CopilotCDPPageContext"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeCopilotPageContext.Name,
                Description = LocKey.Setting.PrivacyEdgeCopilotPageContext.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["CopilotPageContext"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeCopilotSidebar.Name,
                Description = LocKey.Setting.PrivacyEdgeCopilotSidebar.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HubsSidebarEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeEntraCopilot.Name,
                Description = LocKey.Setting.PrivacyEdgeEntraCopilot.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EdgeEntraCopilotPageContext"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeM365CopilotIcon.Name,
                Description = LocKey.Setting.PrivacyEdgeM365CopilotIcon.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Microsoft365CopilotChatIconEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeAiHistorySearch.Name,
                Description = LocKey.Setting.PrivacyEdgeAiHistorySearch.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EdgeHistoryAISearchEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeInlineCompose.Name,
                Description = LocKey.Setting.PrivacyEdgeInlineCompose.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ComposeInlineEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeLocalAiModel.Name,
                Description = LocKey.Setting.PrivacyEdgeLocalAiModel.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["GenAILocalFoundationalModelSettings"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeBuiltinAiApis.Name,
                Description = LocKey.Setting.PrivacyEdgeBuiltinAiApis.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["BuiltInAIAPIsEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeAiThemes.Name,
                Description = LocKey.Setting.PrivacyEdgeAiThemes.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AIGenThemesEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeDevtoolsAi.Name,
                Description = LocKey.Setting.PrivacyEdgeDevtoolsAi.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DevToolsGenAiSettings"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyEdgeShareHistoryCopilot.Name,
                Description = LocKey.Setting.PrivacyEdgeShareHistoryCopilot.Description,
                GroupName = LocKey.SettingGroup.MicrosoftEdgeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShareBrowsingHistoryWithCopilotSearchAllowed"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyOfficeAiTraining.Name,
                Description = LocKey.Setting.PrivacyOfficeAiTraining.Description,
                GroupName = LocKey.SettingGroup.MicrosoftOfficeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["optionalconnectedexperiencesenabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyOfficeConnectedServices.Name,
                Description = LocKey.Setting.PrivacyOfficeConnectedServices.Description,
                GroupName = LocKey.SettingGroup.MicrosoftOfficeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["controllerconnectedservicesenabled"] = Of(0).OrAbsent(),
                        ["usercontentdisabled"] = Of(0).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyWordCopilot.Name,
                Description = LocKey.Setting.PrivacyWordCopilot.Description,
                GroupName = LocKey.SettingGroup.MicrosoftOfficeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableCopilot"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyExcelCopilot.Name,
                Description = LocKey.Setting.PrivacyExcelCopilot.Description,
                GroupName = LocKey.SettingGroup.MicrosoftOfficeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableCopilot"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyOnenoteCopilot.Name,
                Description = LocKey.Setting.PrivacyOnenoteCopilot.Description,
                GroupName = LocKey.SettingGroup.MicrosoftOfficeAI,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.PrivacyOfficeContentSafetyAi.Name,
                Description = LocKey.Setting.PrivacyOfficeContentSafetyAi.Description,
                GroupName = LocKey.SettingGroup.MicrosoftOfficeAI,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["contentsafetyserviceenabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["contentsafetyserviceenabled"] = Of(0) },
                },
            },
        },
    };
}

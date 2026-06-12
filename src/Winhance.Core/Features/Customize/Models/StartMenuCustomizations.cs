using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.Customize.Models;

public static class StartMenuCustomizations
{
    public static SettingGroup GetStartMenuCustomizations()
    {
        return new SettingGroup
        {
            Name = "Start Menu",
            FeatureId = FeatureIds.StartMenu,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = SettingIds.StartMenuCleanWin10,
                    Name = "Clean Start Menu",
                    Description = "Removes all pinned items and applies a clean layout for the current user and any newly created profiles. To clean other existing users, run this again while signed in as each of them.",
                    GroupName = "Layout",
                    InputType = InputType.Action,
                    Icon = "Broom",
                    IsWindows10Only = true,
                    RequiresConfirmation = true,
                    PowerShellScripts = new List<PowerShellScriptSetting>
                    {
                        new PowerShellScriptSetting
                        {
                            EnabledScript = @"
$layoutPath = 'C:\Users\Default\AppData\Local\Microsoft\Windows\Shell\LayoutModification.xml'
$layoutXml = @'
<?xml version=""1.0"" encoding=""utf-8""?>
<LayoutModificationTemplate xmlns:defaultlayout=""http://schemas.microsoft.com/Start/2014/FullDefaultLayout"" xmlns:start=""http://schemas.microsoft.com/Start/2014/StartLayout"" Version=""1"" xmlns:taskbar=""http://schemas.microsoft.com/Start/2014/TaskbarLayout"" xmlns=""http://schemas.microsoft.com/Start/2014/LayoutModification"">
    <LayoutOptions StartTileGroupCellWidth=""6"" />
    <DefaultLayoutOverride>
        <StartLayoutCollection>
            <defaultlayout:StartLayout GroupCellWidth=""6"" />
        </StartLayoutCollection>
    </DefaultLayoutOverride>
</LayoutModificationTemplate>
'@

# Future users: drop the clean template into the Default profile (force-create dir, overwrite if present).
New-Item -ItemType Directory -Path (Split-Path $layoutPath) -Force | Out-Null
[System.IO.File]::WriteAllText($layoutPath, $layoutXml)

# Current user only: apply now via their SID (HKU, not HKCU - correct under OTS), then unlock so they can still customize.
# Other existing users are intentionally not touched - Win10 has no supported way to apply a
# customizable layout to a signed-out profile, so users re-run this per account (see description).
$me = ((Get-CimInstance Win32_ComputerSystem).UserName -split '\\')[-1]
if ($me) {
    $sid = (New-Object System.Security.Principal.NTAccount($me)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $key = ""HKU\$sid\SOFTWARE\Policies\Microsoft\Windows\Explorer""
    reg add $key /v StartLayoutFile /t REG_SZ /d $layoutPath /f | Out-Null
    reg add $key /v LockedStartLayout /t REG_DWORD /d 1 /f | Out-Null
    Stop-Process -Name StartMenuExperienceHost -Force -EA SilentlyContinue; Start-Sleep 3
    reg add $key /v LockedStartLayout /t REG_DWORD /d 0 /f | Out-Null
    Stop-Process -Name StartMenuExperienceHost -Force -EA SilentlyContinue
}
",
                            DisabledScript = null,
                            RequiresElevation = true,
                            RunContext = RunContext.System,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = SettingIds.StartMenuCleanWin11,
                    Name = "Clean Start Menu",
                    Description = "Removes all pinned items and applies clean layout",
                    GroupName = "Layout",
                    InputType = InputType.Action,
                    Icon = "Broom",
                    IsWindows11Only = true,
                    RequiresConfirmation = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            // MDM/CSP path — original target for ConfigureStartPins.
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start",
                            ValueName = "ConfigureStartPins",
                            EnabledValue = [@"{""pinnedList"":[]}"],
                            DisabledValue = [null],
                            RecommendedValue = @"{""pinnedList"":[]}",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            // GPO path — added by KB5062660 (Jul 2025). On Win11 build 26200.8521+
                            // this is the only key that fully clears the default Edge / Settings /
                            // File Explorer pins. Writing both keeps older builds happy and adds
                            // the workaround for newer ones. See issue #660.
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                            ValueName = "ConfigureStartPins",
                            EnabledValue = [@"{""pinnedList"":[]}"],
                            DisabledValue = [null],
                            RecommendedValue = @"{""pinnedList"":[]}",
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                            IsGroupPolicy = true,
                        },
                    },
                    PowerShellScripts = new List<PowerShellScriptSetting>
                    {
                        new PowerShellScriptSetting
                        {
                            EnabledScript = @"
# Clear cached pinned-list data (start.bin / start2.bin) for every real user profile.
# Iterating HKLM\ProfileList is OTS-safe and admin can delete in any profile, so the
# current user and every other user are handled in one loop. ProfileImagePath gives
# the correct path even for non-default profile locations (e.g. D:\Users\...).
$systemAccounts = @('Public','Default','All Users','Default User')
Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList' |
    Where-Object { $_.PSChildName -like 'S-1-5-21-*' } |
    ForEach-Object {
        $profilePath = (Get-ItemProperty $_.PSPath -Name 'ProfileImagePath' -ErrorAction SilentlyContinue).ProfileImagePath
        if ($profilePath -and ((Split-Path $profilePath -Leaf) -notin $systemAccounts)) {
            Remove-Item ""$profilePath\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start*.bin"" -Force -ErrorAction SilentlyContinue
        }
    }

# Restart the Start Menu host so the cleared layout takes effect immediately.
Stop-Process -Name 'StartMenuExperienceHost' -Force -ErrorAction SilentlyContinue
",
                            DisabledScript = null,
                            RequiresElevation = true,
                            RunContext = RunContext.System,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-menu-layout",
                    IsSubjectivePreference = true,
                    Name = "Start layout",
                    Description = "Choose whether the Start Menu shows more pinned apps, more recommendations, or a balanced default layout",
                    GroupName = "Layout",
                    InputType = InputType.Selection,
                    IsWindows11Only = true,
                    IconPack = "Fluent",
                    Icon = "LayoutRowTwoFocusTopSettings",
                    MinimumBuildNumber = 22000, // Windows 11 24H2 starts around build 26100
                    MaximumBuildNumber = 26120, // Removed in build 26120.4250, so max 26120
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_Layout",
                            RecommendedValue = null,
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
                                DisplayName = "Default",
                                ValueMappings = new Dictionary<string, object?> { ["Start_Layout"] = 0 },
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "More pins",
                                ValueMappings = new Dictionary<string, object?> { ["Start_Layout"] = 1 },
                                IsRecommended = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "More recommendations",
                                ValueMappings = new Dictionary<string, object?> { ["Start_Layout"] = 2 },
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-all-apps-view",
                    IsSubjectivePreference = true,
                    Name = "All apps view",
                    Description = "Choose how the All apps section in Start is displayed: by category, in a grid, or as a list",
                    GroupName = "Layout",
                    InputType = InputType.Selection,
                    IsWindows11Only = true,
                    IconPack = "Fluent",
                    Icon = "WindowApps",
                    MinimumBuildNumber = 26100, // Redesigned Start menu shipped in KB5068861 (Nov 11, 2025)
                    MinimumBuildRevision = 7171, // 26100.7171 / 26200.7171
                    AddedInVersion = "26.05.26",
                    RestartProcess = "StartMenuExperienceHost",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                            ValueName = "AllAppsViewMode",
                            RecommendedValue = null,
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
                                DisplayName = "Category",
                                ValueMappings = new Dictionary<string, object?> { ["AllAppsViewMode"] = 0 },
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Grid",
                                ValueMappings = new Dictionary<string, object?> { ["AllAppsViewMode"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "List",
                                ValueMappings = new Dictionary<string, object?> { ["AllAppsViewMode"] = 2 },
                                IsRecommended = true,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-recommended-section",
                    IsSubjectivePreference = true,
                    Name = "Recommended section",
                    Description = "Show or hide the lower section that displays recently opened files and suggested apps. Hiding this section also removes Windows Spotlight from the lock screen and suggested content in the Settings app",
                    GroupName = "Layout",
                    InputType = InputType.Selection,
                    IsWindows11Only = true,
                    Icon = "TableStar",
                    RestartProcess = "StartMenuExperienceHost",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                            ValueName = "HideRecommendedSection",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                            ValueName = "HideRecommendedSection",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start",
                            ValueName = "HideRecommendedSection",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education",
                            ValueName = "IsEducationEnvironment",
                            RecommendedValue = null,
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
                                DisplayName = "Show",
                                ValueMappings = new Dictionary<string, object?>
                                {
                                    ["HideRecommendedSection"] = null, // Delete
                                    ["IsEducationEnvironment"] = null, // Delete
                                },
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Hide",
                                ValueMappings = new Dictionary<string, object?>
                                {
                                    ["HideRecommendedSection"] = 1, // Set to 1
                                    ["IsEducationEnvironment"] = 1, // Set to 1
                                },
                                IsRecommended = true,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-recently-added-apps",
                    Name = "Show recently added apps",
                    Description = "Display a list of recently installed applications at the top of the All Apps list",
                    GroupName = "Start Menu Settings",
                    InputType = InputType.Toggle,
                    Icon = "StarBoxMultipleOutline",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                            ValueName = "ShowRecentList",
                            RecommendedValue = 0,
                            EnabledValue = [1, null], // When toggle is ON, recently added apps are shown
                            DisabledValue = [0], // When toggle is OFF, recently added apps are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-frequent-list",
                    Name = "Show most used apps",
                    Description = "Display your frequently launched applications at the top of the All Apps list for quick access",
                    GroupName = "Start Menu Settings",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "Apps",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                            ValueName = "ShowFrequentList",
                            RecommendedValue = 0,
                            EnabledValue = [1, null], // When toggle is ON, frequently used programs list is shown
                            DisabledValue = [0], // When toggle is OFF, frequently used programs list is hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-track-progs",
                    Name = "Show most used apps",
                    Description = "Display your frequently launched applications at the top of the All Apps list for quick access",
                    GroupName = "Start Menu Settings",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "Apps",
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_TrackProgs",
                            RecommendedValue = 0,
                            EnabledValue = [1, null], // When toggle is ON, frequently used programs list is shown
                            DisabledValue = [0], // When toggle is OFF, frequently used programs list is hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-suggestions",
                    Name = "Show suggestions in Start",
                    Description = "Display app suggestions and promotional content from the Microsoft Store in the Start Menu",
                    GroupName = "Start Menu Settings",
                    InputType = InputType.Toggle,
                    Icon = "LightbulbOnOutline",
                    IsWindows10Only = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "start-show-suggestions",
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
                            ValueName = "SubscribedContent-338388Enabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null], // When toggle is ON, suggestions are shown
                            DisabledValue = [0], // When toggle is OFF, suggestions are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-recommended-files",
                    Name = "Show recommended files and recently opened items",
                    Description = "Display your recently opened documents and files in the Start Menu's Recommended section for quick access",
                    GroupName = "Start Menu Settings",
                    InputType = InputType.Toggle,
                    Icon = "FileStarFourPointsOutline",
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresSpecificValue,
                            DependentSettingId = "start-show-recommended-files",
                            RequiredSettingId = "start-recommended-section",
                            RequiredValue = "Show",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_TrackDocs",
                            RecommendedValue = 0,
                            EnabledValue = [1, null], // When toggle is ON, recommended files are shown
                            DisabledValue = [0], // When toggle is OFF, recommended files are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-menu-recommendations",
                    Name = "Show recommendations for tips, shortcuts, new apps, and more",
                    Description = "Display personalized suggestions from Windows for tips, app shortcuts, and Microsoft Store apps in the Recommended section",
                    GroupName = "Start Menu Settings",
                    InputType = InputType.Toggle,
                    Icon = "CreationOutline",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_IrisRecommendations",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-account-notifications",
                    Name = "Show account-related notifications",
                    Description = "Display notifications about Microsoft account sign-in, sync status, and account-related suggestions",
                    GroupName = "Start Menu Settings",
                    InputType = InputType.Toggle,
                    Icon = "BellRingOutline",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_AccountNotifications",
                            RecommendedValue = 0,
                            EnabledValue = [1, null], // When toggle is ON, account notifications are shown
                            DisabledValue = [0], // When toggle is OFF, account notifications are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-disable-bing-search-results",
                    RecommendedToggleState = false,
                    Name = "Bing Search Results in Start Menu",
                    Description = "Show web results from Bing alongside local files and apps when searching in the Start Menu",
                    GroupName = "Start Menu Settings",
                    InputType = InputType.Toggle,
                    Icon = "MicrosoftBing",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer",
                            ValueName = "DisableSearchBoxSuggestions",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Explorer",
                            ValueName = "DisableSearchBoxSuggestions",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            // Required for Windows 11 25H2+ where the redesigned Start Menu
                            // consults this user-preference key independently of the Group Policy entries above.
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                            ValueName = "BingSearchEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

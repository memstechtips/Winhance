using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Customize.Catalogs;

public static class StartMenuCustomizationsCatalog
{
    public const string FeatureId = FeatureIds.StartMenu;
    public const string FeatureName = "Start Menu";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "start-menu-clean-10",
            Display = new()
            {
                Name = LocKey.Setting.StartMenuClean10.Name,
                Description = LocKey.Setting.StartMenuClean10.Description,
                GroupName = LocKey.SettingGroup.Layout,
                Icon = MaterialIcons.Broom,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Apply = new() { RequiresConfirmation = true },
            Effects = new Effect[]
            {
                new ScriptEffect(@"
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
", RunContext.System),
            },
        },
        new()
        {
            Id = "start-menu-clean-11",
            Display = new()
            {
                Name = LocKey.Setting.StartMenuClean11.Name,
                Description = LocKey.Setting.StartMenuClean11.Description,
                GroupName = LocKey.SettingGroup.Layout,
                Icon = MaterialIcons.Broom,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { RequiresConfirmation = true },
            Effects = new Effect[]
            {
                new RegistryWriteEffect(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start", "ConfigureStartPins", RegistryValueKind.String, @"{""pinnedList"":[]}"),
                new RegistryWriteEffect(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer", "ConfigureStartPins", RegistryValueKind.String, @"{""pinnedList"":[]}") { IsGroupPolicy = true },
                new ScriptEffect(@"
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
", RunContext.System),
            },
        },
        new()
        {
            Id = "start-menu-layout",
            Display = new()
            {
                Name = LocKey.Setting.StartMenuLayout.Name,
                Description = LocKey.Setting.StartMenuLayout.Description,
                GroupName = LocKey.SettingGroup.Layout,
                Icon = FluentIcons.LayoutRowTwoFocusTopSettings,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { new BuildRange(new WinBuild(22000, 0), new WinBuild(26120, int.MaxValue)) } },
            Targets = new Target[]
            {
                new RegTarget("Start_Layout", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "Start_Layout", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.StartMenuLayout.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start_Layout"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.StartMenuLayout.Option1,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start_Layout"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.StartMenuLayout.Option2,
                    Set = new Dictionary<string, StateValue> { ["Start_Layout"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "start-all-apps-view",
            Display = new()
            {
                Name = LocKey.Setting.StartAllAppsView.Name,
                Description = LocKey.Setting.StartAllAppsView.Description,
                GroupName = LocKey.SettingGroup.Layout,
                Icon = FluentIcons.WindowApps,
                AddedInVersion = "26.05.26",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { new BuildRange(new WinBuild(26100, 7171), new WinBuild(int.MaxValue, int.MaxValue)) } },
            Apply = new() { Restart = new RestartProcess("StartMenuExperienceHost") },
            Targets = new Target[]
            {
                new RegTarget("AllAppsViewMode", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start" }, "AllAppsViewMode", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.StartAllAppsView.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllAppsViewMode"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.StartAllAppsView.Option1,
                    Set = new Dictionary<string, StateValue> { ["AllAppsViewMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.StartAllAppsView.Option2,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["AllAppsViewMode"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "start-recommended-section",
            Display = new()
            {
                Name = LocKey.Setting.StartRecommendedSection.Name,
                Description = LocKey.Setting.StartRecommendedSection.Description,
                GroupName = LocKey.SettingGroup.Layout,
                Icon = MaterialIcons.TableStar,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("StartMenuExperienceHost") },
            Targets = new Target[]
            {
                new RegTarget("HideRecommendedSection", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Explorer", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer", @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start" }, "HideRecommendedSection", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("IsEducationEnvironment", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education" }, "IsEducationEnvironment", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.StartRecommendedSection.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HideRecommendedSection"] = Absent, ["IsEducationEnvironment"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.StartRecommendedSection.Option1,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["HideRecommendedSection"] = Of(1), ["IsEducationEnvironment"] = Of(1) },
                    Warning = LocKey.Setting.StartRecommendedSection.OptionWarning1,
                },
            },
        },
        new()
        {
            Id = "start-show-recently-added-apps",
            Display = new()
            {
                Name = LocKey.Setting.StartShowRecentlyAddedApps.Name,
                Description = LocKey.Setting.StartShowRecentlyAddedApps.Description,
                GroupName = LocKey.SettingGroup.StartMenuSettings,
                Icon = MaterialIcons.StarBoxMultipleOutline,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowRecentList", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start" }, "ShowRecentList", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowRecentList"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowRecentList"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "start-show-frequent-list",
            Display = new()
            {
                Name = LocKey.Setting.StartShowFrequentList.Name,
                Description = LocKey.Setting.StartShowFrequentList.Description,
                GroupName = LocKey.SettingGroup.StartMenuSettings,
                Icon = FluentIcons.Apps,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("ShowFrequentList", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start" }, "ShowFrequentList", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["ShowFrequentList"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowFrequentList"] = Of(0).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "start-track-progs",
            Display = new()
            {
                Name = LocKey.Setting.StartTrackProgs.Name,
                Description = LocKey.Setting.StartTrackProgs.Description,
                GroupName = LocKey.SettingGroup.StartMenuSettings,
                Icon = FluentIcons.Apps,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
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
                    Set = new Dictionary<string, StateValue> { ["Start_TrackProgs"] = Of(1).OrAbsent() },
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
            Id = "start-show-suggestions",
            Display = new()
            {
                Name = LocKey.Setting.StartShowSuggestions.Name,
                Description = LocKey.Setting.StartShowSuggestions.Description,
                GroupName = LocKey.SettingGroup.StartMenuSettings,
                Icon = MaterialIcons.LightbulbOnOutline,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Targets = new Target[]
            {
                new RegTarget("SubscribedContent-338388Enabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" }, "SubscribedContent-338388Enabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-338388Enabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SubscribedContent-338388Enabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "start-show-recommended-files",
            Display = new()
            {
                Name = LocKey.Setting.StartShowRecommendedFiles.Name,
                Description = LocKey.Setting.StartShowRecommendedFiles.Description,
                GroupName = LocKey.SettingGroup.StartMenuSettings,
                Icon = MaterialIcons.FileStarFourPointsOutline,
            },
            Targets = new Target[]
            {
                new RegTarget("Start_TrackDocs", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "Start_TrackDocs", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Links = new[] { new Link("start-recommended-section", LinkKind.Requires, LocKey.Setting.StartRecommendedSection.Option0) },
                    Set = new Dictionary<string, StateValue> { ["Start_TrackDocs"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Start_TrackDocs"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "start-menu-recommendations",
            Display = new()
            {
                Name = LocKey.Setting.StartMenuRecommendations.Name,
                Description = LocKey.Setting.StartMenuRecommendations.Description,
                GroupName = LocKey.SettingGroup.StartMenuSettings,
                Icon = MaterialIcons.CreationOutline,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("Start_IrisRecommendations", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "Start_IrisRecommendations", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start_IrisRecommendations"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Start_IrisRecommendations"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "start-show-account-notifications",
            Display = new()
            {
                Name = LocKey.Setting.StartShowAccountNotifications.Name,
                Description = LocKey.Setting.StartShowAccountNotifications.Description,
                GroupName = LocKey.SettingGroup.StartMenuSettings,
                Icon = MaterialIcons.BellRingOutline,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("Start_AccountNotifications", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "Start_AccountNotifications", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start_AccountNotifications"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Start_AccountNotifications"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "start-disable-bing-search-results",
            Display = new()
            {
                Name = LocKey.Setting.StartDisableBingSearchResults.Name,
                Description = LocKey.Setting.StartDisableBingSearchResults.Description,
                GroupName = LocKey.SettingGroup.StartMenuSettings,
                Icon = MaterialIcons.MicrosoftBing,
            },
            Targets = new Target[]
            {
                new RegTarget("DisableSearchBoxSuggestions", new[] { @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Explorer" }, "DisableSearchBoxSuggestions", RegistryValueKind.DWord) { IsGroupPolicy = true },
                new RegTarget("BingSearchEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search" }, "BingSearchEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableSearchBoxSuggestions"] = Absent, ["BingSearchEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableSearchBoxSuggestions"] = Of(1), ["BingSearchEnabled"] = Of(0) },
                },
            },
        },
    };
}

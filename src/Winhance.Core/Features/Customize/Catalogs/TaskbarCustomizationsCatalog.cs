using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Customize.Catalogs;

public static class TaskbarCustomizationsCatalog
{
    public const string FeatureId = FeatureIds.Taskbar;
    public const string FeatureName = "Taskbar";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "taskbar-clean",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarClean.Name,
                Description = LocKey.Setting.TaskbarClean.Description,
                GroupName = LocKey.SettingGroup.Layout,
                Icon = MaterialIcons.Broom,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresConfirmation = true, Restart = new RestartProcess("Explorer") },
            Effects = new Effect[]
            {
                new RegistryWriteEffect(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband", "Favorites", RegistryValueKind.Binary, Array.Empty<byte>()),
            },
        },
        new()
        {
            Id = "taskbar-search-box-11",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarSearchBox11.Name,
                Description = LocKey.Setting.TaskbarSearchBox11.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = MaterialIcons.Magnify,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("SearchboxTaskbarMode", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search" }, "SearchboxTaskbarMode", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarSearchBox11.Option0,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarSearchBox11.Option1,
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarSearchBox11.Option2,
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarSearchBox11.Option3,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "taskbar-search-box-10",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarSearchBox10.Name,
                Description = LocKey.Setting.TaskbarSearchBox10.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = MaterialIcons.Magnify,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("SearchboxTaskbarMode", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search" }, "SearchboxTaskbarMode", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarSearchBox10.Option0,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarSearchBox10.Option1,
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarSearchBox10.Option2,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "taskbar-alignment",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarAlignment.Name,
                Description = LocKey.Setting.TaskbarAlignment.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.FileTableBoxOutline,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("TaskbarAl", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarAl", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarAlignment.Option0,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAl"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarAlignment.Option1,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAl"] = Of(1).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "taskbar-auto-hide",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarAutoHide.Name,
                Description = LocKey.Setting.TaskbarAutoHide.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.ArrowCollapseDown,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("Settings", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3" }, "Settings", RegistryValueKind.Binary) { ByteIndex = 8, ByteOnly = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["Settings"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Settings"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "taskbar-extended-hover-time",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarExtendedHoverTime.Name,
                Description = LocKey.Setting.TaskbarExtendedHoverTime.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.DockBottom,
                AddedInVersion = "26.04.03",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true, Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("ExtendedUIHoverTime", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ExtendedUIHoverTime", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarExtendedHoverTime.Option0,
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarExtendedHoverTime.Option1,
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(10) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarExtendedHoverTime.Option2,
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(50) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarExtendedHoverTime.Option3,
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(100) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarExtendedHoverTime.Option4,
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(200) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarExtendedHoverTime.Option5,
                    Roles = new[] { StateRole.WindowsDefault, StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(400).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "taskbar-badges",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarBadges.Name,
                Description = LocKey.Setting.TaskbarBadges.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.Bell,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("TaskbarBadges", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarBadges", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarBadges"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TaskbarBadges"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-flashing",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarFlashing.Name,
                Description = LocKey.Setting.TaskbarFlashing.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.FlashAlert,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("TaskbarFlashing", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarFlashing", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarFlashing"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TaskbarFlashing"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-multi-display",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarMultiDisplay.Name,
                Description = LocKey.Setting.TaskbarMultiDisplay.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.MonitorMultiple,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("MMTaskbarEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "MMTaskbarEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-multi-display-apps",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarMultiDisplayApps.Name,
                Description = LocKey.Setting.TaskbarMultiDisplayApps.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.Monitor,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            UiParentId = "taskbar-multi-display",
            EnabledWhen = new("taskbar-multi-display", new[] { LocKey.Common.Enabled }),
            Targets = new Target[]
            {
                new RegTarget("MMTaskbarMode", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "MMTaskbarMode", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarMultiDisplayApps.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarMode"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarMultiDisplayApps.Option1,
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarMultiDisplayApps.Option2,
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarMode"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "taskbar-share-window",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarShareWindow.Name,
                Description = LocKey.Setting.TaskbarShareWindow.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.ShareVariant,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("TaskbarSn", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarSn", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarSn"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TaskbarSn"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-show-desktop",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarShowDesktop.Name,
                Description = LocKey.Setting.TaskbarShowDesktop.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.DesktopClassic,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("TaskbarSd", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarSd", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarSd"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TaskbarSd"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-combine-buttons",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarCombineButtons.Name,
                Description = LocKey.Setting.TaskbarCombineButtons.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.Tab,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("TaskbarGlomLevel", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarGlomLevel", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarCombineButtons.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarGlomLevel"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarCombineButtons.Option1,
                    Set = new Dictionary<string, StateValue> { ["TaskbarGlomLevel"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarCombineButtons.Option2,
                    Set = new Dictionary<string, StateValue> { ["TaskbarGlomLevel"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "taskbar-combine-buttons-other",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarCombineButtonsOther.Name,
                Description = LocKey.Setting.TaskbarCombineButtonsOther.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.TabUnselected,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            UiParentId = "taskbar-multi-display",
            EnabledWhen = new("taskbar-multi-display", new[] { LocKey.Common.Enabled }),
            Targets = new Target[]
            {
                new RegTarget("MMTaskbarGlomLevel", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "MMTaskbarGlomLevel", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarCombineButtonsOther.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarGlomLevel"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarCombineButtonsOther.Option1,
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarGlomLevel"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarCombineButtonsOther.Option2,
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarGlomLevel"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "taskbar-button-size",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarButtonSize.Name,
                Description = LocKey.Setting.TaskbarButtonSize.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.Resize,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { new BuildRange(new WinBuild(26100, 4484), new WinBuild(int.MaxValue, int.MaxValue)) } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("IconSizePreference", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "IconSizePreference", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarButtonSize.Option0,
                    Set = new Dictionary<string, StateValue> { ["IconSizePreference"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarButtonSize.Option1,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IconSizePreference"] = Of(2).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarButtonSize.Option2,
                    Set = new Dictionary<string, StateValue> { ["IconSizePreference"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "taskbar-meet-now",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarMeetNow.Name,
                Description = LocKey.Setting.TaskbarMeetNow.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = FluentIcons.Video,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("HideSCAMeetNow", new[]
                {
                    @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                }, "HideSCAMeetNow", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["HideSCAMeetNow"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HideSCAMeetNow"] = Absent },
                },
            },
        },
        new()
        {
            Id = "taskbar-system-tray-icons",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarSystemTrayIcons.Name,
                Description = LocKey.Setting.TaskbarSystemTrayIcons.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.TrayFull,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("EnableAutoTray", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer" }, "EnableAutoTray", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["EnableAutoTray"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableAutoTray"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "taskbar-system-tray-icons-11",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarSystemTrayIcons11.Name,
                Description = LocKey.Setting.TaskbarSystemTrayIcons11.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.TrayFull,
                AddedInVersion = "25.04.08",
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            States = new[]
            {
                new SettingState { Label = LocKey.Setting.TaskbarSystemTrayIcons11.Option0, Roles = new[] { StateRole.Recommended }, Effects = new Effect[] { new ScriptEffect(@"Set-ItemProperty 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify' -Name SystemTrayChevronVisibility -Value 0 -Type DWord -Force; Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' | ForEach-Object { Set-ItemProperty $_.PSPath -Name IsPromoted -Value 1 -Type DWord }", RunContext.User) } },
                new SettingState { Label = LocKey.Setting.TaskbarSystemTrayIcons11.Option1, Effects = new Effect[] { new ScriptEffect(@"Set-ItemProperty 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify' -Name SystemTrayChevronVisibility -Value 1 -Type DWord -Force; Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' | ForEach-Object { Set-ItemProperty $_.PSPath -Name IsPromoted -Value 0 -Type DWord }", RunContext.User) } },
                new SettingState { Label = LocKey.Setting.TaskbarSystemTrayIcons11.Option2, Roles = new[] { StateRole.WindowsDefault } },
            },
            CustomStateScripts = new[] { new ScriptEffect(@"Set-ItemProperty 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify' -Name SystemTrayChevronVisibility -Value 0 -Type DWord -Force; Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' | ForEach-Object { Set-ItemProperty $_.PSPath -Name IsPromoted -Value 1 -Type DWord }", RunContext.User) },
            Detector = new SystemTrayDetector(LocKey.Setting.TaskbarSystemTrayIcons11.Option0, LocKey.Setting.TaskbarSystemTrayIcons11.Option1),
        },
        new()
        {
            Id = "taskbar-task-view",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarTaskView.Name,
                Description = LocKey.Setting.TaskbarTaskView.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = MaterialIcons.DockWindow,
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("ShowTaskViewButton", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowTaskViewButton", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowTaskViewButton"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowTaskViewButton"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-copilot",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarCopilot.Name,
                Description = LocKey.Setting.TaskbarCopilot.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = FluentIcons.BrainCircuit,
            },
            Availability = new() { Builds = new[] { new BuildRange(new WinBuild(22621), new WinBuild(26099, int.MaxValue)) } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("ShowCopilotButton", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowCopilotButton", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowCopilotButton"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowCopilotButton"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-copilot-companion",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarCopilotCompanion.Name,
                Description = LocKey.Setting.TaskbarCopilotCompanion.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = MaterialIcons.Robot,
                AddedInVersion = "26.04.10",
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("TaskbarCompanion", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarCompanion", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarCompanion"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TaskbarCompanion"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-copilot-pwa-pin",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarCopilotPwaPin.Name,
                Description = LocKey.Setting.TaskbarCopilotPwaPin.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = MaterialIcons.Pin,
                AddedInVersion = "26.04.10",
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("CopilotPWAPin", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "CopilotPWAPin", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["CopilotPWAPin"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["CopilotPWAPin"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-recall-pin",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarRecallPin.Name,
                Description = LocKey.Setting.TaskbarRecallPin.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = MaterialIcons.History,
                AddedInVersion = "26.04.10",
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("RecallPin", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "RecallPin", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["RecallPin"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["RecallPin"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-widgets",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarWidgets.Name,
                Description = LocKey.Setting.TaskbarWidgets.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = MaterialIcons.Widgets,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("AllowNewsAndInterests", new[]
                {
                    @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Dsh",
                    @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Dsh",
                }, "AllowNewsAndInterests", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowNewsAndInterests"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowNewsAndInterests"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-news-and-interests",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarNewsAndInterests.Name,
                Description = LocKey.Setting.TaskbarNewsAndInterests.Description,
                GroupName = LocKey.SettingGroup.TaskbarIcons,
                Icon = MaterialIcons.Newspaper,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("EnableFeeds", new[]
                {
                    @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Windows Feeds",
                    @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Windows Feeds",
                }, "EnableFeeds", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableFeeds"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableFeeds"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-transparent",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarTransparent.Name,
                Description = LocKey.Setting.TaskbarTransparent.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.Opacity,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("TaskbarAcrylicOpacity", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarAcrylicOpacity", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarTransparent.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAcrylicOpacity"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarTransparent.Option1,
                    Links = new[] { new Link("theme-transparency", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAcrylicOpacity"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.TaskbarTransparent.Option2,
                    Links = new[] { new Link("theme-transparency", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAcrylicOpacity"] = Of(255) },
                },
            },
        },
        new()
        {
            Id = "taskbar-small",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarSmall.Name,
                Description = LocKey.Setting.TaskbarSmall.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.SizeXxs,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("TaskbarSmallIcons", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarSmallIcons", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["TaskbarSmallIcons"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TaskbarSmallIcons"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-end-task",
            Display = new()
            {
                Name = LocKey.Setting.TaskbarEndTask.Name,
                Description = LocKey.Setting.TaskbarEndTask.Description,
                GroupName = LocKey.SettingGroup.TaskbarBehavior,
                Icon = MaterialIcons.ApplicationCog,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("TaskbarEndTask", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings" }, "TaskbarEndTask", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["TaskbarEndTask"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TaskbarEndTask"] = Of(0) },
                },
            },
        },
    };
}

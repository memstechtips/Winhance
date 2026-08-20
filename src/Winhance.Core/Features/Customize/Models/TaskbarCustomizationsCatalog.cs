using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using static Winhance.Core.Features.Common.Catalog.StateValue;

namespace Winhance.Core.Features.Customize.Models;

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
                Name = "Clean Taskbar",
                Description = "Removes all pinned items from the Taskbar",
                GroupName = "Layout",
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
                Name = "Search in taskbar",
                Description = "Choose how the Windows search appears on your taskbar: hidden, icon only, icon with label, or full search box",
                GroupName = "Taskbar Icons",
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
                    Label = "Hide",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Search icon only",
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Search icon and label",
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(3) },
                },
                new SettingState
                {
                    Label = "Search box",
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
                Name = "Search in taskbar",
                Description = "Choose how the Windows search appears on your taskbar: hidden, icon only, or full search box",
                GroupName = "Taskbar Icons",
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
                    Label = "Hide",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Search icon only",
                    Set = new Dictionary<string, StateValue> { ["SearchboxTaskbarMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Search box",
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
                Name = "Taskbar alignment",
                Description = "Align taskbar icons to the left (classic Windows style) or center (Windows 11 default)",
                GroupName = "Taskbar Behavior",
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
                    Label = "Left",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAl"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Center",
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
                Name = "Automatically hide the taskbar",
                Description = "Automatically hides the taskbar when not in use. Hover at the bottom of the screen to reveal it",
                GroupName = "Taskbar Behavior",
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
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["Settings"] = Of(3) },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Taskbar Auto-Hide Hover Delay",
                Description = "Controls how long you must hover at the screen edge before the auto-hidden taskbar appears (in milliseconds). Lower values make the taskbar appear faster when using auto-hide. Default is 400ms",
                GroupName = "Taskbar Behavior",
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
                    Label = "1ms (Instant)",
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(1) },
                },
                new SettingState
                {
                    Label = "10ms (Very Fast)",
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(10) },
                },
                new SettingState
                {
                    Label = "50ms (Fast)",
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(50) },
                },
                new SettingState
                {
                    Label = "100ms (Moderate)",
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(100) },
                },
                new SettingState
                {
                    Label = "200ms",
                    Set = new Dictionary<string, StateValue> { ["ExtendedUIHoverTime"] = Of(200) },
                },
                new SettingState
                {
                    Label = "400ms (Default)",
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
                Name = "Show badges on taskbar apps",
                Description = "Show notification badge counters on taskbar app icons to indicate unread messages or alerts",
                GroupName = "Taskbar Behavior",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarBadges"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Show flashing on taskbar apps",
                Description = "Allow taskbar app icons to flash when they require your attention",
                GroupName = "Taskbar Behavior",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarFlashing"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Show my taskbar on all displays",
                Description = "Show the taskbar on all connected monitors when using a multi-display setup",
                GroupName = "Taskbar Behavior",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Show taskbar apps on",
                Description = "When using multiple displays, choose which taskbar shows your pinned and running apps",
                GroupName = "Taskbar Behavior",
                Icon = MaterialIcons.Monitor,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            UiParentId = "taskbar-multi-display",
            EnabledWhen = new("taskbar-multi-display", new[] { "Enabled" }),
            Targets = new Target[]
            {
                new RegTarget("MMTaskbarMode", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "MMTaskbarMode", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "All taskbars",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarMode"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Main taskbar and taskbar where window is open",
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Taskbar where window is open",
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarMode"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "taskbar-share-window",
            Display = new()
            {
                Name = "Share any window from my taskbar",
                Description = "Enable sharing any open window directly from the taskbar during a call",
                GroupName = "Taskbar Behavior",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarSn"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Show desktop from taskbar corner",
                Description = "Click the far corner of the taskbar to quickly show the desktop by minimizing all open windows",
                GroupName = "Taskbar Behavior",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarSd"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Combine taskbar buttons and hide labels",
                Description = "Control whether taskbar buttons for the same application are grouped together and whether text labels are shown",
                GroupName = "Taskbar Behavior",
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
                    Label = "Always",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarGlomLevel"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "When taskbar is full",
                    Set = new Dictionary<string, StateValue> { ["TaskbarGlomLevel"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Never",
                    Set = new Dictionary<string, StateValue> { ["TaskbarGlomLevel"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "taskbar-combine-buttons-other",
            Display = new()
            {
                Name = "Combine taskbar buttons on other taskbars",
                Description = "Control whether taskbar buttons are grouped together and labels are hidden on secondary display taskbars",
                GroupName = "Taskbar Behavior",
                Icon = MaterialIcons.TabUnselected,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            UiParentId = "taskbar-multi-display",
            EnabledWhen = new("taskbar-multi-display", new[] { "Enabled" }),
            Targets = new Target[]
            {
                new RegTarget("MMTaskbarGlomLevel", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "MMTaskbarGlomLevel", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Always",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarGlomLevel"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "When taskbar is full",
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarGlomLevel"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Never",
                    Set = new Dictionary<string, StateValue> { ["MMTaskbarGlomLevel"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "taskbar-button-size",
            Display = new()
            {
                Name = "Show smaller taskbar buttons",
                Description = "Control the size of taskbar buttons. This setting may not persist on all Windows 11 builds",
                GroupName = "Taskbar Behavior",
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
                    Label = "Always",
                    Set = new Dictionary<string, StateValue> { ["IconSizePreference"] = Of(0) },
                },
                new SettingState
                {
                    Label = "When taskbar is full",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IconSizePreference"] = Of(2).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Never",
                    Set = new Dictionary<string, StateValue> { ["IconSizePreference"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "taskbar-meet-now",
            Display = new()
            {
                Name = "Remove 'Meet Now' button from system tray",
                Description = "Controls Meet Now button visibility in the system tray",
                GroupName = "Taskbar Icons",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["HideSCAMeetNow"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Always show all system tray icons",
                Description = "Show all system tray icons directly on the taskbar instead of hiding them in the overflow menu. To control individual icon visibility, go to Taskbar Settings and select which icons appear on the taskbar",
                GroupName = "Taskbar Behavior",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["EnableAutoTray"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "System tray icons",
                Description = "Choose how system tray icons appear on the taskbar. \"Show all icons\" promotes every icon to the taskbar; \"Hide all icons\" folds them into the overflow menu; \"Custom\" leaves your per-icon choices from Windows Settings > Personalization > Taskbar > Other system tray icons unchanged.",
                GroupName = "Taskbar Behavior",
                Icon = MaterialIcons.TrayFull,
                AddedInVersion = "25.04.08",
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            States = new[]
            {
                new SettingState { Label = "Show all icons", Roles = new[] { StateRole.Recommended }, Effects = new Effect[] { new ScriptEffect(@"Set-ItemProperty 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify' -Name SystemTrayChevronVisibility -Value 0 -Type DWord -Force; Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' | ForEach-Object { Set-ItemProperty $_.PSPath -Name IsPromoted -Value 1 -Type DWord }", RunContext.User) } },
                new SettingState { Label = "Hide all icons", Effects = new Effect[] { new ScriptEffect(@"Set-ItemProperty 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify' -Name SystemTrayChevronVisibility -Value 1 -Type DWord -Force; Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' | ForEach-Object { Set-ItemProperty $_.PSPath -Name IsPromoted -Value 0 -Type DWord }", RunContext.User) } },
                new SettingState { Label = "Custom", Roles = new[] { StateRole.WindowsDefault } },
            },
            CustomStateScripts = new[] { new ScriptEffect(@"Set-ItemProperty 'HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify' -Name SystemTrayChevronVisibility -Value 0 -Type DWord -Force; Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' | ForEach-Object { Set-ItemProperty $_.PSPath -Name IsPromoted -Value 1 -Type DWord }", RunContext.User) },
            Detector = new SystemTrayDetector("Show all icons", "Hide all icons"),
        },
        new()
        {
            Id = "taskbar-task-view",
            Display = new()
            {
                Name = "Show Task View button",
                Description = "Show the Task View button for managing virtual desktops and viewing all open windows at once",
                GroupName = "Taskbar Icons",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowTaskViewButton"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Copilot Preview Button",
                Description = "Show or hide the Copilot Preview button on the taskbar",
                GroupName = "Taskbar Icons",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowCopilotButton"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Copilot Companion Button",
                Description = "Show or hide the newer Copilot companion button on the taskbar",
                GroupName = "Taskbar Icons",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarCompanion"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Copilot PWA Pin",
                Description = "Show or hide the Copilot PWA pin on the taskbar",
                GroupName = "Taskbar Icons",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["CopilotPWAPin"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Recall Pin",
                Description = "Show or hide the Recall pin on the taskbar",
                GroupName = "Taskbar Icons",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["RecallPin"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Show Widgets",
                Description = "Show the Widgets button that displays personalized news, weather, calendar, and other information",
                GroupName = "Taskbar Icons",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowNewsAndInterests"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Show News and Interests",
                Description = "Show the News and Interests widget that displays headlines, weather, stocks, and other personalized content",
                GroupName = "Taskbar Icons",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableFeeds"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Taskbar Transparency",
                Description = "Controls the transparency level of the taskbar. Winhance automatically enables Transparency Effects when this setting is applied",
                GroupName = "Taskbar Behavior",
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
                    Label = "Windows default",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAcrylicOpacity"] = Absent },
                },
                new SettingState
                {
                    Label = "Transparent",
                    Links = new[] { new Link("theme-transparency", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAcrylicOpacity"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Opaque",
                    Links = new[] { new Link("theme-transparency", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAcrylicOpacity"] = Of(255) },
                },
            },
        },
        new()
        {
            Id = "taskbar-small",
            Display = new()
            {
                Name = "Make taskbar small",
                Description = "Reduce the height of the taskbar by using smaller icons, giving you more screen space",
                GroupName = "Taskbar Behavior",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["TaskbarSmallIcons"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
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
                Name = "Enable 'End Task' in Taskbar",
                Description = "Adds an 'End Task' option when right-clicking applications on the taskbar for quick termination",
                GroupName = "Taskbar Behavior",
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
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["TaskbarEndTask"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TaskbarEndTask"] = Of(0) },
                },
            },
        },
    };
}

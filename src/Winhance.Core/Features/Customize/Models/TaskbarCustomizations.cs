using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.Customize.Models;

public static class TaskbarCustomizations
{
    public static SettingGroup GetTaskbarCustomizations()
    {
        return new SettingGroup
        {
            Name = "Taskbar",
            FeatureId = FeatureIds.Taskbar,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "taskbar-clean",
                    Name = "Clean Taskbar",
                    Description = "Removes all pinned items from the Taskbar",
                    GroupName = "Layout",
                    InputType = InputType.Action,
                    Icon = "Broom",
                    RequiresConfirmation = true,
                    RestartProcess = "Explorer",
                    ActionCommand = "CleanTaskbarAsync",
                },
                new SettingDefinition
                {
                    Id = "taskbar-search-box-11",
                    Name = "Search in taskbar",
                    Description = "Choose how the Windows search appears on your taskbar: hidden, icon only, icon with label, or full search box",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Selection,
                    Icon = "Magnify",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                            ValueName = "SearchboxTaskbarMode",
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
                                DisplayName = "Hide",
                                ValueMappings = new Dictionary<string, object?> { ["SearchboxTaskbarMode"] = 0 },
                                IsRecommended = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Search icon only",
                                ValueMappings = new Dictionary<string, object?> { ["SearchboxTaskbarMode"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Search icon and label",
                                ValueMappings = new Dictionary<string, object?> { ["SearchboxTaskbarMode"] = 3 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Search box",
                                ValueMappings = new Dictionary<string, object?> { ["SearchboxTaskbarMode"] = 2 },
                                IsDefault = true,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-search-box-10",
                    Name = "Search in taskbar",
                    Description = "Choose how the Windows search appears on your taskbar: hidden, icon only, or full search box",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Selection,
                    Icon = "Magnify",
                    IsWindows10Only = true,
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                            ValueName = "SearchboxTaskbarMode",
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
                                DisplayName = "Hide",
                                ValueMappings = new Dictionary<string, object?> { ["SearchboxTaskbarMode"] = 0 },
                                IsRecommended = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Search icon only",
                                ValueMappings = new Dictionary<string, object?> { ["SearchboxTaskbarMode"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Search box",
                                ValueMappings = new Dictionary<string, object?> { ["SearchboxTaskbarMode"] = 2 },
                                IsDefault = true,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-alignment",
                    Name = "Taskbar alignment",
                    Description = "Align taskbar icons to the left (classic Windows style) or center (Windows 11 default)",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Selection,
                    Icon = "FileTableBoxOutline",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarAl",
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
                                DisplayName = "Left",
                                ValueMappings = new Dictionary<string, object?> { ["TaskbarAl"] = 0 },
                                IsRecommended = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Center",
                                ValueMappings = new Dictionary<string, object?> { ["TaskbarAl"] = 1 },
                                IsDefault = true,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-auto-hide",
                    Name = "Automatically hide the taskbar",
                    Description = "Automatically hides the taskbar when not in use. Hover at the bottom of the screen to reveal it",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "ArrowCollapseDown",
                    RestartProcess = "Explorer",
                    AddedInVersion = "26.04.08",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3",
                            ValueName = "Settings",
                            RecommendedValue = 2,
                            EnabledValue = [3],   // 0x03 = auto-hide ON
                            DisabledValue = [2],  // 0x02 = auto-hide OFF
                            DefaultValue = 2,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 8,
                            ModifyByteOnly = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-extended-hover-time",
                    Name = "Taskbar Auto-Hide Hover Delay",
                    Description = "Controls how long you must hover at the screen edge before the auto-hidden taskbar appears (in milliseconds). Lower values make the taskbar appear faster when using auto-hide. Default is 400ms",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Selection,
                    Icon = "DockBottom",
                    RequiresRestart = true,
                    RestartProcess = "Explorer",
                    AddedInVersion = "26.04.03",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ExtendedUIHoverTime",
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
                                DisplayName = "1ms (Instant)",
                                ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "10ms (Very Fast)",
                                ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 10 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "50ms (Fast)",
                                ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 50 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "100ms (Moderate)",
                                ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 100 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "200ms",
                                ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 200 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "400ms (Default)",
                                ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 400 },
                                IsDefault = true,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-badges",
                    Name = "Show badges on taskbar apps",
                    Description = "Show notification badge counters on taskbar app icons to indicate unread messages or alerts",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "Bell",
                    AddedInVersion = "26.04.08",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarBadges",
                            RecommendedValue = 1,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-flashing",
                    Name = "Show flashing on taskbar apps",
                    Description = "Allow taskbar app icons to flash when they require your attention",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "FlashAlert",
                    IsWindows11Only = true,
                    AddedInVersion = "26.04.08",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarFlashing",
                            RecommendedValue = 1,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-multi-display",
                    Name = "Show my taskbar on all displays",
                    Description = "Show the taskbar on all connected monitors when using a multi-display setup",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "MonitorMultiple",
                    RestartProcess = "Explorer",
                    AddedInVersion = "26.04.08",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "MMTaskbarEnabled",
                            RecommendedValue = 1,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-multi-display-apps",
                    Name = "Show taskbar apps on",
                    Description = "When using multiple displays, choose which taskbar shows your pinned and running apps",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Selection,
                    Icon = "Monitor",
                    RestartProcess = "Explorer",
                    AddedInVersion = "26.04.08",
                    ParentSettingId = "taskbar-multi-display",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "MMTaskbarMode",
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
                                DisplayName = "All taskbars",
                                ValueMappings = new Dictionary<string, object?> { ["MMTaskbarMode"] = 0 },
                                IsRecommended = true,
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Main taskbar and taskbar where window is open",
                                ValueMappings = new Dictionary<string, object?> { ["MMTaskbarMode"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Taskbar where window is open",
                                ValueMappings = new Dictionary<string, object?> { ["MMTaskbarMode"] = 2 },
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-share-window",
                    Name = "Share any window from my taskbar",
                    Description = "Enable sharing any open window directly from the taskbar during a call",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "ShareVariant",
                    IsWindows11Only = true,
                    AddedInVersion = "26.04.08",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarSn",
                            RecommendedValue = 1,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-show-desktop",
                    Name = "Show desktop from taskbar corner",
                    Description = "Click the far corner of the taskbar to quickly show the desktop by minimizing all open windows",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "DesktopClassic",
                    IsWindows11Only = true,
                    AddedInVersion = "26.04.08",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarSd",
                            RecommendedValue = 1,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-combine-buttons",
                    Name = "Combine taskbar buttons and hide labels",
                    Description = "Control whether taskbar buttons for the same application are grouped together and whether text labels are shown",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Selection,
                    Icon = "Tab",
                    AddedInVersion = "26.04.08",
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarGlomLevel",
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
                                DisplayName = "Always",
                                ValueMappings = new Dictionary<string, object?> { ["TaskbarGlomLevel"] = 0 },
                                IsRecommended = true,
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "When taskbar is full",
                                ValueMappings = new Dictionary<string, object?> { ["TaskbarGlomLevel"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Never",
                                ValueMappings = new Dictionary<string, object?> { ["TaskbarGlomLevel"] = 2 },
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-combine-buttons-other",
                    Name = "Combine taskbar buttons on other taskbars",
                    Description = "Control whether taskbar buttons are grouped together and labels are hidden on secondary display taskbars",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Selection,
                    Icon = "TabUnselected",
                    AddedInVersion = "26.04.08",
                    RestartProcess = "Explorer",
                    ParentSettingId = "taskbar-multi-display",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "MMTaskbarGlomLevel",
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
                                DisplayName = "Always",
                                ValueMappings = new Dictionary<string, object?> { ["MMTaskbarGlomLevel"] = 0 },
                                IsRecommended = true,
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "When taskbar is full",
                                ValueMappings = new Dictionary<string, object?> { ["MMTaskbarGlomLevel"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Never",
                                ValueMappings = new Dictionary<string, object?> { ["MMTaskbarGlomLevel"] = 2 },
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-button-size",
                    Name = "Show smaller taskbar buttons",
                    Description = "Control the size of taskbar buttons. This setting may not persist on all Windows 11 builds",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Selection,
                    Icon = "Resize",
                    IsWindows11Only = true,
                    MinimumBuildNumber = 26100,
                    MinimumBuildRevision = 4484, // Introduced in 26100.4484 (KB5060829, June 2025)
                    AddedInVersion = "26.04.08",
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "IconSizePreference",
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
                                DisplayName = "Always",
                                ValueMappings = new Dictionary<string, object?> { ["IconSizePreference"] = 0 },
                                IsRecommended = true,
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "When taskbar is full",
                                ValueMappings = new Dictionary<string, object?> { ["IconSizePreference"] = 2 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Never",
                                ValueMappings = new Dictionary<string, object?> { ["IconSizePreference"] = 1 },
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-meet-now",
                    Name = "Remove 'Meet Now' button from system tray",
                    Description = "Controls Meet Now button visibility in the system tray",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "Video",
                    IsWindows10Only = true,
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer",
                            ValueName = "HideSCAMeetNow",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [null],
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = null,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                            ValueName = "HideSCAMeetNow",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [null],
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = null,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                            ValueName = "HideSCAMeetNow",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [null],
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = null,
                            IsGroupPolicy = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-system-tray-icons",
                    Name = "Always show all system tray icons",
                    Description = "Show all system tray icons directly on the taskbar instead of hiding them in the overflow menu. To control individual icon visibility, go to Taskbar Settings and select which icons appear on the taskbar",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "TrayFull",
                    IsWindows10Only = true,
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "EnableAutoTray",
                            RecommendedValue = 0,
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-system-tray-icons-11",
                    Name = "Always show all system tray icons",
                    Description = "Show all system tray icons directly on the taskbar instead of hiding them in the overflow menu. When disabled, all icons will be hidden in the overflow menu. To control individual icon visibility, go to Windows Settings > Personalization > Taskbar > Other system tray icons",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "TrayFull",
                    IsWindows11Only = true,
                    AddedInVersion = "25.04.08",
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify",
                            ValueName = "SystemTrayChevronVisibility",
                            RecommendedValue = 0,
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    PowerShellScripts = new List<PowerShellScriptSetting>
                    {
                        new PowerShellScriptSetting
                        {
                            EnabledScript = @"Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' | ForEach-Object { Set-ItemProperty $_.PSPath -Name IsPromoted -Value 1 -Type DWord }",
                            DisabledScript = @"Get-ChildItem 'HKCU:\Control Panel\NotifyIconSettings' | ForEach-Object { Set-ItemProperty $_.PSPath -Name IsPromoted -Value 0 -Type DWord }",
                            RequiresElevation = false,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-task-view",
                    Name = "Show Task View button",
                    Description = "Show the Task View button for managing virtual desktops and viewing all open windows at once",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Toggle,
                    Icon = "DockWindow",
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowTaskViewButton",
                            RecommendedValue = 0,
                            EnabledValue = [1, null], // When toggle is ON, Task View button is shown
                            DisabledValue = [0], // When toggle is OFF, Task View button is hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-copilot",
                    Name = "Copilot Preview Button",
                    Description = "Show or hide the Copilot Preview button on the taskbar",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "BrainCircuit",
                    IsWindows11Only = true,
                    SupportedBuildRanges = new List<(int, int)>
                    {
                        (22621, 26099)  // Windows 11 22H2/23H2
                    },
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowCopilotButton",
                            RecommendedValue = 0, // Hidden
                            EnabledValue = [1, null],  // Show
                            DisabledValue = [0],    // Hide
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-copilot-companion",
                    Name = "Copilot Companion Button",
                    Description = "Show or hide the newer Copilot companion button on the taskbar",
                    GroupName = "Taskbar Icons",
                    AddedInVersion = "26.04.10",
                    Icon = "Robot",
                    InputType = InputType.Toggle,
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarCompanion",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    RestartProcess = "Explorer",
                },
                new SettingDefinition
                {
                    Id = "taskbar-copilot-pwa-pin",
                    Name = "Copilot PWA Pin",
                    Description = "Show or hide the Copilot PWA pin on the taskbar",
                    GroupName = "Taskbar Icons",
                    AddedInVersion = "26.04.10",
                    Icon = "Pin",
                    InputType = InputType.Toggle,
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "CopilotPWAPin",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    RestartProcess = "Explorer",
                },
                new SettingDefinition
                {
                    Id = "taskbar-recall-pin",
                    Name = "Recall Pin",
                    Description = "Show or hide the Recall pin on the taskbar",
                    GroupName = "Taskbar Icons",
                    AddedInVersion = "26.04.10",
                    Icon = "History",
                    InputType = InputType.Toggle,
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "RecallPin",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    RestartProcess = "Explorer",
                },
                new SettingDefinition
                {
                    Id = "taskbar-widgets",
                    Name = "Show Widgets",
                    Description = "Show the Widgets button that displays personalized news, weather, calendar, and other information",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Toggle,
                    Icon = "Widgets",
                    IsWindows11Only = true,
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Dsh",
                            ValueName = "AllowNewsAndInterests",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Dsh",
                            ValueName = "AllowNewsAndInterests",
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
                    Id = "taskbar-news-and-interests",
                    Name = "Show News and Interests",
                    Description = "Show the News and Interests widget that displays headlines, weather, stocks, and other personalized content",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Toggle,
                    Icon = "Newspaper",
                    IsWindows10Only = true,
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Windows Feeds",
                            ValueName = "EnableFeeds",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            IsGroupPolicy = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Windows Feeds",
                            ValueName = "EnableFeeds",
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
                    Id = "taskbar-transparent",
                    Name = "Taskbar Transparency",
                    Description = "Controls the transparency level of the taskbar. Winhance automatically enables Transparency Effects when this setting is applied",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Selection,
                    Icon = "Opacity",
                    IsWindows10Only = true,
                    RestartProcess = "Explorer",
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "taskbar-transparent",
                            RequiredSettingId = "theme-transparency",
                            RequiredModule = "WindowsThemeCustomizations",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarAcrylicOpacity",
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
                                DisplayName = "Windows default",
                                ValueMappings = new Dictionary<string, object?> { ["TaskbarAcrylicOpacity"] = null },
                                IsRecommended = true,
                                IsDefault = true,
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Transparent",
                                ValueMappings = new Dictionary<string, object?> { ["TaskbarAcrylicOpacity"] = 0 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Opaque",
                                ValueMappings = new Dictionary<string, object?> { ["TaskbarAcrylicOpacity"] = 255 },
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-small",
                    Name = "Make taskbar small",
                    Description = "Reduce the height of the taskbar by using smaller icons, giving you more screen space",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "SizeXxs",
                    IsWindows10Only = true,
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarSmallIcons",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-end-task",
                    Name = "Enable 'End Task' in Taskbar",
                    Description = "Adds an 'End Task' option when right-clicking applications on the taskbar for quick termination",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Toggle,
                    Icon = "ApplicationCog",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                            ValueName = "TaskbarEndTask",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

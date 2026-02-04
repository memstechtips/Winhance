using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

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
                            RecommendedValue = 0, // Hide
                            DefaultValue = 3, // Windows default is search box
                            ValueType = RegistryValueKind.DWord,
                            CustomProperties = new Dictionary<string, object>
                            {
                                ["DefaultOption"] = "Search box",
                            },
                        },
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "Hide",
                            "Search icon only",
                            "Search icon and label",
                            "Search box",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> // Hide
                            {
                                ["SearchboxTaskbarMode"] = 0,
                            },
                            [1] = new Dictionary<string, object?> // Search icon only
                            {
                                ["SearchboxTaskbarMode"] = 1,
                            },
                            [2] = new Dictionary<string, object?> // Search icon and label
                            {
                                ["SearchboxTaskbarMode"] = 2,
                            },
                            [3] = new Dictionary<string, object?> // Search box
                            {
                                ["SearchboxTaskbarMode"] = 3,
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
                            RecommendedValue = 0, // Hide
                            DefaultValue = 2, // Windows default is search box
                            ValueType = RegistryValueKind.DWord,
                            CustomProperties = new Dictionary<string, object>
                            {
                                ["DefaultOption"] = "Search box",
                            },
                        },
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "Hide",
                            "Search icon only",
                            "Search box",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> // Hide
                            {
                                ["SearchboxTaskbarMode"] = 0,
                            },
                            [1] = new Dictionary<string, object?> // Search icon only
                            {
                                ["SearchboxTaskbarMode"] = 1,
                            },
                            [2] = new Dictionary<string, object?> // Search box
                            {
                                ["SearchboxTaskbarMode"] = 2,
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
                            RecommendedValue = 0, // Left alignment
                            DefaultValue = 1, // Center alignment
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Left", "Center" },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> // Left
                            {
                                ["TaskbarAl"] = 0,
                            },
                            [1] = new Dictionary<string, object?> // Center
                            {
                                ["TaskbarAl"] = 1,
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
                            EnabledValue = 1,
                            DisabledValue = null,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                            ValueName = "HideSCAMeetNow",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = null,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                            ValueName = "HideSCAMeetNow",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = null,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = null,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-system-tray-icons",
                    Name = "Always show all system tray icons",
                    Description = "Show all system tray icons directly on the taskbar instead of hiding them in the overflow menu (up arrow)",
                    GroupName = "System Tray",
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
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
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
                            EnabledValue = 1, // When toggle is ON, Task View button is shown
                            DisabledValue = 0, // When toggle is OFF, Task View button is hidden
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
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
                    Icon = "Bot",
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
                            EnabledValue = 1,     // Show
                            DisabledValue = 0,    // Hide
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
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
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Dsh",
                            ValueName = "AllowNewsAndInterests",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
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
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Windows Feeds",
                            ValueName = "EnableFeeds",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-transparent",
                    Name = "Make taskbar transparent",
                    Description = "Controls the transparency of the taskbar",
                    GroupName = "Taskbar",
                    InputType = InputType.Toggle,
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
                            RecommendedValue = 0,
                            EnabledValue = 0, // Transparent
                            DisabledValue = 1, // Opaque
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-small",
                    Name = "Make taskbar small",
                    Description = "Reduce the height of the taskbar by using smaller icons, giving you more screen space",
                    GroupName = "Taskbar",
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
                            EnabledValue = 1,
                            DisabledValue = 0,
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
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

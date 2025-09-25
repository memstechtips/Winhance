using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;

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
                    RequiresConfirmation = true,
                    ConfirmationTitle = "Taskbar Cleaning",
                    ConfirmationMessage =
                        "You are about to clean the Taskbar for the current user.\n\n"
                        + "This will remove all pinned items from the Taskbar.\n\n"
                        + "Do you want to continue?",
                    ConfirmationCheckboxText = "Also apply recommended Taskbar settings",
                    ActionCommand = "CleanTaskbarAsync",
                },
                new SettingDefinition
                {
                    Id = "taskbar-search-box-11",
                    Name = "Search in Taskbar",
                    Description = "Controls search box appearance in taskbar",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Selection,
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
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                        {
                            [0] = new Dictionary<string, int?> // Hide
                            {
                                ["SearchboxTaskbarMode"] = 0,
                            },
                            [1] = new Dictionary<string, int?> // Search icon only
                            {
                                ["SearchboxTaskbarMode"] = 1,
                            },
                            [2] = new Dictionary<string, int?> // Search icon and label
                            {
                                ["SearchboxTaskbarMode"] = 2,
                            },
                            [3] = new Dictionary<string, int?> // Search box
                            {
                                ["SearchboxTaskbarMode"] = 3,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-search-box-10",
                    Name = "Search in Taskbar",
                    Description = "Controls search box appearance in taskbar",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Selection,
                    IsWindows10Only = true,
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
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                        {
                            [0] = new Dictionary<string, int?> // Hide
                            {
                                ["SearchboxTaskbarMode"] = 0,
                            },
                            [1] = new Dictionary<string, int?> // Search icon only
                            {
                                ["SearchboxTaskbarMode"] = 1,
                            },
                            [2] = new Dictionary<string, int?> // Search box
                            {
                                ["SearchboxTaskbarMode"] = 2,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-alignment",
                    Name = "Taskbar Alignment",
                    Description = "Controls taskbar icons alignment",
                    GroupName = "Taskbar Behavior",
                    InputType = InputType.Selection,
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
                            CustomProperties = new Dictionary<string, object>
                            {
                                ["DefaultOption"] = "Center",
                            },
                        },
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Left", "Center" },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int?>>
                        {
                            [0] = new Dictionary<string, int?> // Left
                            {
                                ["TaskbarAl"] = 0,
                            },
                            [1] = new Dictionary<string, int?> // Center
                            {
                                ["TaskbarAl"] = 1,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-meet-now",
                    Name = "Remove Meet Now Button from System Tray",
                    Description = "Controls Meet Now button visibility in the system tray",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Toggle,
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                            ValueName = "HideSCAMeetNow",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, Meet Now button is hidden
                            DisabledValue = null, // When toggle is OFF, Meet Now button is shown
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = null,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-system-tray-icons",
                    Name = "Always Show All System Tray Icons",
                    Description =
                        "Controls whether system tray icons are shown in the taskbar or hidden in the chevron menu",
                    GroupName = "System Tray",
                    InputType = InputType.Toggle,
                    IsWindows10Only = true,
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
                    Name = "Show Task View Button",
                    Description = "Controls Task View button visibility in taskbar",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Toggle,
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
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-widgets",
                    Name = "Show Widgets",
                    Description = "Controls Widgets visibility in taskbar",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Toggle,
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Dsh",
                            ValueName = "AllowNewsAndInterests",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, Widgets button is shown
                            DisabledValue = 0, // When toggle is OFF, Widgets button is hidden
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-news-and-interests",
                    Name = "Show News and Interests",
                    Description = "Controls News and Interests visibility in taskbar",
                    GroupName = "Taskbar Icons",
                    InputType = InputType.Toggle,
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Windows Feeds",
                            ValueName = "EnableFeeds",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, News and Interests is shown
                            DisabledValue = null, // When toggle is OFF, News and Interests is hidden
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-transparent",
                    Name = "Make Taskbar Transparent",
                    Description = "Controls the transparency of the taskbar",
                    GroupName = "Taskbar",
                    InputType = InputType.Toggle,
                    IsWindows10Only = true,
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
                    Name = "Make Taskbar Small",
                    Description = "Controls the size of taskbar icons",
                    GroupName = "Taskbar",
                    InputType = InputType.Toggle,
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarSmallIcons",
                            RecommendedValue = 1,
                            EnabledValue = 1, // Small icons
                            DisabledValue = 0, // Normal icons
                            DefaultValue = 0, // Default is normal icons
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

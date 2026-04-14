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
                    Id = "start-menu-clean-10",
                    Name = "Clean Start Menu",
                    Description = "Removes all pinned items and applies clean layout",
                    GroupName = "Layout",
                    InputType = InputType.Action,
                    Icon = "Broom",
                    IsWindows10Only = true,
                    RequiresConfirmation = true,
                    ActionCommand = "CleanWindows10StartMenuAsync",
                },
                new SettingDefinition
                {
                    Id = "start-menu-clean-11",
                    Name = "Clean Start Menu",
                    Description = "Removes all pinned items and applies clean layout",
                    GroupName = "Layout",
                    InputType = InputType.Action,
                    Icon = "Broom",
                    IsWindows11Only = true,
                    RequiresConfirmation = true,
                    ActionCommand = "CleanWindows11StartMenuAsync",
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
                            RecommendedValue = null,
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
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [1],
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

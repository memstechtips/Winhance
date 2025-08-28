using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Models.WindowsRegistry;
using Winhance.Core.Features.Customize.Interfaces;

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
                    InputType = SettingInputType.Action,
                    IsWindows10Only = true,
                    RequiresConfirmation = true,
                    ConfirmationTitle = "Start Menu Cleaning",
                    ConfirmationMessage =
                        "You are about to clean the Start Menu for all users on this computer.\n\n"
                        + "This will remove all pinned items and apply recommended settings to disable suggestions, "
                        + "recommendations, and tracking features.\n\n"
                        + "Do you want to continue?",
                    ConfirmationCheckboxText = "Also apply recommended Start Menu settings",
                    ActionCommand = "CleanWindows10StartMenuAsync",
                },
                new SettingDefinition
                {
                    Id = "start-menu-clean-11",
                    Name = "Clean Start Menu",
                    Description = "Removes all pinned items and applies clean layout",
                    GroupName = "Layout",
                    InputType = SettingInputType.Action,
                    IsWindows11Only = true,
                    RequiresConfirmation = true,
                    ConfirmationTitle = "Start Menu Cleaning",
                    ConfirmationMessage =
                        "You are about to clean the Start Menu for all users on this computer.\n\n"
                        + "This will remove all pinned items and apply recommended settings to disable suggestions, "
                        + "recommendations, and tracking features.\n\n"
                        + "Do you want to continue?",
                    ConfirmationCheckboxText = "Also apply recommended Start Menu settings",
                    ActionCommand = "CleanWindows11StartMenuAsync",
                },
                new SettingDefinition
                {
                    Id = "start-menu-layout",
                    Name = "Start Layout",
                    Description = "Controls the layout of the Start Menu",
                    GroupName = "Layout",
                    InputType = SettingInputType.Selection,
                    IsWindows11Only = true,
                    Icon = "\uF78C", // Start menu icon
                    MinimumBuildNumber = 22000, // Windows 11 24H2 starts around build 26100
                    MaximumBuildNumber = 26120, // Removed in build 26120.4250, so max 26120
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_Layout",
                            RecommendedValue = 1, // More Pins
                            DefaultValue = 0, // Windows default is default layout
                            ValueType = RegistryValueKind.DWord,
                            CustomProperties = new Dictionary<string, object>
                            {
                                ["DefaultOption"] = "Default",
                                ["RecommendedOption"] = "More pins",
                            },
                        },
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "Default",
                            "More pins",
                            "More recommendations",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int>>
                        {
                            [0] = new Dictionary<string, int> // Default
                            {
                                ["Start_Layout"] = 0,
                            },
                            [1] = new Dictionary<string, int> // More pins
                            {
                                ["Start_Layout"] = 1,
                            },
                            [2] = new Dictionary<string, int> // More recommendations
                            {
                                ["Start_Layout"] = 2,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-recommended-section",
                    Name = "Recommended Section",
                    Description =
                        "Controls visibility of the recommended section in the Start Menu",
                    GroupName = "Layout",
                    InputType = SettingInputType.Selection,
                    IsWindows11Only = true,
                    Icon = "\uF054", // Reviews icon
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                            ValueName = "HideRecommendedSection",
                            RecommendedValue = 1, // Hide recommended section
                            EnabledValue = 1, // When "Hide" is selected, set value to 1
                            DisabledValue = null, // When "Show" is selected, delete the value
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            // ComboBox display names and value mappings handled at setting level
                            CustomProperties = new Dictionary<string, object>
                            {
                                ["DefaultOption"] = "Show",
                                ["RecommendedOption"] = "Hide",
                            },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start",
                            ValueName = "HideRecommendedSection",
                            RecommendedValue = 1, // Hide recommended section
                            EnabledValue = 1, // When "Hide" is selected, set value to 1
                            DisabledValue = null, // When "Show" is selected, delete the value
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            CustomProperties = new Dictionary<string, object>
                            {
                                ["DefaultOption"] = "Show",
                                ["RecommendedOption"] = "Hide",
                            },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education",
                            ValueName = "IsEducationEnvironment",
                            RecommendedValue = 1, // Enable education environment
                            EnabledValue = 1, // When "Hide" is selected, set value to 1
                            DisabledValue = null, // When "Show" is selected, delete the value (Group Policy)
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            CustomProperties = new Dictionary<string, object>
                            {
                                ["DefaultOption"] = "Show",
                                ["RecommendedOption"] = "Hide",
                            },
                        },
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[] { "Show", "Hide" },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, int>>
                        {
                            [0] = new Dictionary<string, int> // Show (delete registry values)
                            {
                                ["HideRecommendedSection"] = 0, // Delete or set to 0 (Group Policy)
                                ["IsEducationEnvironment"] = 0, // Delete or set to 0 (Group Policy)
                            },
                            [1] = new Dictionary<string, int> // Hide (set registry values)
                            {
                                ["HideRecommendedSection"] = 1, // Set to 1
                                ["IsEducationEnvironment"] = 1, // Set to 1
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-all-pins-by-default",
                    Name = "Show all pins by default",
                    Description = "Controls whether all pins are shown by default in Start Menu",
                    GroupName = "Start Menu Settings",
                    InputType = SettingInputType.Toggle,
                    IsWindows11Only = true,
                    SupportedBuildRanges = new List<(int, int)>
                    {
                        (26120, int.MaxValue), // Windows 11 24H2 build 26120.4250 and later
                        (26200, int.MaxValue), // Windows 11 25H2 build 26200.5670 and later
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                            ValueName = "ShowAllPinsList",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, all pins are shown
                            DisabledValue = 0, // When toggle is OFF, all pins are not shown
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-recently-added-apps",
                    Name = "Show Recently Added Apps",
                    Description = "Controls visibility of recently added apps in Start Menu",
                    GroupName = "Start Menu Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                            ValueName = "ShowRecentList",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, recently added apps are shown
                            DisabledValue = 0, // When toggle is OFF, recently added apps are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Windows 11 - Show Most Used Apps (uses ShowFrequentList)
                new SettingDefinition
                {
                    Id = "start-show-frequent-list",
                    Name = "Show Most Used Apps",
                    Description = "Controls visibility of most used apps in Start Menu",
                    GroupName = "Start Menu",
                    InputType = SettingInputType.Toggle,
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                            ValueName = "ShowFrequentList",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, frequently used programs list is shown
                            DisabledValue = 0, // When toggle is OFF, frequently used programs list is hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Windows 10 - Show Most Used Apps (uses Start_TrackProgs)
                new SettingDefinition
                {
                    Id = "start-track-progs",
                    Name = "Show Most Used Apps",
                    Description = "Controls visibility of most used apps in Start Menu",
                    GroupName = "Start Menu",
                    InputType = SettingInputType.Toggle,
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_TrackProgs",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, frequently used programs list is shown
                            DisabledValue = 0, // When toggle is OFF, frequently used programs list is hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-suggestions",
                    Name = "Show suggestions in Start",
                    Description = "Controls visibility of suggestions in Start Menu",
                    GroupName = "Start Menu Settings",
                    InputType = SettingInputType.Toggle,
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338388Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, suggestions are shown
                            DisabledValue = 0, // When toggle is OFF, suggestions are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-recommended-files",
                    Name = "Show Recommended Files and Recently Opened Items",
                    Description =
                        "Controls visibility of recommended files and recently opened items in Start Menu",
                    GroupName = "Start Menu Settings",
                    InputType = SettingInputType.Toggle,
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
                            EnabledValue = 1, // When toggle is ON, recommended files are shown
                            DisabledValue = 0, // When toggle is OFF, recommended files are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-menu-recommendations",
                    Name = "Show recommendations for tips, shortcuts, new apps, and more",
                    Description =
                        "Controls visibility of recommendations for tips, shortcuts and new apps in the Start Menu",
                    GroupName = "Start Menu Settings",
                    InputType = SettingInputType.Toggle,
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_IrisRecommendations",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-show-account-notifications",
                    Name = "Show Account-related Notifications",
                    Description =
                        "Controls visibility of account-related notifications in Start Menu",
                    GroupName = "Start Menu Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_AccountNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, account notifications are shown
                            DisabledValue = 0, // When toggle is OFF, account notifications are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-power-lock-option",
                    Name = "Hide Lock Option",
                    Description =
                        "Controls visibility of the Lock option in the Start Menu power flyout",
                    GroupName = "Start Menu",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\System",
                            ValueName = "DisableLockWorkstation",
                            RecommendedValue = null,
                            EnabledValue = 1, // Hide lock option
                            DisabledValue = null, // Show lock option
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "start-disable-bing-search-results",
                    Name = "Disable Bing Search Results",
                    Description =
                        "Controls whether results from Bing online search are displayed when using Start Menu search",
                    GroupName = "Start Menu Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Explorer",
                            ValueName = "DisableSearchBoxSuggestions",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, Bing search results are not displayed when using Start Menu search
                            DisabledValue = null, // When toggle is OFF, Bing search results are displayed when using Start Menu search
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

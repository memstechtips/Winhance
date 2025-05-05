using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class ExplorerOptimizations
{
    public static OptimizationGroup GetExplorerOptimizations()
    {
        return new OptimizationGroup
        {
            Name = "Explorer",
            Category = OptimizationCategory.Explorer,
            Settings = new List<OptimizationSetting>
            {
                new OptimizationSetting
                {
                    Id = "explorer-long-paths-enabled",
                    Name = "Long Paths Enabled",
                    Description = "Controls support for long file paths (up to 32,767 characters)",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SYSTEM\\CurrentControlSet\\Control\\FileSystem",
                            Name = "LongPathsEnabled",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description =
                                "Controls support for long file paths (up to 32,767 characters)",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-block-aad-workplace-join",
                    Name = "Block AAD Workplace Join",
                    Description = "Controls 'Allow my organization to manage my device' pop-up",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System",
                            Name = "BlockAADWorkplaceJoin",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description =
                                "Controls 'Allow my organization to manage my device' pop-up",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-disable-sync-provider-notifications",
                    Name = "Sync Provider Notifications",
                    Description = "Controls sync provider notifications visibility",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "ShowSyncProviderNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls sync provider notifications visibility",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-tablet-mode",
                    Name = "Tablet Mode",
                    Description = "Controls Tablet Mode",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "System Interface",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\ImmersiveShell",
                            Name = "TabletMode",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, tablet mode is enabled
                            DisabledValue = 0, // When toggle is OFF, tablet mode is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description = "Controls Tablet Mode",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-desktop-mode-signin",
                    Name = "Desktop Mode on Sign-in",
                    Description = "Controls whether the system goes to desktop mode on sign-in",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "System Interface",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\ImmersiveShell",
                            Name = "SignInMode",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, system goes to desktop mode on sign-in
                            DisabledValue = 0, // When toggle is OFF, system uses default behavior
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description =
                                "Controls whether the system goes to desktop mode on sign-in",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-voice-typing",
                    Name = "Voice Typing Button",
                    Description = "Controls voice typing microphone button",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "Input Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\InputSettings",
                            Name = "IsVoiceTypingKeyEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, voice typing is enabled
                            DisabledValue = 0, // When toggle is OFF, voice typing is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls voice typing microphone button",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-typing-insights",
                    Name = "Typing Insights",
                    Description = "Controls typing insights and suggestions",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "Input Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\InputSettings",
                            Name = "InsightsEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, typing insights are enabled
                            DisabledValue = 0, // When toggle is OFF, typing insights are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls typing insights and suggestions",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-suggested-actions",
                    Name = "Clipboard Suggested Actions",
                    Description = "Controls suggested actions for clipboard content",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "System Interface",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SmartActionPlatform\\SmartClipboard",
                            Name = "Disabled",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, suggested actions are enabled
                            DisabledValue = 1, // When toggle is OFF, suggested actions are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description = "Controls suggested actions for clipboard content",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-windows-manage-printer",
                    Name = "Default Printer Management",
                    Description = "Controls Windows managing default printer",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "Printer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Windows",
                            Name = "LegacyDefaultPrinterMode",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, Windows manages default printer
                            DisabledValue = 1, // When toggle is OFF, Windows does not manage default printer
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description = "Controls Windows managing default printer",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-disable-snap-assist",
                    Name = "Snap Assist",
                    Description = "Controls Snap Assist feature",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "SnapAssist",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls Snap Assist feature",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-frequent-folders",
                    Name = "Frequent Folders in Quick Access",
                    Description = "Controls display of frequent folders in Quick Access",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer",
                            Name = "ShowFrequent",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, frequent folders are shown
                            DisabledValue = 0, // When toggle is OFF, frequent folders are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls display of frequent folders in Quick Access",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-office-files",
                    Name = "Office Files in Quick Access",
                    Description = "Controls display of files from Office.com in Quick Access",
                    Category = OptimizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer",
                            Name = "ShowCloudFilesInQuickAccess",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, Office.com files are shown
                            DisabledValue = 0, // When toggle is OFF, Office.com files are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description =
                                "Controls display of files from Office.com in Quick Access",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
            },
        };
    }
}

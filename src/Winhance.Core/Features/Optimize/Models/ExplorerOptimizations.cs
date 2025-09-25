using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class ExplorerOptimizations
{
    public static SettingGroup GetExplorerOptimizations()
    {
        return new SettingGroup
        {
            Name = "ExplorerOptimizations",
            FeatureId = FeatureIds.ExplorerOptimization,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "explorer-optimization-long-paths-enabled",
                    Name = "Long Paths Enabled",
                    Description = "Controls support for long file paths (up to 32,767 characters)",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem",
                            ValueName = "LongPathsEnabled",
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
                    Id = "explorer-optimization-block-aad-workplace-join",
                    Name = "Block AAD Workplace Join",
                    Description = "Controls 'Allow my organization to manage my device' pop-up",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\System",
                            ValueName = "BlockAADWorkplaceJoin",
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
                    Id = "explorer-optimization-disable-sync-provider-notifications",
                    Name = "Sync Provider Notifications",
                    Description = "Controls sync provider notifications visibility",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowSyncProviderNotifications",
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
                    Id = "explorer-optimization-tablet-mode",
                    Name = "Tablet Mode",
                    Description = "Controls Tablet Mode",
                    GroupName = "System Interface",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ImmersiveShell",
                            ValueName = "TabletMode",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, tablet mode is enabled
                            DisabledValue = 0, // When toggle is OFF, tablet mode is disabled
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-optimization-desktop-mode-signin",
                    Name = "Desktop Mode on Sign-in",
                    Description = "Controls whether the system goes to desktop mode on sign-in",
                    GroupName = "System Interface",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ImmersiveShell",
                            ValueName = "SignInMode",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, system goes to desktop mode on sign-in
                            DisabledValue = 0, // When toggle is OFF, system uses default behavior
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-optimization-voice-typing",
                    Name = "Voice Typing Button",
                    Description = "Controls voice typing microphone button",
                    GroupName = "Input Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\InputSettings",
                            ValueName = "IsVoiceTypingKeyEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, voice typing is enabled
                            DisabledValue = 0, // When toggle is OFF, voice typing is disabled
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-optimization-typing-insights",
                    Name = "Typing Insights",
                    Description = "Controls typing insights and suggestions",
                    GroupName = "Input Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\InputSettings",
                            ValueName = "InsightsEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, typing insights are enabled
                            DisabledValue = 0, // When toggle is OFF, typing insights are disabled
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-optimization-suggested-actions",
                    Name = "Clipboard Suggested Actions",
                    Description = "Controls suggested actions for clipboard content",
                    GroupName = "System Interface",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\SmartActionPlatform\SmartClipboard",
                            ValueName = "Disabled",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, suggested actions are enabled
                            DisabledValue = 1, // When toggle is OFF, suggested actions are disabled
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-optimization-windows-manage-printer",
                    Name = "Default Printer Management",
                    Description = "Controls Windows managing default printer",
                    GroupName = "Printer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Windows",
                            ValueName = "LegacyDefaultPrinterMode",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, Windows manages default printer
                            DisabledValue = 1, // When toggle is OFF, Windows does not manage default printer
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-optimization-disable-snap-assist",
                    Name = "Snap Assist",
                    Description = "Controls Snap Assist feature",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "SnapAssist",
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
                    Id = "explorer-optimization-frequent-folders",
                    Name = "Frequent Folders in Quick Access",
                    Description = "Controls display of frequent folders in Quick Access",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowFrequent",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, frequent folders are shown
                            DisabledValue = 0, // When toggle is OFF, frequent folders are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-optimization-compress-desktop-wallpaper",
                    Name = "Compress Desktop Wallpaper",
                    Description = "Controls compression of desktop wallpaper",
                    GroupName = "Desktop Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "JPEGImportQuality",
                            RecommendedValue = 100,
                            EnabledValue = 0,
                            DisabledValue = 100,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-optimization-office-files",
                    Name = "Office Files in Quick Access",
                    Description = "Controls display of files from Office.com in Quick Access",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowCloudFilesInQuickAccess",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, Office.com files are shown
                            DisabledValue = 0, // When toggle is OFF, Office.com files are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}

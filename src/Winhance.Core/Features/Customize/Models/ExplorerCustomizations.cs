using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Models.WindowsRegistry;

namespace Winhance.Core.Features.Customize.Models;

public static class ExplorerCustomizations
{
    public static SettingGroup GetExplorerCustomizations()
    {
        return new SettingGroup
        {
            Name = "ExplorerCustomizations",
            FeatureId = FeatureIds.ExplorerCustomization,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "explorer-customization-3d-objects",
                    Name = "3D Objects in This PC",
                    Description = "Controls 3D Objects folder visibility in This PC",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}",
                            ValueName = null,
                            RecommendedValue = null,
                            EnabledValue = null, // When toggle is ON, 3D Objects folder is shown (key exists)
                            DisabledValue = null, // When toggle is OFF, 3D Objects folder is hidden (key removed)
                            DefaultValue = null,
                            ValueType = RegistryValueKind.None,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-home-folder",
                    Name = "Home Folder in Navigation Pane",
                    Description = "Controls Home Folder visibility in Navigation Pane",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}",
                            ValueName = null,
                            RecommendedValue = null,
                            EnabledValue = null, // When toggle is ON, Home Folder is shown (key exists)
                            DisabledValue = null, // When toggle is OFF, Home Folder is hidden (key removed)
                            DefaultValue = null,
                            ValueType = RegistryValueKind.None,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-launch-to",
                    Name = "Launch Explorer to This PC",
                    Description = "Makes File Explorer open This PC instead of Quick Access",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "LaunchTo",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, File Explorer opens to 'This PC'
                            DisabledValue = 2, // When toggle is OFF, File Explorer opens to 'Quick access'
                            DefaultValue = 2,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-file-ext",
                    Name = "Show File Extensions",
                    Description = "Controls visibility of file name extensions",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "HideFileExt",
                            RecommendedValue = 0,
                            EnabledValue = 0, // When toggle is ON, file extensions are shown
                            DisabledValue = 1, // When toggle is OFF, file extensions are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-hidden-files",
                    Name = "Show Hidden Files, Folders & Drives",
                    Description = "Controls visibility of hidden files and folders",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Hidden",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, hidden files are shown
                            DisabledValue = 0, // When toggle is OFF, hidden files are hidden
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-hide-protected-files",
                    Name = "Hide Protected Operating System Files",
                    Description = "Controls visibility of protected operating system files",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowSuperHidden",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, protected operating system files are hidden
                            DisabledValue = 1, // When toggle is OFF, protected operating system files are shown
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-folder-tips",
                    Name = "Folder Tips",
                    Description = "Controls file size information in folder tips",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "FolderContentsInfoTip",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, folder tips are enabled
                            DisabledValue = 0, // When toggle is OFF, folder tips are disabled
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-popup-descriptions",
                    Name = "Pop-up Descriptions",
                    Description = "Controls pop-up descriptions for folder and desktop items",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowInfoTip",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, pop-up descriptions are shown
                            DisabledValue = 0, // When toggle is OFF, pop-up descriptions are hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-preview-handlers",
                    Name = "Preview Handlers",
                    Description = "Controls preview handlers in preview pane",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowPreviewHandlers",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, preview handlers are enabled
                            DisabledValue = 0, // When toggle is OFF, preview handlers are disabled
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-status-bar",
                    Name = "Status Bar",
                    Description = "Controls status bar visibility in File Explorer",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowStatusBar",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, status bar is shown
                            DisabledValue = 0, // When toggle is OFF, status bar is hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-thumbnails",
                    Name = "Show Thumbnails",
                    Description = "Controls whether to show thumbnails or icons",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "IconsOnly",
                            RecommendedValue = 0,
                            EnabledValue = 0, // When toggle is ON, thumbnails are shown
                            DisabledValue = 1, // When toggle is OFF, icons are shown
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-translucent-selection",
                    Name = "Translucent Selection",
                    Description = "Controls translucent selection rectangle",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ListviewAlphaSelect",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, translucent selection is enabled
                            DisabledValue = 0, // When toggle is OFF, translucent selection is disabled
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-drop-shadows",
                    Name = "Drop Shadows",
                    Description = "Controls drop shadows for icon labels",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ListviewShadow",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, drop shadows are enabled
                            DisabledValue = 0, // When toggle is OFF, drop shadows are disabled
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-full-path",
                    Name = "Full Path in Title Bar",
                    Description = "Controls full path display in the title bar",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState",
                            ValueName = "FullPath",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, full path is shown
                            DisabledValue = 0, // When toggle is OFF, full path is hidden
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-font-smoothing",
                    Name = "Font Smoothing",
                    Description = "Controls smooth edges of screen fonts",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "FontSmoothing",
                            RecommendedValue = "2",
                            EnabledValue = "2", // When toggle is ON, font smoothing is enabled
                            DisabledValue = "0", // When toggle is OFF, font smoothing is disabled
                            DefaultValue = "0",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-dpi-scaling",
                    Name = "DPI Scaling (100%)",
                    Description = "Controls DPI scaling setting",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "LogPixels",
                            RecommendedValue = 96,
                            EnabledValue = 96, // When toggle is ON, DPI scaling is set to 100%
                            DisabledValue = 120, // When toggle is OFF, DPI scaling is set to 125%
                            DefaultValue = 120,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-per-process-dpi",
                    Name = "Per-Process DPI",
                    Description = "Controls per-process system DPI",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "EnablePerProcessSystemDPI",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, per-process DPI is enabled
                            DisabledValue = 0, // When toggle is OFF, per-process DPI is disabled
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-lock-screen",
                    Name = "Lock Screen",
                    Description = "Controls lock screen visibility",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Personalization",
                            ValueName = "NoLockScreen",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-gallery",
                    Name = "Gallery in Navigation Pane",
                    Description = "Controls gallery visibility in navigation pane",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}",
                            ValueName = null,
                            RecommendedValue = null,
                            EnabledValue = null, // When toggle is ON, gallery is shown (key exists)
                            DisabledValue = null, // When toggle is OFF, gallery is hidden (key removed)
                            DefaultValue = null,
                            ValueType = RegistryValueKind.None,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-context-menu",
                    Name = "Classic Context Menu",
                    Description = "Controls context menu style (classic or modern)",
                    GroupName = "File Explorer Settings",
                    InputType = SettingInputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                            ValueName = "",
                            RecommendedValue = "",
                            EnabledValue = "", // When toggle is ON, classic context menu is used (value is deleted)
                            DisabledValue = "", // When toggle is OFF, modern context menu is used (empty value is set)
                            DefaultValue = "",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
            },
        };
    }
}

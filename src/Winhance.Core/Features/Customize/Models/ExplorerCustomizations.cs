using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Enums;

namespace Winhance.Core.Features.Customize.Models;

public static class ExplorerCustomizations
{
    public static CustomizationGroup GetExplorerCustomizations()
    {
        return new CustomizationGroup
        {
            Name = "Explorer",
            Category = CustomizationCategory.Explorer,
            Settings = new List<CustomizationSetting>
            {
                new CustomizationSetting
                {
                    Id = "explorer-customization-3d-objects",
                    Name = "3D Objects in This PC",
                    Description = "Controls 3D Objects folder visibility in This PC",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace",
                            Name = "{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}",
                            RecommendedValue = null,
                            EnabledValue = null, // When toggle is ON, 3D Objects folder is shown (key exists)
                            DisabledValue = null, // When toggle is OFF, 3D Objects folder is hidden (key removed)
                            ValueType = RegistryValueKind.None,
                            DefaultValue = null,
                            Description = "Controls 3D Objects folder visibility in This PC",
                            ActionType = RegistryActionType.Remove,
                            IsGuidSubkey = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-home-folder",
                    Name = "Home Folder in Navigation Pane",
                    Description = "Controls Home Folder visibility in Navigation Pane",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Desktop\\NameSpace",
                            Name = "{f874310e-b6b7-47dc-bc84-b9e6b38f5903}",
                            RecommendedValue = null,
                            EnabledValue = null, // When toggle is ON, Home Folder is shown (key exists)
                            DisabledValue = null, // When toggle is OFF, Home Folder is hidden (key removed)
                            ValueType = RegistryValueKind.None,
                            DefaultValue = null,
                            Description = "Controls Home Folder visibility in Navigation Pane",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                            ActionType = RegistryActionType.Remove,
                            IsGuidSubkey = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-launch-to",
                    Name = "Launch to This PC",
                    Description = "Controls where File Explorer opens by default",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "LaunchTo",
                            RecommendedValue = 1, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, File Explorer opens to 'This PC'
                            DisabledValue = 2, // When toggle is OFF, File Explorer opens to 'Quick access'
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            Description = "Controls where File Explorer opens by default",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-show-file-ext",
                    Name = "Show File Extensions",
                    Description = "Controls visibility of file name extensions",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "HideFileExt",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 0, // When toggle is ON, file extensions are shown
                            DisabledValue = 1, // When toggle is OFF, file extensions are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls visibility of file name extensions",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-show-hidden-files",
                    Name = "Show Hidden Files, Folders & Drives",
                    Description = "Controls visibility of hidden files and folders",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Hidden",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description = "Controls visibility of hidden files, folders and drives",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-hide-protected-files",
                    Name = "Hide Protected Operating System Files",
                    Description = "Controls visibility of protected operating system files",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "ShowSuperHidden",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1,
                            Description = "Controls visibility of protected operating system files",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-folder-tips",
                    Name = "Folder Tips",
                    Description = "Controls file size information in folder tips",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "FolderContentsInfoTip",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, folder tips are enabled
                            DisabledValue = 0, // When toggle is OFF, folder tips are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls file size information in folder tips",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-popup-descriptions",
                    Name = "Pop-up Descriptions",
                    Description = "Controls pop-up descriptions for folder and desktop items",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "ShowInfoTip",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, pop-up descriptions are shown
                            DisabledValue = 0, // When toggle is OFF, pop-up descriptions are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description =
                                "Controls pop-up descriptions for folder and desktop items",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-preview-handlers",
                    Name = "Preview Handlers",
                    Description = "Controls preview handlers in preview pane",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "ShowPreviewHandlers",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, preview handlers are enabled
                            DisabledValue = 0, // When toggle is OFF, preview handlers are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls preview handlers in preview pane",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-status-bar",
                    Name = "Status Bar",
                    Description = "Controls status bar visibility in File Explorer",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "ShowStatusBar",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, status bar is shown
                            DisabledValue = 0, // When toggle is OFF, status bar is hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls status bar visibility in File Explorer",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-show-thumbnails",
                    Name = "Show Thumbnails",
                    Description = "Controls whether to show thumbnails or icons",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "IconsOnly",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 0, // When toggle is ON, thumbnails are shown
                            DisabledValue = 1, // When toggle is OFF, icons are shown
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls whether to show thumbnails or icons",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-translucent-selection",
                    Name = "Translucent Selection",
                    Description = "Controls translucent selection rectangle",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "ListviewAlphaSelect",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, translucent selection is enabled
                            DisabledValue = 0, // When toggle is OFF, translucent selection is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls translucent selection rectangle",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-drop-shadows",
                    Name = "Drop Shadows",
                    Description = "Controls drop shadows for icon labels",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "ListviewShadow",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, drop shadows are enabled
                            DisabledValue = 0, // When toggle is OFF, drop shadows are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls drop shadows for icon labels",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-full-path",
                    Name = "Full Path in Title Bar",
                    Description = "Controls full path display in the title bar",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\CabinetState",
                            Name = "FullPath",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, full path is shown
                            DisabledValue = 0, // When toggle is OFF, full path is hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls full path display in the title bar",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-font-smoothing",
                    Name = "Font Smoothing",
                    Description = "Controls smooth edges of screen fonts",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Control Panel\\Desktop",
                            Name = "FontSmoothing",
                            RecommendedValue = "2",
                            EnabledValue = "2", // When toggle is ON, font smoothing is enabled
                            DisabledValue = "0", // When toggle is OFF, font smoothing is disabled
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "0", // Default value when registry key exists but no value is set
                            Description = "Controls smooth edges of screen fonts",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-dpi-scaling",
                    Name = "DPI Scaling (100%)",
                    Description = "Controls DPI scaling setting",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Control Panel\\Desktop",
                            Name = "LogPixels",
                            RecommendedValue = 96,
                            EnabledValue = 96, // When toggle is ON, DPI scaling is set to 100%
                            DisabledValue = 120, // When toggle is OFF, DPI scaling is set to 125%
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 120, // Default value when registry key exists but no value is set
                            Description = "Controls DPI scaling setting",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-per-process-dpi",
                    Name = "Per-Process DPI",
                    Description = "Controls per-process system DPI",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey = "Control Panel\\Desktop",
                            Name = "EnablePerProcessSystemDPI",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, per-process DPI is enabled
                            DisabledValue = 0, // When toggle is OFF, per-process DPI is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls per-process system DPI",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-lock-screen",
                    Name = "Lock Screen",
                    Description = "Controls lock screen visibility",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization",
                            Name = "NoLockScreen",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            Description = "Controls lock screen visibility",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-gallery",
                    Name = "Gallery in Navigation Pane",
                    Description = "Controls gallery visibility in navigation pane",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_LOCAL_MACHINE",
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Desktop\\NameSpace",
                            Name = "{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}",
                            RecommendedValue = null,
                            EnabledValue = null, // When toggle is ON, gallery is shown (key exists)
                            DisabledValue = null, // When toggle is OFF, gallery is hidden (key removed)
                            ValueType = RegistryValueKind.None,
                            DefaultValue = null,
                            Description = "Controls gallery visibility in navigation pane",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                            ActionType = RegistryActionType.Remove,
                            IsGuidSubkey = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "explorer-customization-context-menu",
                    Name = "Classic Context Menu",
                    Description = "Controls context menu style (classic or modern)",
                    Category = CustomizationCategory.Explorer,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = "HKEY_CURRENT_USER",
                            SubKey =
                                "Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32",
                            Name = "",
                            RecommendedValue = "",
                            EnabledValue = null, // When toggle is ON, classic context menu is used (value is deleted)
                            DisabledValue = "", // When toggle is OFF, modern context menu is used (empty value is set)
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "", // Default value when registry key exists but no value is set
                            Description = "Controls context menu style (classic or modern)",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
            },
        };
    }
}

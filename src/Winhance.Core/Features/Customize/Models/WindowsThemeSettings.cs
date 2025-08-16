using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Enums;

namespace Winhance.Core.Features.Customize.Models
{
    /// <summary>
    /// Model for Windows theme settings.
    /// </summary>
    public static class WindowsThemeSettings
    {
        // Wallpaper paths
        public static class Wallpaper
        {
            // Windows 11 wallpaper paths
            public const string Windows11BasePath = @"C:\Windows\Web\Wallpaper\Windows";
            public const string Windows11LightWallpaper = "img0.jpg";
            public const string Windows11DarkWallpaper = "img19.jpg";

            // Windows 10 wallpaper path
            public const string Windows10Wallpaper =
                @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg";

            public static string GetDefaultWallpaperPath(bool isWindows11, bool isDarkMode)
            {
                if (isWindows11)
                {
                    return System.IO.Path.Combine(
                        Windows11BasePath,
                        isDarkMode ? Windows11DarkWallpaper : Windows11LightWallpaper
                    );
                }

                return Windows10Wallpaper;
            }
        }

        public static CustomizationGroup GetWindowsThemeCustomizations()
        {
            return new CustomizationGroup
            {
                Name = "Windows Theme",
                Category = CustomizationCategory.WindowsTheme,
                Settings = new List<CustomizationSetting>
                {
                    // Theme Mode Selection (ComboBox)
                    new CustomizationSetting
                    {
                        Id = "theme-mode-windows",
                        Name = "Choose Your Mode",
                        Description = "Choose between Light and Dark mode for Windows and apps",
                        Category = CustomizationCategory.WindowsTheme,
                        GroupName = "Windows Theme",
                        IsEnabled = true,
                        ControlType = ControlType.ComboBox,
                        RequiresConfirmation = true,
                        ConfirmationTitle = "Windows Theme Change",
                        ConfirmationMessage =
                            "You are about to apply {themeMode} to Windows and apps.\n\nDo you want to continue?",
                        ConfirmationCheckboxText =
                            "Apply default Windows wallpaper for {themeMode}",
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                Category = "WindowsTheme",
                                Hive = "HKEY_CURRENT_USER",
                                SubKey =
                                    "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                                Name = "AppsUseLightTheme",
                                RecommendedValue = 0, // Dark mode recommended
                                ValueType = RegistryValueKind.DWord,
                                DefaultValue = 1, // Light mode is Windows default
                                Description = "Controls Windows Apps theme mode (0=Dark, 1=Light)",
                                IsPrimary = true,
                                AbsenceMeansEnabled = false,
                                CustomProperties = new Dictionary<string, object>
                                {
                                    ["ComboBoxOptions"] = new Dictionary<string, int>
                                    {
                                        ["Light Mode"] = 1, // AppsUseLightTheme = 1
                                        ["Dark Mode"] = 0, // AppsUseLightTheme = 0
                                    },
                                    ["DefaultOption"] = "Light Mode",
                                },
                            },
                            new RegistrySetting
                            {
                                Category = "WindowsTheme",
                                Hive = "HKEY_CURRENT_USER",
                                SubKey =
                                    "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                                Name = "SystemUsesLightTheme",
                                RecommendedValue = 0, // Dark mode recommended
                                ValueType = RegistryValueKind.DWord,
                                DefaultValue = 1, // Light mode is Windows default
                                Description =
                                    "Controls Windows System theme mode (0=Dark, 1=Light)",
                                IsPrimary = false, // Secondary setting, follows the primary
                                AbsenceMeansEnabled = false,
                            },
                        },
                    },
                    // Transparency Effects Toggle
                    new CustomizationSetting
                    {
                        Id = "theme-transparency",
                        Name = "Transparency Effects",
                        Description = "Controls whether windows and surfaces appear translucent",
                        Category = CustomizationCategory.WindowsTheme,
                        GroupName = "Windows Theme",
                        IsEnabled = true, // Enable this setting
                        ControlType = ControlType.BinaryToggle,
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                Category = "WindowsTheme",
                                Hive = "HKEY_CURRENT_USER",
                                SubKey =
                                    "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                                Name = "EnableTransparency",
                                RecommendedValue = 1, // Enable transparency recommended
                                EnabledValue = 1, // When toggle is ON, transparency effects are enabled
                                DisabledValue = 0, // When toggle is OFF, transparency effects are disabled
                                ValueType = RegistryValueKind.DWord,
                                DefaultValue = 1, // Default value when registry key exists but no value is set
                                Description = "Controls transparency effects in Windows",
                                IsPrimary = true,
                                AbsenceMeansEnabled = true,
                            },
                        },
                    },
                },
            };
        }
    }
}

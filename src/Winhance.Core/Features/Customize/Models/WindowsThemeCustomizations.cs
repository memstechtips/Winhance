using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Models.WindowsRegistry;

namespace Winhance.Core.Features.Customize.Models
{
    /// <summary>
    /// Model for Windows theme settings.
    /// </summary>
    public static class WindowsThemeCustomizations
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

        public static SettingGroup GetWindowsThemeCustomizations()
        {
            return new SettingGroup
            {
                Name = "Windows Theme",
                FeatureId = FeatureIds.WindowsTheme,
                Settings = new List<SettingDefinition>
                {
                    // Theme Mode Selection (ComboBox)
                    new SettingDefinition
                    {
                        Id = "theme-mode-windows",
                        Name = "Choose Your Mode",
                        Description = "Choose between Light and Dark mode for Windows and apps",
                        GroupName = "Windows Theme",
                        InputType = SettingInputType.Selection,
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
                                KeyPath =
                                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                                ValueName = "AppsUseLightTheme",
                                RecommendedValue = 0, // Dark mode recommended
                                DefaultValue = 1, // Light mode is Windows default
                                ValueType = RegistryValueKind.DWord,
                                CustomProperties = new Dictionary<string, object>
                                {
                                    ["DefaultOption"] = "Light Mode",
                                },
                            },
                            new RegistrySetting
                            {
                                KeyPath =
                                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                                ValueName = "SystemUsesLightTheme",
                                RecommendedValue = 0, // Dark mode recommended
                                DefaultValue = 1, // Light mode is Windows default
                                ValueType = RegistryValueKind.DWord,
                            },
                        },
                        CustomProperties = new Dictionary<string, object>
                        {
                            [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                            {
                                "Light Mode",
                                "Dark Mode",
                            },
                            [CustomPropertyKeys.ValueMappings] = new Dictionary<
                                int,
                                Dictionary<string, int>
                            >
                            {
                                [0] = new Dictionary<string, int> // Light Mode
                                {
                                    ["AppsUseLightTheme"] = 1,
                                    ["SystemUsesLightTheme"] = 1,
                                },
                                [1] = new Dictionary<string, int> // Dark Mode
                                {
                                    ["AppsUseLightTheme"] = 0,
                                    ["SystemUsesLightTheme"] = 0,
                                },
                            },
                            [CustomPropertyKeys.SupportsCustomState] = true,
                            [CustomPropertyKeys.CustomStateDisplayName] = "Custom (User Defined)",
                        },
                    },
                    // Transparency Effects Toggle
                    new SettingDefinition
                    {
                        Id = "theme-transparency",
                        Name = "Transparency Effects",
                        Description = "Controls whether windows and surfaces appear translucent",
                        GroupName = "Windows Theme",
                        InputType = SettingInputType.Toggle,
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath =
                                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                                ValueName = "EnableTransparency",
                                RecommendedValue = 1, // Enable transparency recommended
                                EnabledValue = 1, // When toggle is ON, transparency effects are enabled
                                DisabledValue = 0, // When toggle is OFF, transparency effects are disabled
                                DefaultValue = 1, // Default value when registry key exists but no value is set
                                ValueType = RegistryValueKind.DWord,
                            },
                        },
                    },
                },
            };
        }
    }
}

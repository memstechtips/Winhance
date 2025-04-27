using Microsoft.Win32;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Enums;
using Winhance.Core.Features.Customize.Interfaces;

namespace Winhance.Core.Features.Customize.Models
{
    /// <summary>
    /// Model for Windows theme settings.
    /// </summary>
    public class WindowsThemeSettings
    {
        // Registry paths and keys
        public static class Registry
        {
            public const string ThemesPersonalizeSubKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            public const string AppsUseLightThemeName = "AppsUseLightTheme";
            public const string SystemUsesLightThemeName = "SystemUsesLightTheme";
        }

        // Wallpaper paths
        public static class Wallpaper
        {
            // Windows 11 wallpaper paths
            public const string Windows11BasePath = @"C:\Windows\Web\Wallpaper\Windows";
            public const string Windows11LightWallpaper = "img0.jpg";
            public const string Windows11DarkWallpaper = "img19.jpg";

            // Windows 10 wallpaper path
            public const string Windows10Wallpaper = @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg";

            public static string GetDefaultWallpaperPath(bool isWindows11, bool isDarkMode)
            {
                if (isWindows11)
                {
                    return System.IO.Path.Combine(
                        Windows11BasePath, 
                        isDarkMode ? Windows11DarkWallpaper : Windows11LightWallpaper);
                }
                
                return Windows10Wallpaper;
            }
        }

        private readonly IThemeService _themeService;
        private bool _isDarkMode;
        private bool _changeWallpaper;

        /// <summary>
        /// Gets or sets a value indicating whether dark mode is enabled.
        /// </summary>
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set => _isDarkMode = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to change the wallpaper when changing the theme.
        /// </summary>
        public bool ChangeWallpaper
        {
            get => _changeWallpaper;
            set => _changeWallpaper = value;
        }

        /// <summary>
        /// Gets the current theme name.
        /// </summary>
        public string ThemeName => IsDarkMode ? "Dark Mode" : "Light Mode";

        /// <summary>
        /// Gets the available theme options.
        /// </summary>
        public List<string> ThemeOptions { get; } = new List<string> { "Light Mode", "Dark Mode" };

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsThemeSettings"/> class.
        /// </summary>
        public WindowsThemeSettings()
        {
            // Default constructor for serialization
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsThemeSettings"/> class.
        /// </summary>
        /// <param name="themeService">The theme service.</param>
        public WindowsThemeSettings(IThemeService themeService)
        {
            _themeService = themeService;
            _isDarkMode = themeService?.IsDarkModeEnabled() ?? false;
        }

        /// <summary>
        /// Loads the current theme settings from the system.
        /// </summary>
        public void LoadCurrentSettings()
        {
            if (_themeService != null)
            {
                _isDarkMode = _themeService.IsDarkModeEnabled();
            }
        }

        /// <summary>
        /// Applies the current theme settings to the system.
        /// </summary>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        public bool ApplyTheme()
        {
            return _themeService?.SetThemeMode(_isDarkMode) ?? false;
        }

        /// <summary>
        /// Applies the current theme settings to the system asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<bool> ApplyThemeAsync()
        {
            if (_themeService == null)
                return false;

            return await _themeService.ApplyThemeAsync(_isDarkMode, _changeWallpaper);
        }

        /// <summary>
        /// Creates registry settings for Windows theme.
        /// </summary>
        /// <returns>A list of registry settings.</returns>
        public static List<RegistrySetting> CreateRegistrySettings()
        {
            return new List<RegistrySetting>
            {
                new RegistrySetting
                {
                    Category = "WindowsTheme",
                    Hive = RegistryHive.CurrentUser,
                    SubKey = Registry.ThemesPersonalizeSubKey,
                    Name = Registry.AppsUseLightThemeName,
                    EnabledValue = 0, // Dark mode
                    DisabledValue = 1, // Light mode
                    ValueType = RegistryValueKind.DWord,
                    Description = "Windows Apps Theme Mode",
                    // For backward compatibility
                    RecommendedValue = 0,
                    DefaultValue = 1
                },
                new RegistrySetting
                {
                    Category = "WindowsTheme",
                    Hive = RegistryHive.CurrentUser,
                    SubKey = Registry.ThemesPersonalizeSubKey,
                    Name = Registry.SystemUsesLightThemeName,
                    EnabledValue = 0, // Dark mode
                    DisabledValue = 1, // Light mode
                    ValueType = RegistryValueKind.DWord,
                    Description = "Windows System Theme Mode",
                    // For backward compatibility
                    RecommendedValue = 0,
                    DefaultValue = 1
                }
            };
        }

        /// <summary>
        /// Creates a customization setting for Windows theme.
        /// </summary>
        /// <returns>A customization setting.</returns>
        public static CustomizationSetting CreateCustomizationSetting()
        {
            var setting = new CustomizationSetting
            {
                Id = "WindowsTheme",
                Name = "Windows Theme",
                Description = "Toggle between light and dark theme for Windows",
                GroupName = "Windows Theme",
                Category = CustomizationCategory.Theme,
                ControlType = ControlType.ComboBox,
                RegistrySettings = CreateRegistrySettings()
            };

            return setting;
        }
    }
}
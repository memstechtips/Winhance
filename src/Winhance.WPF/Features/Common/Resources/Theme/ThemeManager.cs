using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Properties;

namespace Winhance.WPF.Features.Common.Resources.Theme
{
    public partial class ThemeManager : ObservableObject, IThemeManager, IDisposable
    {
        private bool _isDarkTheme = true;
        private bool _isHighContrast;

        public bool IsHighContrast
        {
            get => _isHighContrast;
            private set
            {
                if (_isHighContrast != value)
                {
                    _isHighContrast = value;
                    OnPropertyChanged(nameof(IsHighContrast));
                }
            }
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (_isDarkTheme != value)
                {
                    _isDarkTheme = value;
                    OnPropertyChanged(nameof(IsDarkTheme));
                    Application.Current.Resources["IsDarkTheme"] = _isDarkTheme;
                }
            }
        }

        private readonly INavigationService _navigationService;
        private readonly IWindowsThemeQueryService _windowsThemeQueryService;

        private static readonly Dictionary<string, Color> DarkThemeColors = new()
        {
            { "PrimaryTextColor", Color.FromRgb(255, 255, 255) },
            { "SecondaryTextColor", Color.FromRgb(170, 170, 170) },
            { "TertiaryTextColor", Color.FromRgb(128, 128, 128) },
            { "HelpIconColor", Color.FromRgb(255, 255, 255) },
            { "TooltipBackgroundColor", Color.FromRgb(43, 45, 48) },
            { "TooltipForegroundColor", Color.FromRgb(255, 255, 255) },
            { "TooltipBorderColor", Color.FromRgb(255, 222, 0) },
            { "ControlForegroundColor", Color.FromRgb(255, 255, 255) },
            { "ControlFillColor", Color.FromRgb(255, 255, 255) },
            { "ControlBorderColor", Color.FromRgb(255, 222, 0) },
            { "ToggleKnobColor", Color.FromRgb(255, 255, 255) },
            { "ToggleKnobCheckedColor", Color.FromRgb(255, 222, 0) },
            { "ContentSectionBorderColor", Color.FromRgb(31, 32, 34) },
            { "MainContainerBorderColor", Color.FromRgb(43, 45, 48) },
            { "SettingsItemBackgroundColor", Color.FromRgb(37, 38, 40) },
            { "PrimaryButtonForegroundColor", Color.FromRgb(255, 255, 255) },
            { "AccentColor", Color.FromRgb(255, 222, 0) },
            { "ButtonHoverTextColor", Color.FromRgb(32, 33, 36) },
            { "ButtonDisabledForegroundColor", Color.FromRgb(153, 163, 164) },
            { "ButtonDisabledBorderColor", Color.FromRgb(43, 45, 48) },
            { "NavigationButtonBackgroundColor", Color.FromRgb(31, 32, 34) },
            { "NavigationButtonForegroundColor", Color.FromRgb(255, 255, 255) },
            { "SliderTrackColor", Color.FromRgb(64, 64, 64) },
            { "BackgroundColor", Color.FromRgb(32, 32, 32) },
            { "ContentSectionBackgroundColor", Color.FromRgb(31, 32, 34) },
            { "ElevatedBackgroundColor", Color.FromRgb(60, 60, 60) },
            { "ScrollBarThumbColor", Color.FromRgb(255, 222, 0) },
            { "ScrollBarThumbHoverColor", Color.FromRgb(255, 233, 76) },
            { "ScrollBarThumbPressedColor", Color.FromRgb(255, 240, 102) },
        };

        private static readonly Dictionary<string, Color> LightThemeColors = new()
        {
            { "PrimaryTextColor", Color.FromRgb(32, 33, 36) },
            { "SecondaryTextColor", Color.FromRgb(102, 102, 102) },
            { "TertiaryTextColor", Color.FromRgb(153, 153, 153) },
            { "HelpIconColor", Color.FromRgb(32, 33, 36) },
            { "TooltipBackgroundColor", Color.FromRgb(255, 255, 255) },
            { "TooltipForegroundColor", Color.FromRgb(32, 33, 36) },
            { "TooltipBorderColor", Color.FromRgb(66, 66, 66) },
            { "ControlForegroundColor", Color.FromRgb(32, 33, 36) },
            { "ControlFillColor", Color.FromRgb(66, 66, 66) },
            { "ControlBorderColor", Color.FromRgb(66, 66, 66) },
            { "ToggleKnobColor", Color.FromRgb(255, 255, 255) },
            { "ToggleKnobCheckedColor", Color.FromRgb(66, 66, 66) },
            { "ContentSectionBorderColor", Color.FromRgb(234, 236, 242) },
            { "MainContainerBorderColor", Color.FromRgb(255, 255, 255) },
            { "SettingsItemBackgroundColor", Color.FromRgb(255, 255, 255) },
            { "PrimaryButtonForegroundColor", Color.FromRgb(32, 33, 36) },
            { "AccentColor", Color.FromRgb(0, 120, 212) },
            { "ButtonHoverTextColor", Color.FromRgb(255, 255, 255) },
            { "ButtonDisabledForegroundColor", Color.FromRgb(204, 204, 204) },
            { "ButtonDisabledBorderColor", Color.FromRgb(238, 238, 238) },
            { "NavigationButtonBackgroundColor", Color.FromRgb(246, 248, 252) },
            { "NavigationButtonForegroundColor", Color.FromRgb(32, 33, 36) },
            { "SliderTrackColor", Color.FromRgb(204, 204, 204) },
            { "BackgroundColor", Color.FromRgb(246, 248, 252) },
            { "ContentSectionBackgroundColor", Color.FromRgb(234, 236, 242) },
            { "ElevatedBackgroundColor", Color.FromRgb(224, 224, 224) },
            { "ScrollBarThumbColor", Color.FromRgb(66, 66, 66) },
            { "ScrollBarThumbHoverColor", Color.FromRgb(102, 102, 102) },
            { "ScrollBarThumbPressedColor", Color.FromRgb(34, 34, 34) },
        };

        private static Dictionary<string, Color> GetHighContrastColors()
        {
            return new Dictionary<string, Color>
            {
                { "PrimaryTextColor", SystemColors.WindowTextColor },
                { "SecondaryTextColor", SystemColors.GrayTextColor },
                { "TertiaryTextColor", SystemColors.GrayTextColor },
                { "HelpIconColor", SystemColors.WindowTextColor },
                { "TooltipBackgroundColor", SystemColors.InfoColor },
                { "TooltipForegroundColor", SystemColors.InfoTextColor },
                { "TooltipBorderColor", SystemColors.WindowFrameColor },
                { "ControlForegroundColor", SystemColors.WindowTextColor },
                { "ControlFillColor", SystemColors.HighlightColor },
                { "ControlBorderColor", SystemColors.WindowFrameColor },
                { "ToggleKnobColor", SystemColors.WindowColor },
                { "ToggleKnobCheckedColor", SystemColors.HighlightColor },
                { "ContentSectionBorderColor", SystemColors.ActiveBorderColor },
                { "MainContainerBorderColor", SystemColors.ActiveBorderColor },
                { "SettingsItemBackgroundColor", SystemColors.WindowColor },
                { "PrimaryButtonForegroundColor", SystemColors.HighlightTextColor },
                { "AccentColor", SystemColors.HighlightColor },
                { "ButtonHoverTextColor", SystemColors.HighlightTextColor },
                { "ButtonDisabledForegroundColor", SystemColors.GrayTextColor },
                { "ButtonDisabledBorderColor", SystemColors.InactiveBorderColor },
                { "NavigationButtonBackgroundColor", SystemColors.ControlColor },
                { "NavigationButtonForegroundColor", SystemColors.ControlTextColor },
                { "SliderTrackColor", SystemColors.ControlDarkColor },
                { "BackgroundColor", SystemColors.WindowColor },
                { "ContentSectionBackgroundColor", SystemColors.WindowColor },
                { "ElevatedBackgroundColor", SystemColors.ControlColor },
                { "ScrollBarThumbColor", SystemColors.ControlDarkColor },
                { "ScrollBarThumbHoverColor", SystemColors.ControlDarkDarkColor },
                { "ScrollBarThumbPressedColor", SystemColors.HighlightColor },
            };
        }

        public ThemeManager(INavigationService navigationService, IWindowsThemeQueryService windowsThemeQueryService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _windowsThemeQueryService = windowsThemeQueryService ?? throw new ArgumentNullException(nameof(windowsThemeQueryService));

            SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;

            LoadThemePreference();
            ApplyTheme();
        }

        public void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ApplyTheme();
            SaveThemePreference();
        }

        public void ApplyTheme()
        {
            try
            {
                var themeColors = IsHighContrast
                    ? GetHighContrastColors()
                    : (IsDarkTheme ? DarkThemeColors : LightThemeColors);

                var brushes = new List<(string key, SolidColorBrush brush)>
                {
                    ("WindowBackground", new SolidColorBrush(themeColors["BackgroundColor"])),
                    ("PrimaryTextColor", new SolidColorBrush(themeColors["PrimaryTextColor"])),
                    ("SecondaryTextColor", new SolidColorBrush(themeColors["SecondaryTextColor"])),
                    ("TertiaryTextColor", new SolidColorBrush(themeColors["TertiaryTextColor"])),
                    ("SubTextColor", new SolidColorBrush(themeColors["SecondaryTextColor"])),
                    ("HelpIconForeground", new SolidColorBrush(themeColors["HelpIconColor"])),
                    ("ContentSectionBackground", new SolidColorBrush(themeColors["ContentSectionBackgroundColor"])),
                    ("ElevatedBackground", new SolidColorBrush(themeColors["ElevatedBackgroundColor"])),
                    ("ContentSectionBorderBrush", new SolidColorBrush(themeColors["ContentSectionBorderColor"])),
                    ("MainContainerBorderBrush", new SolidColorBrush(themeColors["MainContainerBorderColor"])),
                    ("SettingsItemBackground", new SolidColorBrush(themeColors["SettingsItemBackgroundColor"])),
                    ("NavigationButtonBackground", new SolidColorBrush(themeColors["NavigationButtonBackgroundColor"])),
                    ("NavigationButtonForeground", new SolidColorBrush(themeColors["NavigationButtonForegroundColor"])),
                    ("ButtonBorderBrush", new SolidColorBrush(themeColors["AccentColor"])),
                    ("ButtonHoverBackground", new SolidColorBrush(themeColors["AccentColor"])),
                    ("ButtonHoverTextColor", new SolidColorBrush(themeColors["ButtonHoverTextColor"])),
                    ("PrimaryButtonForeground", new SolidColorBrush(themeColors["PrimaryButtonForegroundColor"])),
                    ("ButtonDisabledForeground", new SolidColorBrush(themeColors["ButtonDisabledForegroundColor"])),
                    ("ButtonDisabledBorderBrush", new SolidColorBrush(themeColors["ButtonDisabledBorderColor"])),
                    ("ButtonDisabledHoverBackground", new SolidColorBrush(themeColors["ButtonDisabledBorderColor"])),
                    ("ButtonDisabledHoverForeground", new SolidColorBrush(themeColors["ButtonDisabledForegroundColor"])),
                    ("TooltipBackground", new SolidColorBrush(themeColors["TooltipBackgroundColor"])),
                    ("TooltipForeground", new SolidColorBrush(themeColors["TooltipForegroundColor"])),
                    ("TooltipBorderBrush", new SolidColorBrush(themeColors["TooltipBorderColor"])),
                    ("ControlForeground", new SolidColorBrush(themeColors["ControlForegroundColor"])),
                    ("ControlFillColor", new SolidColorBrush(themeColors["ControlFillColor"])),
                    ("ControlBorderBrush", new SolidColorBrush(themeColors["ControlBorderColor"])),
                    ("ToggleKnobBrush", new SolidColorBrush(themeColors["ToggleKnobColor"])),
                    ("ToggleKnobCheckedBrush", new SolidColorBrush(themeColors["ToggleKnobCheckedColor"])),
                    ("SliderTrackBackground", new SolidColorBrush(themeColors["SliderTrackColor"])),
                    ("SliderAccentColor", new SolidColorBrush(IsDarkTheme ? themeColors["AccentColor"] : Color.FromRgb(240, 240, 240))),
                    ("TickBarForeground", new SolidColorBrush(themeColors["PrimaryTextColor"])),
                    ("ScrollBarThumbBrush", new SolidColorBrush(themeColors["ScrollBarThumbColor"])),
                    ("ScrollBarThumbHoverBrush", new SolidColorBrush(themeColors["ScrollBarThumbHoverColor"])),
                    ("ScrollBarThumbPressedBrush", new SolidColorBrush(themeColors["ScrollBarThumbPressedColor"])),
                };

                foreach (var (key, brush) in brushes)
                {
                    brush.Freeze();
                }

                var resources = Application.Current.Resources;

                resources.BeginInit();
                try
                {
                    foreach (var (key, brush) in brushes)
                    {
                        resources[key] = brush;
                    }
                }
                finally
                {
                    resources.EndInit();
                }

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    NotifyWindowsOfThemeChange();
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
            }
        }

        private void SaveThemePreference()
        {
            try
            {
                Settings.Default.ThemeSetByUser = true;
                Settings.Default.IsDarkTheme = IsDarkTheme;
                Settings.Default.Save();
            }
            catch
            {
            }
        }

        public void LoadThemePreference()
        {
            try
            {
                IsHighContrast = SystemParameters.HighContrast;

                if (!Settings.Default.ThemeSetByUser)
                {
                    IsDarkTheme = _windowsThemeQueryService.IsDarkModeEnabled();
                }
                else
                {
                    IsDarkTheme = Settings.Default.IsDarkTheme;
                }
            }
            catch
            {
                IsDarkTheme = true;
            }
        }

        public void Dispose()
        {
            SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        }

        private void OnSystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
            {
                IsHighContrast = SystemParameters.HighContrast;
                ApplyTheme();

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    NotifyWindowsOfThemeChange();
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        public void ResetThemePreference()
        {
            try
            {
                Settings.Default.Reset();
                LoadThemePreference();
                ApplyTheme();
            }
            catch
            {
            }
        }

        private void NotifyWindowsOfThemeChange()
        {
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is Winhance.WPF.Features.Common.Interfaces.IThemeAwareWindow themeAware)
                    {
                        window.Dispatcher.Invoke(() => themeAware.OnThemeChanged(IsDarkTheme));
                    }
                }
            }
            catch
            {
            }
        }
    }
}

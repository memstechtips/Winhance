using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Properties;

namespace Winhance.WPF.Features.Common.Resources.Theme
{
    public partial class ThemeManager : ObservableObject, IThemeManager, IDisposable
    {
        private bool _isDarkTheme = true;

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (_isDarkTheme != value)
                {
                    _isDarkTheme = value;
                    OnPropertyChanged(nameof(IsDarkTheme));

                    // Update the application resource
                    Application.Current.Resources["IsDarkTheme"] = _isDarkTheme;
                }
            }
        }

        private readonly INavigationService _navigationService;

        // Dictionary with default color values for each theme
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
            { "ContentSectionBorderColor", Color.FromRgb(246, 248, 252) },
            { "MainContainerBorderColor", Color.FromRgb(255, 255, 255) },
            { "SettingsItemBackgroundColor", Color.FromRgb(255, 255, 255) },
            { "PrimaryButtonForegroundColor", Color.FromRgb(32, 33, 36) },
            { "AccentColor", Color.FromRgb(66, 66, 66) },
            { "ButtonHoverTextColor", Color.FromRgb(255, 255, 255) },
            { "ButtonDisabledForegroundColor", Color.FromRgb(204, 204, 204) },
            { "ButtonDisabledBorderColor", Color.FromRgb(238, 238, 238) },
            { "NavigationButtonBackgroundColor", Color.FromRgb(246, 248, 252) },
            { "NavigationButtonForegroundColor", Color.FromRgb(32, 33, 36) },
            { "SliderTrackColor", Color.FromRgb(204, 204, 204) },
            { "BackgroundColor", Color.FromRgb(246, 248, 252) },
            { "ContentSectionBackgroundColor", Color.FromRgb(240, 240, 240) },
            { "ScrollBarThumbColor", Color.FromRgb(66, 66, 66) },
            { "ScrollBarThumbHoverColor", Color.FromRgb(102, 102, 102) },
            { "ScrollBarThumbPressedColor", Color.FromRgb(34, 34, 34) },
        };

        public ThemeManager(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // Subscribe to navigation events to update toggle switches when navigating between views
            _navigationService.Navigated += NavigationService_Navigated;

            LoadThemePreference();
            ApplyTheme();
        }

        private void NavigationService_Navigated(object sender, NavigationEventArgs e)
        {
            // We no longer need to update toggle switches on navigation
            // as they will automatically pick up the correct theme from the application resources
        }

        public void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ApplyTheme();

            // Ensure the window icons are updated when toggling the theme
            NotifyWindowsOfThemeChange();
        }

        public void ApplyTheme()
        {
            try
            {
                var themeColors = IsDarkTheme ? DarkThemeColors : LightThemeColors;

                // We no longer need to explicitly update toggle switches
                // as they will automatically pick up the theme from application resources

                // Create brushes for all UI elements
                var brushes = new List<(string key, SolidColorBrush brush)>
                {
                    ("WindowBackground", new SolidColorBrush(themeColors["BackgroundColor"])),
                    ("PrimaryTextColor", new SolidColorBrush(themeColors["PrimaryTextColor"])),
                    ("SecondaryTextColor", new SolidColorBrush(themeColors["SecondaryTextColor"])),
                    ("TertiaryTextColor", new SolidColorBrush(themeColors["TertiaryTextColor"])),
                    ("SubTextColor", new SolidColorBrush(themeColors["SecondaryTextColor"])),
                    ("HelpIconForeground", new SolidColorBrush(themeColors["HelpIconColor"])),
                    (
                        "ContentSectionBackground",
                        new SolidColorBrush(themeColors["ContentSectionBackgroundColor"])
                    ),
                    (
                        "ContentSectionBorderBrush",
                        new SolidColorBrush(themeColors["ContentSectionBorderColor"])
                    ),
                    (
                        "MainContainerBorderBrush",
                        new SolidColorBrush(themeColors["MainContainerBorderColor"])
                    ),
                    (
                        "SettingsItemBackground",
                        new SolidColorBrush(themeColors["SettingsItemBackgroundColor"])
                    ),
                    (
                        "NavigationButtonBackground",
                        new SolidColorBrush(themeColors["NavigationButtonBackgroundColor"])
                    ),
                    (
                        "NavigationButtonForeground",
                        new SolidColorBrush(themeColors["NavigationButtonForegroundColor"])
                    ),
                    ("ButtonBorderBrush", new SolidColorBrush(themeColors["AccentColor"])),
                    ("ButtonHoverBackground", new SolidColorBrush(themeColors["AccentColor"])),
                    (
                        "ButtonHoverTextColor",
                        new SolidColorBrush(themeColors["ButtonHoverTextColor"])
                    ),
                    (
                        "PrimaryButtonForeground",
                        new SolidColorBrush(themeColors["PrimaryButtonForegroundColor"])
                    ),
                    (
                        "ButtonDisabledForeground",
                        new SolidColorBrush(themeColors["ButtonDisabledForegroundColor"])
                    ),
                    (
                        "ButtonDisabledBorderBrush",
                        new SolidColorBrush(themeColors["ButtonDisabledBorderColor"])
                    ),
                    (
                        "ButtonDisabledHoverBackground",
                        new SolidColorBrush(themeColors["ButtonDisabledBorderColor"])
                    ),
                    (
                        "ButtonDisabledHoverForeground",
                        new SolidColorBrush(themeColors["ButtonDisabledForegroundColor"])
                    ),
                    (
                        "TooltipBackground",
                        new SolidColorBrush(themeColors["TooltipBackgroundColor"])
                    ),
                    (
                        "TooltipForeground",
                        new SolidColorBrush(themeColors["TooltipForegroundColor"])
                    ),
                    ("TooltipBorderBrush", new SolidColorBrush(themeColors["TooltipBorderColor"])),
                    (
                        "ControlForeground",
                        new SolidColorBrush(themeColors["ControlForegroundColor"])
                    ),
                    ("ControlFillColor", new SolidColorBrush(themeColors["ControlFillColor"])),
                    (
                        "ControlBorderBrush",
                        new SolidColorBrush(themeColors["ControlBorderColor"])
                    ),
                    (
                        "ToggleKnobBrush",
                        new SolidColorBrush(themeColors["ToggleKnobColor"])
                    ),
                    (
                        "ToggleKnobCheckedBrush",
                        new SolidColorBrush(themeColors["ToggleKnobCheckedColor"])
                    ),
                    ("SliderTrackBackground", new SolidColorBrush(themeColors["SliderTrackColor"])),
                    // Special handling for slider thumb in light mode to make them more visible
                    (
                        "SliderAccentColor",
                        new SolidColorBrush(
                            IsDarkTheme ? themeColors["AccentColor"] : Color.FromRgb(240, 240, 240)
                        )
                    ),
                    ("TickBarForeground", new SolidColorBrush(themeColors["PrimaryTextColor"])),
                    (
                        "ScrollBarThumbBrush",
                        new SolidColorBrush(themeColors["ScrollBarThumbColor"])
                    ),
                    (
                        "ScrollBarThumbHoverBrush",
                        new SolidColorBrush(themeColors["ScrollBarThumbHoverColor"])
                    ),
                    (
                        "ScrollBarThumbPressedBrush",
                        new SolidColorBrush(themeColors["ScrollBarThumbPressedColor"])
                    ),
                };

                var resources = Application.Current.Resources;

                // Update all brushes in the application resources
                foreach (var (key, brush) in brushes)
                {
                    // Freeze for better performance
                    brush.Freeze();

                    // Update in main resources dictionary
                    resources[key] = brush;
                }

                // Notify the ViewNameToBackgroundConverter that the theme has changed
                Winhance.WPF.Features.Common.Converters.ViewNameToBackgroundConverter.Instance.NotifyThemeChanged();

                // Attempt to force a visual refresh of the main window
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    // Force a layout update
                    mainWindow.InvalidateVisual();

                    // Directly update the navigation buttons and toggle switches
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // Find the navigation buttons by name
                            var softwareAppsButton = FindChildByName(
                                mainWindow,
                                "SoftwareAppsButton"
                            );
                            var optimizeButton = FindChildByName(mainWindow, "OptimizeButton");
                            var customizeButton = FindChildByName(mainWindow, "CustomizeButton");
                            var aboutButton = FindChildByName(mainWindow, "AboutButton");

                            // Get the current view name from the main view model
                            string currentViewName = string.Empty;
                            if (mainWindow.DataContext != null)
                            {
                                var mainViewModel = mainWindow.DataContext as dynamic;
                                currentViewName = mainViewModel.CurrentViewName;
                            }

                            // Update each button's background if not null
                            if (softwareAppsButton != null)
                                UpdateButtonBackground(
                                    softwareAppsButton,
                                    "SoftwareApps",
                                    currentViewName
                                );
                            if (optimizeButton != null)
                                UpdateButtonBackground(optimizeButton, "Optimize", currentViewName);
                            if (customizeButton != null)
                                UpdateButtonBackground(
                                    customizeButton,
                                    "Customize",
                                    currentViewName
                                );
                            if (aboutButton != null)
                                UpdateButtonBackground(aboutButton, "About", currentViewName);

                            // We no longer need to explicitly update toggle switches
                            // as they will automatically pick up the theme from application resources

                            // Force a more thorough refresh of the UI
                            mainWindow.UpdateLayout();

                            // Update theme-dependent icons
                            NotifyWindowsOfThemeChange();
                        }
                        catch (Exception ex)
                        {
                            // Silently handle exceptions to avoid crashes
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Silently handle exceptions to avoid crashes
            }

            // Save theme preference
            SaveThemePreference();
        }

        private void SaveThemePreference()
        {
            try
            {
                Settings.Default.IsDarkTheme = IsDarkTheme;
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                // Silently handle exceptions to avoid crashes
            }
        }

        public void LoadThemePreference()
        {
            try
            {
                IsDarkTheme = Settings.Default.IsDarkTheme;
            }
            catch (Exception ex)
            {
                // Keep default value if loading fails
            }
        }

        // Clean up event subscriptions
        public void Dispose()
        {
            try
            {
                if (_navigationService != null)
                {
                    _navigationService.Navigated -= NavigationService_Navigated;
                }
            }
            catch (Exception ex)
            {
                // Silently handle exceptions to avoid crashes
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
            catch (Exception ex)
            {
                // Silently handle exceptions to avoid crashes
            }
        }

        // Helper method to find a child element by name
        private static System.Windows.Controls.Button? FindChildByName(
            DependencyObject parent,
            string name
        )
        {
            if (parent == null)
                return null;

            // Check if the current element is the one we're looking for
            if (
                parent is FrameworkElement element
                && element.Name == name
                && element is System.Windows.Controls.Button button
            )
            {
                return button;
            }

            // Get the number of children
            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            // Recursively search through all children
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindChildByName(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        // Helper method to update a button's background based on whether it's selected
        private void UpdateButtonBackground(
            System.Windows.Controls.Button button,
            string buttonViewName,
            string currentViewName
        )
        {
            if (button == null)
                return;

            var themeColors = IsDarkTheme ? DarkThemeColors : LightThemeColors;

            // Determine if this button is for the currently selected view
            bool isSelected = string.Equals(
                buttonViewName,
                currentViewName,
                StringComparison.OrdinalIgnoreCase
            );

            // Set the appropriate background color
            if (isSelected)
            {
                button.Background = new SolidColorBrush(themeColors["MainContainerBorderColor"]);
            }
            else
            {
                button.Background = new SolidColorBrush(
                    themeColors["NavigationButtonBackgroundColor"]
                );
            }

            // Update the foreground color for the button's content
            button.Foreground = new SolidColorBrush(themeColors["NavigationButtonForegroundColor"]);
        }

        // Method to update all toggle switches in all open windows
        private void UpdateAllToggleSwitches()
        {
            try
            {
                // Update toggle switches in all open windows
                foreach (Window window in Application.Current.Windows)
                {
                    UpdateToggleSwitches(window);
                }
            }
            catch (Exception ex)
            {
                // Silently handle exceptions to avoid crashes
            }
        }

        // Method to notify windows about theme changes
        private void NotifyWindowsOfThemeChange()
        {
            try
            {
                // Notify all open windows about theme changes
                foreach (Window window in Application.Current.Windows)
                {
                    // Check if the window is a MainWindow or LoadingWindow
                    if (window is MainWindow mainWindow)
                    {
                        // Call the UpdateThemeIcon method using reflection
                        try
                        {
                            var method = mainWindow.GetType().GetMethod("UpdateThemeIcon", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (method != null)
                            {
                                // Use the dispatcher to ensure UI updates happen on the UI thread
                                mainWindow.Dispatcher.Invoke(() =>
                                {
                                    method.Invoke(mainWindow, null);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            // Silently handle exceptions to avoid crashes
                        }
                    }
                    else if (window is LoadingWindow loadingWindow)
                    {
                        // Call the UpdateThemeIcon method using reflection
                        try
                        {
                            var method = loadingWindow.GetType().GetMethod("UpdateThemeIcon", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (method != null)
                            {
                                // Use the dispatcher to ensure UI updates happen on the UI thread
                                loadingWindow.Dispatcher.Invoke(() =>
                                {
                                    method.Invoke(loadingWindow, null);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            // Silently handle exceptions to avoid crashes
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently handle exceptions to avoid crashes
            }
        }

        // Helper method to update all toggle switches in the visual tree
        private void UpdateToggleSwitches(DependencyObject parent)
        {
            if (parent == null)
                return;

            // Check if the current element is a ToggleButton
            if (parent is ToggleButton toggleButton)
            {
                try
                {
                    // Always set the Tag property for all toggle buttons
                    toggleButton.Tag = IsDarkTheme ? "Dark" : "Light";

                    // Force a visual refresh for all toggle buttons
                    toggleButton.InvalidateVisual();

                    // Force a more thorough refresh
                    if (toggleButton.Parent is FrameworkElement parentElement)
                    {
                        parentElement.InvalidateVisual();
                        parentElement.UpdateLayout();
                    }

                    // Ensure the toggle button is enabled and clickable if it should be
                    if (!toggleButton.IsEnabled && toggleButton.IsEnabled != false)
                    {
                        toggleButton.IsEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    // Silently handle exceptions to avoid crashes
                }
            }

            // Get the number of children
            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            // Recursively search through all children
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                UpdateToggleSwitches(child);
            }
        }
    }
}

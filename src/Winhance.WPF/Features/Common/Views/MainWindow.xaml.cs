using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// MainWindow - responsible ONLY for view initialization
    /// All business logic is handled by MainViewModel and services
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Only essential UI event handlers remain in the View
            this.PreviewMouseWheel += MainWindow_PreviewMouseWheel;
            
            // Initialize the app icon
            UpdateThemeIcon();
        }

        /// <summary>
        /// Essential UI helper method - finds visual children in the tree
        /// This is pure UI functionality and can remain in the View
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject obj)
            where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                if (child != null && child is T)
                {
                    return (T)child;
                }

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }

            return null;
        }

        /// <summary>
        /// Essential UI event handler - handles mouse wheel scrolling
        /// This is pure UI behavior and can remain in the View
        /// </summary>
        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(this);
            if (scrollViewer != null)
            {
                // Redirect the mouse wheel event to the ScrollViewer
                if (e.Delta < 0)
                {
                    scrollViewer.LineDown();
                }
                else
                {
                    scrollViewer.LineUp();
                }

                // Mark the event as handled to prevent it from bubbling up
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles clicking outside the flyout menu to close it
        /// This is pure UI behavior and can remain in the View
        /// </summary>
        private void MoreMenuOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Close the flyout when clicking on the overlay (outside the menu)
            if (DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.CloseMoreMenuFlyout();
            }
        }

        /// <summary>
        /// Handles keyboard input on the flyout overlay (Escape to close)
        /// This is pure UI behavior and can remain in the View
        /// </summary>
        private void MoreMenuOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            // Close the flyout when pressing Escape
            if (e.Key == Key.Escape && DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.CloseMoreMenuFlyout();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Updates the window and image icons based on the current theme
        /// This method is called by the ThemeManager via reflection
        /// </summary>
        private void UpdateThemeIcon()
        {
            try
            {
                // Get theme information from application resources
                bool isDarkTheme = Application.Current.Resources.Contains("IsDarkTheme") &&
                                 Application.Current.Resources["IsDarkTheme"] is bool darkTheme && darkTheme;

                // Get the appropriate icon based on the theme
                string iconPath = isDarkTheme
                    ? "pack://application:,,,/Resources/AppIcons/winhance-rocket-white-transparent-bg.ico"
                    : "pack://application:,,,/Resources/AppIcons/winhance-rocket-black-transparent-bg.ico";

                // Create a BitmapImage from the icon path
                var iconImage = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                iconImage.Freeze(); // Freeze for better performance and thread safety

                // Set the window icon
                this.Icon = iconImage;

                // Set the image control source
                if (AppIconImage != null)
                {
                    AppIconImage.Source = iconImage;
                }
            }
            catch (Exception ex)
            {
                // If there's an error, fall back to the default icon
                try
                {
                    var defaultIcon = new BitmapImage(new Uri("pack://application:,,,/Resources/AppIcons/winhance-rocket.ico", UriKind.Absolute));
                    this.Icon = defaultIcon;
                    if (AppIconImage != null)
                    {
                        AppIconImage.Source = defaultIcon;
                    }
                }
                catch
                {
                    // Ignore any errors with the fallback icon
                }
            }
        }
    }
}

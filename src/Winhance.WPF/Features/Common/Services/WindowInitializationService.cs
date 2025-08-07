using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.WPF.Features.Common.Controls;
using Winhance.WPF.Features.Common.Utilities;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service responsible for initializing window-specific functionality
    /// This bridges the gap between MVVM and window-specific operations
    /// </summary>
    public class WindowInitializationService
    {
        private readonly IEventBus _eventBus;
        private readonly WindowEffectsService _windowEffectsService;
        private readonly UserPreferencesService _userPreferencesService;
        private readonly ILogService _logService;

        public WindowInitializationService(
            IEventBus eventBus,
            UserPreferencesService userPreferencesService,
            ILogService logService
        )
        {
            _eventBus =
                eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _userPreferencesService =
                userPreferencesService
                ?? throw new ArgumentNullException(nameof(userPreferencesService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _windowEffectsService = new WindowEffectsService();
        }

        /// <summary>
        /// Initializes a window with all necessary effects and messaging
        /// </summary>
        /// <param name="window">The window to initialize</param>
        public void InitializeWindow(Window window)
        {
            if (window == null)
                return;

            try
            {
                // Set up window size management
                WindowSizeManager windowSizeManager = null;
                if (_userPreferencesService != null && _logService != null)
                {
                    windowSizeManager = new WindowSizeManager(
                        window,
                        _userPreferencesService,
                        _logService
                    );
                }

                // Set up window effects and messaging when loaded
                window.Loaded += (sender, e) =>
                {
                    try
                    {
                        // Apply window effects
                        _windowEffectsService.EnableBlur(window);

                        if (windowSizeManager == null)
                        {
                            _windowEffectsService.SetDynamicWindowSize(window);
                        }
                        else
                        {
                            windowSizeManager.Initialize();
                        }

                        // Update theme icon
                        UpdateWindowIcon(window);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't crash the application
                        _logService?.LogError("Error during window initialization", ex);
                    }
                };

                // Set up event handling for window state changes
                _eventBus.Subscribe<WindowStateEvent>(
                    evt => HandleWindowStateEvent(window, evt)
                );
                _eventBus.Subscribe<UpdateThemeIconEvent>(
                    _ => UpdateWindowIcon(window)
                );

                // Window closed event handling
                window.Closed += (sender, e) =>
                {
                    // No need to unregister from event bus as it's handled by subscription tokens
                };

                // Handle window state changes for ViewModel updates
                window.StateChanged += (sender, e) =>
                {
                    if (window.DataContext is MainViewModel viewModel)
                    {
                        viewModel.HandleWindowStateChanged(window.WindowState);
                    }
                };
            }
            catch (Exception ex)
            {
                _logService?.LogError("Error setting up window initialization", ex);
            }
        }

        private void HandleWindowStateEvent(Window window, WindowStateEvent evt)
        {
            try
            {
                switch (evt.WindowState)
                {
                    case Core.Features.Common.Enums.WindowState.Minimized:
                        window.WindowState = WindowState.Minimized;
                        break;
                    case Core.Features.Common.Enums.WindowState.Maximized:
                        window.WindowState = WindowState.Maximized;
                        break;
                    case Core.Features.Common.Enums.WindowState.Normal:
                        window.WindowState = WindowState.Normal;
                        break;
                    case Core.Features.Common.Enums.WindowState.Closed:
                        window.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError("Error handling window state event", ex);
            }
        }

        private void UpdateWindowIcon(Window window)
        {
            try
            {
                if (window.DataContext is not MainViewModel viewModel)
                {
                    return;
                }

                string iconPath = viewModel.GetThemeIconPath();
                string defaultIconPath = viewModel.GetDefaultIconPath();

                try
                {
                    var icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                    window.Icon = icon;
                }
                catch
                {
                    try
                    {
                        var defaultIcon = new BitmapImage(
                            new Uri(defaultIconPath, UriKind.Absolute)
                        );
                        window.Icon = defaultIcon;
                    }
                    catch
                    {
                        // Silently fail if both icons can't be loaded
                    }
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError("Error updating window icon", ex);
            }
        }

        /// <summary>
        /// Helper method to find visual children in the visual tree
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
        /// Helper method to find all visual children of a specific type in the visual tree
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject obj)
            where T : DependencyObject
        {
            var children = new List<T>();

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                {
                    children.Add((T)child);
                }

                children.AddRange(FindVisualChildren<T>(child));
            }

            return children;
        }
    }
}

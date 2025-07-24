using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Messaging;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Utilities;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service responsible for initializing window-specific functionality
    /// This bridges the gap between MVVM and window-specific operations
    /// </summary>
    public class WindowInitializationService
    {
        private readonly IMessengerService _messengerService;
        private readonly WindowEffectsService _windowEffectsService;
        private readonly UserPreferencesService _userPreferencesService;
        private readonly ILogService _logService;

        public WindowInitializationService(
            IMessengerService messengerService,
            UserPreferencesService userPreferencesService,
            ILogService logService)
        {
            _messengerService = messengerService ?? throw new ArgumentNullException(nameof(messengerService));
            _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _windowEffectsService = new WindowEffectsService();
        }

        /// <summary>
        /// Initializes a window with all necessary effects and messaging
        /// </summary>
        /// <param name="window">The window to initialize</param>
        public void InitializeWindow(Window window)
        {
            if (window == null) return;

            try
            {
                // Set up window size management
                WindowSizeManager windowSizeManager = null;
                if (_userPreferencesService != null && _logService != null)
                {
                    windowSizeManager = new WindowSizeManager(window, _userPreferencesService, _logService);
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

                // Set up messaging for window state changes
                _messengerService.Register<WindowStateMessage>(window, (msg) => HandleWindowStateMessage(window, msg));
                _messengerService.Register<UpdateThemeIconMessage>(window, (msg) => UpdateWindowIcon(window));

                // Clean up messaging when window closes
                window.Closed += (sender, e) =>
                {
                    _messengerService.Unregister(window);
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

        private void HandleWindowStateMessage(Window window, WindowStateMessage message)
        {
            try
            {
                switch (message.Action)
                {
                    case WindowStateMessage.WindowStateAction.Minimize:
                        window.WindowState = WindowState.Minimized;
                        break;
                    case WindowStateMessage.WindowStateAction.Maximize:
                        window.WindowState = WindowState.Maximized;
                        break;
                    case WindowStateMessage.WindowStateAction.Restore:
                        window.WindowState = WindowState.Normal;
                        break;
                    case WindowStateMessage.WindowStateAction.Close:
                        window.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError("Error handling window state message", ex);
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
                        var defaultIcon = new BitmapImage(new Uri(defaultIconPath, UriKind.Absolute));
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
    }
}

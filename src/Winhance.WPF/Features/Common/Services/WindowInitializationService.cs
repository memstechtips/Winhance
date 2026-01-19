using System;
using System.Windows;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Utilities;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Services
{
    public class WindowInitializationService
    {
        private readonly IEventBus _eventBus;
        private readonly WindowEffectsService _windowEffectsService;
        private readonly UserPreferencesService _userPreferencesService;
        private readonly ILogService _logService;
        private readonly IApplicationCloseService _applicationCloseService;

        public WindowInitializationService(
            IEventBus eventBus,
            UserPreferencesService userPreferencesService,
            ILogService logService,
            IApplicationCloseService applicationCloseService
        )
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _applicationCloseService = applicationCloseService ?? throw new ArgumentNullException(nameof(applicationCloseService));
            _windowEffectsService = new WindowEffectsService();
        }

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

                    // Register shutdown hook to save window state
                    _applicationCloseService.BeforeShutdown = async () => await windowSizeManager.SaveWindowSettingsAsync();
                }

                // Set up window effects and messaging when loaded
                window.Loaded += async (sender, e) =>
                {
                    try
                    {
                        _windowEffectsService.EnableBlur(window);

                        if (windowSizeManager == null)
                            _windowEffectsService.SetDynamicWindowSize(window);
                        else
                            await windowSizeManager.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logService?.LogError("Error during window initialization", ex);
                    }
                };

                _eventBus.Subscribe<WindowStateEvent>(evt => HandleWindowStateEvent(window, evt));

                window.StateChanged += (sender, e) =>
                {
                    if (window.DataContext is MainViewModel viewModel)
                        viewModel.HandleWindowStateChanged(window.WindowState);
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
    }
}

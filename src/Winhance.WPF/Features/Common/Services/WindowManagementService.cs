using System;
using System.Threading.Tasks;
using System.Windows;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Resources.Theme;

namespace Winhance.WPF.Features.Common.Services
{
    public class WindowManagementService(
        ILogService logService,
        IEventBus eventBus,
        IThemeManager themeManager,
        IApplicationCloseService applicationCloseService) : IWindowManagementService
    {
        public void MinimizeWindow()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null)
                {
                    logService?.LogWarning("Cannot minimize window: MainWindow is null");
                    return;
                }

                mainWindow.WindowState = System.Windows.WindowState.Minimized;
                logService?.LogInformation("Window minimized successfully");
            }
            catch (Exception ex)
            {
                logService?.LogError($"Failed to minimize window directly: {ex.Message}", ex);
                try
                {
                    eventBus.Publish(new WindowStateEvent(Core.Features.Common.Enums.WindowState.Minimized));
                    logService?.LogInformation("Published minimize window event as fallback");
                }
                catch (Exception msgEx)
                {
                    logService?.LogError($"Failed to send minimize window message: {msgEx.Message}", msgEx);
                }
            }
        }

        public void MaximizeRestoreWindow()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null)
                {
                    logService?.LogWarning("Cannot maximize/restore window: MainWindow is null");
                    return;
                }

                if (mainWindow.WindowState == System.Windows.WindowState.Maximized)
                {
                    try
                    {
                        mainWindow.WindowState = System.Windows.WindowState.Normal;
                        logService?.LogInformation("Window restored successfully");
                    }
                    catch (Exception ex)
                    {
                        logService?.LogError($"Failed to restore window directly: {ex.Message}", ex);
                        eventBus.Publish(new WindowStateEvent(Core.Features.Common.Enums.WindowState.Normal));
                        logService?.LogInformation("Published restore window event as fallback");
                    }
                }
                else
                {
                    try
                    {
                        mainWindow.WindowState = System.Windows.WindowState.Maximized;
                        logService?.LogInformation("Window maximized successfully");
                    }
                    catch (Exception ex)
                    {
                        logService?.LogError($"Failed to maximize window directly: {ex.Message}", ex);
                        eventBus.Publish(new WindowStateEvent(Core.Features.Common.Enums.WindowState.Maximized));
                        logService?.LogInformation("Published maximize window event as fallback");
                    }
                }
            }
            catch (Exception ex)
            {
                logService?.LogError($"Unexpected error in MaximizeRestoreWindow: {ex.Message}", ex);
            }
        }

        public async Task CloseWindowAsync()
        {
            try
            {
                logService?.LogInformation("Close window command executed");
                await applicationCloseService.CheckOperationsAndCloseAsync();
                logService?.LogInformation("Application close service completed");
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error in CloseWindowAsync: {ex.Message}", ex);

                try
                {
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.Close();
                        logService?.LogInformation("Fallback: Window closed directly");
                    }
                    else
                    {
                        eventBus.Publish(new WindowStateEvent(Core.Features.Common.Enums.WindowState.Closed));
                        logService?.LogInformation("Fallback: Published close window event");
                    }
                }
                catch (Exception fallbackEx)
                {
                    logService?.LogError($"Failed in fallback close: {fallbackEx.Message}", fallbackEx);
                    try
                    {
                        Application.Current.Shutdown();
                    }
                    catch (Exception shutdownEx)
                    {
                        logService?.LogError($"Failed to shutdown application: {shutdownEx.Message}", shutdownEx);
                    }
                }
            }
        }

        public void HandleWindowStateChanged(Core.Features.Common.Enums.WindowState windowState)
        {
            // Convert domain WindowState to WPF if needed for any logic
            // Currently no additional logic needed
        }

        public string GetThemeIconPath()
        {
            return themeManager.IsDarkTheme
                ? "pack://application:,,,/Resources/AppIcons/winhance-rocket-white-transparent-bg.ico"
                : "pack://application:,,,/Resources/AppIcons/winhance-rocket-black-transparent-bg.ico";
        }

        public string GetDefaultIconPath()
        {
            return "pack://application:,,,/Resources/AppIcons/winhance-rocket.ico";
        }

        public void RequestThemeIconUpdate()
        {
            eventBus.Publish(new UpdateThemeIconEvent());
        }

        public void ToggleTheme()
        {
            themeManager.ToggleTheme();
        }

        public bool IsDarkTheme => themeManager.IsDarkTheme;
    }
}

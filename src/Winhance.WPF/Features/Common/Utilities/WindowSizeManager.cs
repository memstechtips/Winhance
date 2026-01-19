using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.Utilities;

namespace Winhance.WPF.Features.Common.Utilities
{
    /// <summary>
    /// Manages window size and position, including dynamic sizing based on screen resolution
    /// and handling multiple monitors and DPI scaling.
    /// </summary>
    public class WindowSizeManager
    {
        private readonly Window _window;
        private readonly ILogService _logService;

        // Default window dimensions
        private const double DEFAULT_WIDTH = 1600;
        private const double DEFAULT_HEIGHT = 900;
        private const double MIN_WIDTH = 800;
        private const double MIN_HEIGHT = 600; // Reduced minimum height to fit better on smaller screens
        private const double SCREEN_PERCENTAGE = 0.80; // Use 80% of screen size for better fit

        public WindowSizeManager(Window window, UserPreferencesService userPreferencesService, ILogService logService)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        private readonly UserPreferencesService _userPreferencesService;

        /// <summary>
        /// Initializes the window size and position, restoring from preferences if available
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Try to load saved settings
                bool loaded = await LoadWindowSettingsAsync();

                if (!loaded)
                {
                    // Fallback to dynamic defaults if no settings found
                    SetDynamicWindowSize();
                    CenterWindowOnScreen();
                }
            }
            catch (Exception ex)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Error initializing window size: {ex.Message}");
                // Fallback on error
                SetDynamicWindowSize();
                CenterWindowOnScreen();
            }
        }

        private async Task<bool> LoadWindowSettingsAsync()
        {
            try
            {
                var width = await _userPreferencesService.GetPreferenceAsync<double>("WindowWidth", 0);
                var height = await _userPreferencesService.GetPreferenceAsync<double>("WindowHeight", 0);
                var left = await _userPreferencesService.GetPreferenceAsync<double>("WindowLeft", double.NaN);
                var top = await _userPreferencesService.GetPreferenceAsync<double>("WindowTop", double.NaN);
                var isMaximized = await _userPreferencesService.GetPreferenceAsync<bool>("WindowMaximized", false);

                // Validation: Ensure size is reasonable
                if (width < MIN_WIDTH || height < MIN_HEIGHT)
                    return false;

                _window.Width = width;
                _window.Height = height;

                // Restore position if valid
                if (!double.IsNaN(left) && !double.IsNaN(top))
                {
                    // Basic off-screen check could be added here, but WPF handles some of this
                    _window.Left = left;
                    _window.Top = top;
                }
                else
                {
                    CenterWindowOnScreen();
                }

                // Restore state (must be done last)
                if (isMaximized)
                {
                    _window.WindowState = System.Windows.WindowState.Maximized;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Failed to load window settings: {ex.Message}");
                return false;
            }
        }

        public async Task SaveWindowSettingsAsync()
        {
            try
            {
                var prefs = await _userPreferencesService.GetPreferencesAsync();

                if (_window.WindowState == System.Windows.WindowState.Maximized)
                {
                    prefs["WindowMaximized"] = true;
                    // Save RestoreBounds to remember the "normal" size/position before maximizing
                    prefs["WindowWidth"] = _window.RestoreBounds.Width;
                    prefs["WindowHeight"] = _window.RestoreBounds.Height;
                    prefs["WindowLeft"] = _window.RestoreBounds.Left;
                    prefs["WindowTop"] = _window.RestoreBounds.Top;
                }
                else if (_window.WindowState == System.Windows.WindowState.Normal)
                {
                    prefs["WindowMaximized"] = false;
                    prefs["WindowWidth"] = _window.Width;
                    prefs["WindowHeight"] = _window.Height;
                    prefs["WindowLeft"] = _window.Left;
                    prefs["WindowTop"] = _window.Top;
                }
                // If minimized, we ideally don't overwrite with "Minimized" state or bad bounds, 
                // but usually RestoreBounds works there too. For safety, we skip saving if minimized 
                // to avoid saving a tiny window or off-screen coordinates accidentally.

                await _userPreferencesService.SavePreferencesAsync(prefs);
            }
            catch (Exception ex)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Failed to save window settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Centers the window on the current screen
        /// </summary>
        private void CenterWindowOnScreen()
        {
            try
            {
                // Get the current screen's working area
                var workArea = GetCurrentScreenWorkArea();

                // Get DPI scaling factor
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;

                try
                {
                    var presentationSource = PresentationSource.FromVisual(_window);
                    if (presentationSource?.CompositionTarget != null)
                    {
                        dpiScaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                        dpiScaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
                    }
                }
                catch (Exception ex)
                {
                    // Error getting DPI scale
                }

                // Convert screen coordinates to account for DPI
                double screenWidth = workArea.Width / dpiScaleX;
                double screenHeight = workArea.Height / dpiScaleY;
                double screenLeft = workArea.X / dpiScaleX;
                double screenTop = workArea.Y / dpiScaleY;

                // Calculate center position
                double left = screenLeft + (screenWidth - _window.Width) / 2;
                double top = screenTop + (screenHeight - _window.Height) / 2;

                // Set window position
                _window.Left = left;
                _window.Top = top;

            }
            catch (Exception ex)
            {
                // Error centering window
            }
        }

        /// <summary>
        /// Sets the window size dynamically based on the screen resolution
        /// </summary>
        private void SetDynamicWindowSize()
        {
            try
            {
                // Get the current screen's working area (excludes taskbar)
                var workArea = GetCurrentScreenWorkArea();

                // Get DPI scaling factor for the current screen
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;

                try
                {
                    var presentationSource = PresentationSource.FromVisual(_window);
                    if (presentationSource?.CompositionTarget != null)
                    {
                        dpiScaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                        dpiScaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
                    }
                }
                catch (Exception ex)
                {
                    // Error getting DPI scale
                }

                // Calculate available screen space
                double screenWidth = workArea.Width / dpiScaleX;
                double screenHeight = workArea.Height / dpiScaleY;

                // Calculate window size (80% of screen size, using min/max constraints only where logical)
                double windowWidth = screenWidth * SCREEN_PERCENTAGE;
                double windowHeight = screenHeight * SCREEN_PERCENTAGE;

                // Ensure minimum size for usability
                windowWidth = Math.Max(windowWidth, MIN_WIDTH);
                windowHeight = Math.Max(windowHeight, MIN_HEIGHT);

                // Only set the window size, let WPF handle the centering via WindowStartupLocation="CenterScreen"
                _window.Width = windowWidth;
                _window.Height = windowHeight;

            }
            catch (Exception ex)
            {
                // Error setting dynamic window size
            }
        }

        /// <summary>
        /// Gets the working area of the screen that contains the window
        /// </summary>
        private Rect GetCurrentScreenWorkArea()
        {
            try
            {
                // Get the window handle
                var windowHandle = new WindowInteropHelper(_window).Handle;
                if (windowHandle != IntPtr.Zero)
                {
                    // Get the monitor info for the monitor containing the window
                    var monitorInfo = new MONITORINFO();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                    if (GetMonitorInfo(MonitorFromWindow(windowHandle, MONITOR_DEFAULTTONEAREST), ref monitorInfo))
                    {
                        // Convert the working area to a WPF Rect
                        return new Rect(
                            monitorInfo.rcWork.left,
                            monitorInfo.rcWork.top,
                            monitorInfo.rcWork.right - monitorInfo.rcWork.left,
                            monitorInfo.rcWork.bottom - monitorInfo.rcWork.top);
                    }
                }
            }
            catch (Exception ex)
            {
                // Error getting current screen
            }

            // Fallback to primary screen working area
            return SystemParameters.WorkArea;
        }

        #region Win32 API

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        #endregion
    }
}
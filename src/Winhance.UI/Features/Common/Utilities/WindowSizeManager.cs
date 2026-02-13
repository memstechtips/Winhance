using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Windows.Graphics;

namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Manages window size and position persistence for WinUI 3.
/// Saves/restores window bounds and maximized state via user preferences.
/// </summary>
public class WindowSizeManager
{
    private readonly AppWindow _appWindow;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ILogService _logService;

    private const int MinWidth = 800;
    private const int MinHeight = 600;
    private const double ScreenWidthPercentage = 0.70;
    private const double ScreenHeightPercentage = 0.80;

    // Tracked "normal" bounds (WinUI 3 has no RestoreBounds equivalent)
    private int _normalX;
    private int _normalY;
    private int _normalWidth;
    private int _normalHeight;
    private bool _isTrackingBounds;

    public WindowSizeManager(AppWindow appWindow, IUserPreferencesService userPreferencesService, ILogService logService)
    {
        _appWindow = appWindow ?? throw new ArgumentNullException(nameof(appWindow));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    /// <summary>
    /// Initializes window size and position, restoring from preferences if available.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            bool loaded = await LoadWindowSettingsAsync();

            if (!loaded)
            {
                SetDynamicWindowSize();
                CenterOnScreen();
            }

            // Start tracking normal bounds after initial positioning
            RecordNormalBounds();
            SubscribeToWindowChanges();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error initializing window size: {ex.Message}");
            SetDynamicWindowSize();
            CenterOnScreen();
            RecordNormalBounds();
            SubscribeToWindowChanges();
        }
    }

    /// <summary>
    /// Saves current window settings to preferences.
    /// Called by ApplicationCloseService.BeforeShutdown which properly awaits before exiting.
    /// </summary>
    public async Task SaveWindowSettingsAsync()
    {
        try
        {
            var presenter = _appWindow.Presenter as OverlappedPresenter;
            var state = presenter?.State ?? OverlappedPresenterState.Restored;

            // Skip saving if minimized to avoid bad coordinates
            if (state == OverlappedPresenterState.Minimized)
                return;

            var prefs = await _userPreferencesService.GetPreferencesAsync();

            if (state == OverlappedPresenterState.Maximized)
            {
                prefs[UserPreferenceKeys.WindowMaximized] = true;
                // Save tracked normal bounds (pre-maximize size/position)
                prefs[UserPreferenceKeys.WindowWidth] = (double)_normalWidth;
                prefs[UserPreferenceKeys.WindowHeight] = (double)_normalHeight;
                prefs[UserPreferenceKeys.WindowLeft] = (double)_normalX;
                prefs[UserPreferenceKeys.WindowTop] = (double)_normalY;
            }
            else
            {
                prefs[UserPreferenceKeys.WindowMaximized] = false;
                prefs[UserPreferenceKeys.WindowWidth] = (double)_appWindow.Size.Width;
                prefs[UserPreferenceKeys.WindowHeight] = (double)_appWindow.Size.Height;
                prefs[UserPreferenceKeys.WindowLeft] = (double)_appWindow.Position.X;
                prefs[UserPreferenceKeys.WindowTop] = (double)_appWindow.Position.Y;
            }

            await _userPreferencesService.SavePreferencesAsync(prefs);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to save window settings: {ex.Message}");
        }
    }

    private async Task<bool> LoadWindowSettingsAsync()
    {
        try
        {
            var width = await _userPreferencesService.GetPreferenceAsync<double>(UserPreferenceKeys.WindowWidth, 0);
            var height = await _userPreferencesService.GetPreferenceAsync<double>(UserPreferenceKeys.WindowHeight, 0);
            var left = await _userPreferencesService.GetPreferenceAsync<double>(UserPreferenceKeys.WindowLeft, double.NaN);
            var top = await _userPreferencesService.GetPreferenceAsync<double>(UserPreferenceKeys.WindowTop, double.NaN);
            var isMaximized = await _userPreferencesService.GetPreferenceAsync<bool>(UserPreferenceKeys.WindowMaximized, false);

            // Only reject clearly invalid values (no saved prefs yet)
            if (width <= 0 || height <= 0)
                return false;

            int w = (int)width;
            int h = (int)height;

            // Restore position if valid
            if (!double.IsNaN(left) && !double.IsNaN(top))
            {
                _appWindow.MoveAndResize(new RectInt32((int)left, (int)top, w, h));
            }
            else
            {
                _appWindow.Resize(new SizeInt32(w, h));
                CenterOnScreen();
            }

            // Restore maximized state (must be done after position/size)
            if (isMaximized)
            {
                var presenter = _appWindow.Presenter as OverlappedPresenter;
                presenter?.Maximize();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to load window settings: {ex.Message}");
            return false;
        }
    }

    private void SetDynamicWindowSize()
    {
        try
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;

            int windowWidth = (int)(workArea.Width * ScreenWidthPercentage);
            int windowHeight = (int)(workArea.Height * ScreenHeightPercentage);

            windowWidth = Math.Max(windowWidth, MinWidth);
            windowHeight = Math.Max(windowHeight, MinHeight);

            _appWindow.Resize(new SizeInt32(windowWidth, windowHeight));
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error setting dynamic window size: {ex.Message}");
            _appWindow.Resize(new SizeInt32(1280, 800));
        }
    }

    private void CenterOnScreen()
    {
        try
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;

            int x = workArea.X + (workArea.Width - _appWindow.Size.Width) / 2;
            int y = workArea.Y + (workArea.Height - _appWindow.Size.Height) / 2;

            _appWindow.Move(new PointInt32(x, y));
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error centering window: {ex.Message}");
        }
    }

    private void RecordNormalBounds()
    {
        _normalX = _appWindow.Position.X;
        _normalY = _appWindow.Position.Y;
        _normalWidth = _appWindow.Size.Width;
        _normalHeight = _appWindow.Size.Height;
        _isTrackingBounds = true;
    }

    private void SubscribeToWindowChanges()
    {
        _appWindow.Changed += AppWindow_Changed;
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!_isTrackingBounds)
            return;

        // Only record bounds when the window is in normal (restored) state
        if (args.DidPositionChange || args.DidSizeChange)
        {
            var presenter = sender.Presenter as OverlappedPresenter;
            if (presenter?.State == OverlappedPresenterState.Restored)
            {
                _normalX = sender.Position.X;
                _normalY = sender.Position.Y;
                _normalWidth = sender.Size.Width;
                _normalHeight = sender.Size.Height;
            }
        }
    }
}

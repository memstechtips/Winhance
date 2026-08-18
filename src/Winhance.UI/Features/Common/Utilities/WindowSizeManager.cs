using Microsoft.UI.Windowing;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Windows.Graphics;

namespace Winhance.UI.Features.Common.Utilities;

public class WindowSizeManager
{
    private readonly AppWindow _appWindow;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ILogService _logService;

    private const int MinWidth = 800;
    private const int MinHeight = 600;
    private const double ScreenWidthPercentage = 0.70;
    private const double ScreenHeightPercentage = 0.80;

    // Minimum on-screen overlap required for a restored window position to be
    // considered usable. Smaller than a normal title bar so partially-dragged
    // windows still restore in place, large enough that a stale off-screen
    // position (e.g. saved on a since-disconnected secondary monitor) fails.
    internal const int MinVisibleWidth = 120;
    internal const int MinVisibleHeight = 40;

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

            // Restore position if valid and still visible on an active monitor.
            // If the saved rect is off-screen (e.g. secondary monitor was disconnected
            // since last close), fall back to centering on the primary display.
            if (!double.IsNaN(left) && !double.IsNaN(top))
            {
                var savedRect = new RectInt32((int)left, (int)top, w, h);

                if (IsSavedPositionVisibleOnAnyDisplay(savedRect))
                {
                    _appWindow.MoveAndResize(savedRect);
                }
                else
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"Saved window position ({savedRect.X}, {savedRect.Y}) is not visible on any active display; re-centering on primary.");
                    _appWindow.Resize(new SizeInt32(w, h));
                    CenterOnScreen();
                }
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
            _logService.Log(LogLevel.Error, $"Failed to load window settings ({ex.GetType().FullName}): {ex.Message}");
            if (ex.InnerException != null)
                _logService.Log(LogLevel.Error, $"Inner exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
            _logService.Log(LogLevel.Error, $"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    // Enumerates displays and runs the visibility check. Isolated so a failure
    // inside the WinUI DisplayArea APIs (which can throw in obscure COM-interop
    // scenarios during early startup) can't take down the whole load path —
    // if we can't enumerate displays, assume the saved position might be stale
    // and recenter, which matches the safe behavior for issue #585.
    private bool IsSavedPositionVisibleOnAnyDisplay(RectInt32 savedRect)
    {
        try
        {
            var displays = DisplayArea.FindAll();
            var workAreas = new List<RectInt32>(displays.Count);
            for (int i = 0; i < displays.Count; i++)
            {
                workAreas.Add(displays[i].WorkArea);
            }

            return IsWindowRectVisible(savedRect, workAreas);
        }
        catch (Exception ex)
        {
            _logService.Log(
                LogLevel.Warning,
                $"Could not enumerate displays to validate window position ({ex.GetType().FullName}: {ex.Message}); treating saved position as invalid and re-centering.");
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

    internal static bool IsWindowRectVisible(RectInt32 windowRect, IEnumerable<RectInt32> workAreas)
    {
        if (workAreas is null)
            return false;

        foreach (var work in workAreas)
        {
            int overlapLeft = Math.Max(windowRect.X, work.X);
            int overlapTop = Math.Max(windowRect.Y, work.Y);
            int overlapRight = Math.Min(windowRect.X + windowRect.Width, work.X + work.Width);
            int overlapBottom = Math.Min(windowRect.Y + windowRect.Height, work.Y + work.Height);

            int overlapW = overlapRight - overlapLeft;
            int overlapH = overlapBottom - overlapTop;

            if (overlapW >= MinVisibleWidth && overlapH >= MinVisibleHeight)
                return true;
        }

        return false;
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

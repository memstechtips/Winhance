using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Services;
using WinRT.Interop;

namespace Winhance.UI.Features.Common.Services;

// Puts Winhance in the notification area. Closing or minimizing hides the window instead when the user chose that,
// and the icon brings it back. The icon shows only while the window is hidden or a notification waits for a click.
public sealed class TrayController(
    ISystemTrayIcon trayIcon,
    IUserPreferencesService preferences,
    ILocalizationService localization,
    IApplicationCloseService closeService,
    AppUpdateMonitor updateMonitor,
    ILogService logService) : IDisposable
{
    internal const string StartInTrayFlag = "--tray";

    private const string Tooltip = "Winhance";
    private const int OpenItem = 0;
    private const int CheckItem = 1;
    private const int ExitItem = 2;

    private Window? _window;
    private Action? _showUpdates;
    private AppUpdateNoticeKind? _pendingNotice;
    private bool _exiting;

    public void Attach(Window window, Action showUpdates)
    {
        _window = window;
        _showUpdates = showUpdates;
        trayIcon.Attach(WindowNative.GetWindowHandle(window));
        trayIcon.Invoked += OnTrayIconInvoked;
        trayIcon.NotificationClicked += OnNotificationClicked;
        trayIcon.MenuItemInvoked += OnMenuItemInvoked;
        window.AppWindow.Changed += OnAppWindowChanged;
        updateMonitor.NoticeRaised += OnNoticeRaised;
        localization.LanguageChanged += OnLanguageChanged;
        RefreshMenu();
        updateMonitor.Start();
    }

    // The window's Closing handler asks first; true means the window went to the tray instead of closing.
    public bool TryHideOnClose()
    {
        if (_exiting || !preferences.GetPreference(UserPreferenceKeys.CloseToTray, false))
            return false;

        HideToTray();
        return true;
    }

    public void HideToTray()
    {
        if (_window is null)
            return;

        _window.AppWindow.Hide();
        trayIcon.Show(Tooltip);
        if (!preferences.GetPreference(UserPreferenceKeys.TrayHintShown, false))
        {
            preferences.SetPreferenceAsync(UserPreferenceKeys.TrayHintShown, true).FireAndForget(logService);
            trayIcon.ShowNotification(localization.GetString("Tray_StillRunning_Title"), localization.GetString("Tray_StillRunning_Message"));
        }
    }

    public void ShowWindow()
    {
        if (_window is null)
            return;

        if (_window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        _window.AppWindow.Show();
        _window.Activate();
        trayIcon.Hide();
        _pendingNotice = null;
    }

    public void RemoveIcon() => trayIcon.Hide();

    public void Dispose()
    {
        updateMonitor.NoticeRaised -= OnNoticeRaised;
        localization.LanguageChanged -= OnLanguageChanged;
        if (_window is not null)
            _window.AppWindow.Changed -= OnAppWindowChanged;
        trayIcon.Dispose();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (sender.IsVisible
            && sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized }
            && preferences.GetPreference(UserPreferenceKeys.MinimizeToTray, false))
            HideToTray();
    }

    private void OnTrayIconInvoked(object? sender, EventArgs e) => OpenFromTray();

    private void OnNotificationClicked(object? sender, EventArgs e) => OpenFromTray();

    // A pending update notice opens straight onto the apps it is about.
    private void OpenFromTray()
    {
        var notice = _pendingNotice;
        ShowWindow();
        if (notice is AppUpdateNoticeKind.Available or AppUpdateNoticeKind.Installed)
            _showUpdates?.Invoke();
    }

    private void OnMenuItemInvoked(object? sender, int item)
    {
        switch (item)
        {
            case OpenItem:
                OpenFromTray();
                break;
            case CheckItem:
                updateMonitor.CheckAsync(manual: true).FireAndForget(logService);
                break;
            case ExitItem:
                ExitAsync().FireAndForget(logService);
                break;
        }
    }

    private async Task ExitAsync()
    {
        _exiting = true;
        ShowWindow();
        var result = await closeService.CheckOperationsAndCloseAsync();
        if (!result.Success)
            _exiting = false;
    }

    private void OnNoticeRaised(object? sender, AppUpdateNotice notice)
    {
        _pendingNotice = notice.Kind;
        trayIcon.ShowNotification(notice.Title, notice.Message);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshMenu();

    private void RefreshMenu() => trayIcon.SetMenu(
    [
        localization.GetString("Tray_Open"),
        localization.GetString("Tray_CheckForUpdates"),
        localization.GetString("Tray_Exit"),
    ]);
}

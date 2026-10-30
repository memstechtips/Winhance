using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.SoftwareApps.Services;
using Winhance.UI.Helpers;
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
    IProfileService profiles,
    ICoverageSummaryService coverageSummary,
    ILogService logService) : IDisposable
{
    internal const string StartInTrayFlag = "--tray";
    internal const string ReapplyProfileFlag = "--reapply-profile";

    private const string Tooltip = "Winhance";
    private const int OpenItem = 0;
    private const int CheckItem = 1;
    private const int ExitItem = 2;
    private const int FirstProfileItem = 100;

    private Window? _window;
    private Action? _showUpdates;
    private AppUpdateNoticeKind? _pendingNotice;
    private bool _exiting;
    private IReadOnlyList<SettingsProfile> _menuProfiles = [];

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
        profiles.Changed += OnProfilesChanged;
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
        RefreshMenu();
        trayIcon.Show(Tooltip);
        if (!preferences.GetPreference(UserPreferenceKeys.TrayHintShown, false))
        {
            preferences.SetPreferenceAsync(UserPreferenceKeys.TrayHintShown, true).FireAndForget(logService);
            trayIcon.ShowNotification(localization.GetString("Tray_StillRunning_Title"), localization.GetString("Tray_StillRunning_Message"));
        }
    }

    // A launch made only to switch a profile exits when done, unless the user opened the window or another launch
    // asked for the tray meanwhile.
    public bool StaysOpen { get; private set; }

    public void StayInTray() => StaysOpen = true;

    public void ShowWindow()
    {
        if (_window is null)
            return;

        StaysOpen = true;

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
        profiles.Changed -= OnProfilesChanged;
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
            case >= FirstProfileItem when item - FirstProfileItem < _menuProfiles.Count:
                SwitchProfileAsync(_menuProfiles[item - FirstProfileItem]).FireAndForget(logService);
                break;
        }
    }

    // --profile or --reapply-profile. True when nothing needs the user's attention, so a launch made only for it can exit;
    // otherwise the icon stays so its notification can be read.
    public async Task<bool> RunCommandLineProfileAsync(LaunchOptions launch)
    {
        if (launch.ReapplyProfile)
            return profiles.ActiveProfileId is not { } id || profiles.Find(id) is not { } active || await SwitchProfileAsync(active, reapply: true);

        if (profiles.Find(launch.Profile!) is { } profile)
            return await SwitchProfileAsync(profile, reapply: false);

        logService.LogWarning($"No profile named '{launch.Profile}' to switch to");
        trayIcon.ShowNotification(localization.GetString("Profiles_Failed_Title"), localization.GetString("Profiles_NotFound_Message", launch.Profile!));
        return false;
    }

    // Success shows as the tick on the next menu; only a failure, a needed restart or a profile that leaves out
    // settings this version added is worth a notification. Re-applying at sign-in stays silent.
    private async Task<bool> SwitchProfileAsync(SettingsProfile profile, bool reapply = false)
    {
        var result = reapply ? await profiles.ReapplyAsync(profile) : await profiles.SwitchAsync(profile);
        if (result is null)
            return true;

        if (!result.Success)
            trayIcon.ShowNotification(localization.GetString("Profiles_Failed_Title"), result.NotApplied.Count == 1
                ? localization.GetString("Profiles_Failed_MessageOne", profile.Name)
                : localization.GetString("Profiles_Failed_Message", profile.Name, result.NotApplied.Count));
        else if (result.NeedsRestart.Count > 0)
            trayIcon.ShowNotification(localization.GetString("Profiles_RestartNeeded_Title"), localization.GetString("Profiles_RestartNeeded_Message", profile.Name));
        else if (!reapply && await profiles.LoadAsync(profile) is { } config && await coverageSummary.TakeProfileReminderAsync(profile, config) is > 0 and var missing)
            trayIcon.ShowNotification(localization.GetString("Profiles_Reminder_Title"), localization.GetString("Profiles_Reminder_Toast", profile.Name, missing));
        return result.Success && result.NeedsRestart.Count == 0;
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

    private void OnProfilesChanged(object? sender, EventArgs e) => RefreshMenu();

    private void RefreshMenu()
    {
        _menuProfiles = profiles.GetProfiles();
        var activeId = profiles.LastSwitchFailed ? null : profiles.ActiveProfileId;
        var profileItems = _menuProfiles
            .Select((profile, i) => new TrayMenuItem(profile.Name, FirstProfileItem + i, Checked: profile.Id == activeId, Enabled: !profiles.IsBusy))
            .ToList();
        trayIcon.SetMenu(
        [
            new(localization.GetString("Tray_Open"), OpenItem),
            new(localization.GetString("Tray_SwitchProfile"), -1, Children: profileItems),
            new(localization.GetString("Tray_CheckForUpdates"), CheckItem),
            TrayMenuItem.Separator,
            new(localization.GetString("Tray_Exit"), ExitItem),
        ]);
    }
}

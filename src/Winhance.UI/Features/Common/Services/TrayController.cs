using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.SoftwareApps.Services;
using Winhance.UI.Helpers;
using Winhance.UI.ViewModels;
using WinRT.Interop;

namespace Winhance.UI.Features.Common.Services;

// Puts Winhance in the notification area. Closing or minimizing hides the window instead when the user chose that,
// and the icon brings it back. The icon shows only while the window is hidden or a notification waits for a click.
// Closed to the tray, the app hands the icon to the light TrayHost once nothing is running and leaves; minimized, it stays.
public sealed class TrayController(
    ISystemTrayIcon trayIcon,
    IUserPreferencesService preferences,
    ILocalizationService localization,
    IApplicationCloseService closeService,
    AppUpdateMonitor updateMonitor,
    IProfileService profiles,
    ICoverageSummaryService coverageSummary,
    ITaskProgressService taskProgress,
    ILogService logService,
    MemoryDiagnostics memoryDiagnostics,
    IVersionService versionService,
    UpdateCheckViewModel updateCheck) : IDisposable
{
    internal const string ProfileFlag = "--profile";
    internal const string ExitFlag = "--exit";
    internal const string CheckAppUpdatesFlag = "--check-app-updates";

    internal const int OpenItem = 0;
    internal const int CheckItem = 1;
    internal const int ExitItem = 2;
    internal const int StateItem = 3;
    internal const int UpdateAppsItem = 4;
    internal const int FirstProfileItem = 100;

    // A notification needs the icon to stay readable, and one that opens something on a click keeps it longer.
    internal static readonly TimeSpan ToastWait = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan ClickWait = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LeaveCheckInterval = TimeSpan.FromSeconds(5);

    private Window? _window;
    private Action? _showUpdates;
    private AppUpdateNoticeKind? _pendingNotice;
    private bool _exiting;
    private IReadOnlyList<SettingsProfile> _menuProfiles = [];
    // Once true the icon outlives this process: it goes back to the TrayHost instead of away.
    private bool _staysInTray;
    private bool _noticeWaitsForClick;
    private DateTime _notifiedAtUtc = DateTime.MinValue;
    private DispatcherQueueTimer? _leaveTimer;
    private TrayStatus _status = new();

    public void Attach(Window window, Action showUpdates)
    {
        _staysInTray = TrayHost.SignalQuit();
        _window = window;
        _showUpdates = showUpdates;
        trayIcon.Attach(WindowNative.GetWindowHandle(window));
        trayIcon.Invoked += OnTrayIconInvoked;
        trayIcon.NotificationClicked += OnNotificationClicked;
        trayIcon.MenuItemInvoked += OnMenuItemInvoked;
        window.AppWindow.Changed += OnAppWindowChanged;
        updateMonitor.NoticeRaised += OnNoticeRaised;
        updateMonitor.Finished += OnUpdatesFinished;
        updateMonitor.WinhanceUpdateChecked += OnWinhanceUpdateChecked;
        localization.LanguageChanged += OnLanguageChanged;
        profiles.Changed += OnProfilesChanged;
        _status = TrayStatus.Load(preferences, versionService.GetCurrentVersion());
        ApplyStatus();
        if (window.AppWindow.IsVisible)
            SetStatus(_status with { Attention = false });
        updateMonitor.Start();
    }

    // The window's Closing handler asks first; true means the window went to the tray instead of closing.
    public bool TryHideOnClose()
    {
        if (_exiting || !preferences.GetPreference(UserPreferenceKeys.CloseToTray, false))
            return false;

        _staysInTray = true;
        HideToTray();
        LeaveWhenIdle();
        return true;
    }

    public void HideToTray()
    {
        if (_window is null)
            return;

        _window.AppWindow.Hide();
        memoryDiagnostics.Log("hide-to-tray");
        RefreshMenu();
        trayIcon.Show(_status.Tooltip(localization));
        if (!preferences.GetPreference(UserPreferenceKeys.TrayHintShown, false))
        {
            preferences.SetPreferenceAsync(UserPreferenceKeys.TrayHintShown, true).FireAndForget(logService);
            Notify(localization.GetString("Tray_StillRunning_Title"), localization.GetString("Tray_StillRunning_Message"), waitsForClick: false);
        }
    }

    public void StayInTray() => _staysInTray = true;

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
        _noticeWaitsForClick = false;
    }

    public void RemoveIcon() => trayIcon.Hide();

    // A launch with a job to do hidden: --check-app-updates, --profile or --reapply-profile. It leaves once the job is
    // done, unless the window was opened meanwhile.
    public async Task RunBackgroundLaunchAsync(LaunchOptions launch)
    {
        if (launch.StartInTray)
            _staysInTray = true;
        if (launch.CheckAppUpdates is { } check)
            await updateMonitor.CheckAsync(manual: check != AppUpdateCheckLaunch.Scheduled, install: check == AppUpdateCheckLaunch.Install);
        if (launch.SwitchesProfile)
            await RunCommandLineProfileAsync(launch);
        LeaveWhenIdle();
    }

    internal static bool CanLeave(bool busy, bool noticeWaitsForClick, TimeSpan sinceNotice) =>
        !busy && sinceNotice >= (noticeWaitsForClick ? ClickWait : ToastWait);

    private void LeaveWhenIdle()
    {
        if (_window is null)
            return;

        if (_leaveTimer is null)
        {
            _leaveTimer = _window.DispatcherQueue.CreateTimer();
            _leaveTimer.Interval = LeaveCheckInterval;
            _leaveTimer.Tick += (_, _) => TryLeave();
        }
        _leaveTimer.Start();
        TryLeave();
    }

    private void TryLeave()
    {
        if (_exiting || _window is null || _window.AppWindow.IsVisible)
        {
            _leaveTimer?.Stop();
            return;
        }

        var busy = taskProgress.IsTaskRunning || profiles.IsBusy || updateMonitor.IsChecking;
        if (!CanLeave(busy, _noticeWaitsForClick, DateTime.UtcNow - _notifiedAtUtc))
            return;

        _leaveTimer?.Stop();
        LeaveAsync().FireAndForget(logService);
    }

    // The host would find this instance's key still registered and redirect to it, so the key goes first.
    private async Task LeaveAsync()
    {
        _exiting = true;
        if (_staysInTray)
        {
            AppInstance.GetCurrent().UnregisterKey();
            if (!TrayHost.Start(logService))
            {
                AppInstance.FindOrRegisterForKey(Program.AppKey);
                _exiting = false;
                return;
            }
        }
        await closeService.ExitAsync();
    }

    public void Dispose()
    {
        _leaveTimer?.Stop();
        updateMonitor.NoticeRaised -= OnNoticeRaised;
        updateMonitor.Finished -= OnUpdatesFinished;
        updateMonitor.WinhanceUpdateChecked -= OnWinhanceUpdateChecked;
        localization.LanguageChanged -= OnLanguageChanged;
        profiles.Changed -= OnProfilesChanged;
        if (_window is not null)
            _window.AppWindow.Changed -= OnAppWindowChanged;
        trayIcon.Dispose();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        // Attention clears once the window shows: the user is looking at what it pointed to.
        if (args.DidVisibilityChange && sender.IsVisible)
            SetStatus(_status with { Attention = false });

        if (sender.IsVisible
            && sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized }
            && preferences.GetPreference(UserPreferenceKeys.MinimizeToTray, false))
            HideToTray();
    }

    private void OnTrayIconInvoked(object? sender, EventArgs e) => OpenFromTray();

    private void OnNotificationClicked(object? sender, EventArgs e) => OpenFromTray();

    // A pending update notice opens straight onto the apps it is about, and a known Winhance update shows its info bar.
    private void OpenFromTray()
    {
        var notice = _pendingNotice;
        var winhanceUpdate = _status.WinhanceVersion is not null;
        ShowWindow();
        if (notice is AppUpdateNoticeKind.Available or AppUpdateNoticeKind.Installed)
            _showUpdates?.Invoke();
        if (winhanceUpdate)
            updateCheck.CheckForUpdatesOnStartupAsync().FireAndForget(logService);
    }

    private void OnMenuItemInvoked(object? sender, int item)
    {
        switch (item)
        {
            case OpenItem:
                OpenFromTray();
                break;
            case StateItem:
                if (_status.Badge == TrayBadge.AppUpdates)
                    _pendingNotice = AppUpdateNoticeKind.Available;
                OpenFromTray();
                break;
            case CheckItem:
                updateMonitor.CheckAsync(manual: true).FireAndForget(logService);
                break;
            case UpdateAppsItem:
                updateMonitor.CheckAsync(manual: true, install: true).FireAndForget(logService);
                break;
            case ExitItem:
                ExitAsync().FireAndForget(logService);
                break;
            case >= FirstProfileItem when item - FirstProfileItem < _menuProfiles.Count:
                SwitchProfileAsync(_menuProfiles[item - FirstProfileItem]).FireAndForget(logService);
                break;
        }
    }

    private async Task RunCommandLineProfileAsync(LaunchOptions launch)
    {
        if (launch.ReapplyProfile)
        {
            if (profiles.ActiveProfileId is { } id && profiles.FindById(id) is { } active)
                await SwitchProfileAsync(active, reapply: true);
            return;
        }

        if (profiles.Find(launch.Profile!) is { } profile)
        {
            await SwitchProfileAsync(profile, reapply: false);
            return;
        }

        logService.LogWarning($"No profile named '{launch.Profile}' to switch to");
        Notify(localization.GetString("Profiles_Failed_Title"), localization.GetString("Profiles_NotFound_Message", launch.Profile!), waitsForClick: true);
    }

    // Success shows as the tick on the next menu; only a failure, a needed restart or a profile that leaves out
    // settings this version added is worth a notification. Re-applying at sign-in stays silent.
    private async Task SwitchProfileAsync(SettingsProfile profile, bool reapply = false)
    {
        var result = reapply ? await profiles.ReapplyAsync(profile) : await profiles.SwitchAsync(profile);
        if (result is null)
            return;

        if (!result.Success || result.NeedsRestart.Count > 0)
            SetStatus(_status with { Attention = true });

        if (!result.Success)
            Notify(localization.GetString("Profiles_Failed_Title"), result.NotApplied.Count == 1
                ? localization.GetString("Profiles_Failed_MessageOne", profile.Name)
                : localization.GetString("Profiles_Failed_Message", profile.Name, result.NotApplied.Count), waitsForClick: true);
        else if (result.NeedsRestart.Count > 0)
            Notify(localization.GetString("Profiles_RestartNeeded_Title"), localization.GetString("Profiles_RestartNeeded_Message", profile.Name), waitsForClick: true);
        else if (!reapply && await profiles.LoadAsync(profile) is { } config && await coverageSummary.TakeProfileReminderAsync(profile, config) is > 0 and var missing)
            Notify(localization.GetString("Profiles_Reminder_Title"), localization.GetString("Profiles_Reminder_Toast", profile.Name, missing), waitsForClick: true);
    }

    private void Notify(string title, string message, bool waitsForClick)
    {
        _noticeWaitsForClick |= waitsForClick;
        _notifiedAtUtc = DateTime.UtcNow;
        trayIcon.ShowNotification(title, message);
    }

    // The same close as the window's X: the running-operation check, the daily support dialog, then shutdown.
    public async Task ExitAsync()
    {
        if (_exiting)
            return;

        _exiting = true;
        ShowWindow();
        var result = await closeService.CheckOperationsAndCloseAsync();
        if (!result.Success)
            _exiting = false;
    }

    private void OnNoticeRaised(object? sender, AppUpdateNotice notice)
    {
        _pendingNotice = notice.Kind;
        if (notice.Kind == AppUpdateNoticeKind.CheckFailed)
            SetStatus(_status with { Attention = true });
        Notify(notice.Title, notice.Message, waitsForClick: notice.Kind is AppUpdateNoticeKind.Available or AppUpdateNoticeKind.Installed);
    }

    private void OnUpdatesFinished(object? sender, AppUpdateOutcome outcome) =>
        SetStatus(_status with { AppUpdates = outcome.Pending, Attention = _status.Attention || outcome.InstallFailed });

    private void OnWinhanceUpdateChecked(object? sender, string? version) => RecordWinhanceUpdateAsync(version).FireAndForget(logService);

    private async Task RecordWinhanceUpdateAsync(string? version)
    {
        var current = versionService.GetCurrentVersion();
        var announce = await TrayStatus.RecordWinhanceUpdateAsync(preferences, version, current);
        _status = TrayStatus.Load(preferences, current);
        ApplyStatus();
        if (announce && _window is { AppWindow.IsVisible: false })
            Notify(localization.GetString("Tray_WinhanceUpdate_Title"), localization.GetString("Tray_WinhanceUpdate_Message", version!), waitsForClick: true);
    }

    private void SetStatus(TrayStatus status)
    {
        if (status == _status)
            return;

        _status = status;
        status.SaveAsync(preferences).FireAndForget(logService);
        ApplyStatus();
    }

    private void ApplyStatus()
    {
        trayIcon.SetBadge(_status.Badge, _status.Tooltip(localization));
        RefreshMenu();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyStatus();

    private void OnProfilesChanged(object? sender, EventArgs e) => RefreshMenu();

    private void RefreshMenu()
    {
        _menuProfiles = profiles.GetProfiles();
        trayIcon.SetMenuTheme(TrayStatus.MenuThemeFor(preferences.GetPreference(UserPreferenceKeys.Theme, string.Empty)));
        trayIcon.SetMenu(BuildMenu(localization, _menuProfiles, profiles.LastSwitchFailed ? null : profiles.ActiveProfileId, profiles.IsBusy, _status));
    }

    // The state line leads while a state is active, then Update apps joins when app updates wait.
    internal static IReadOnlyList<TrayMenuItem> BuildMenu(ILocalizationService localization, IReadOnlyList<SettingsProfile> menuProfiles, string? activeId, bool busy, TrayStatus status)
    {
        var profileItems = menuProfiles
            .Select((profile, i) => new TrayMenuItem(profile.Name, FirstProfileItem + i, Checked: profile.Id == activeId, Enabled: !busy))
            .ToList();
        List<TrayMenuItem> items = [];
        if (status.Text(localization) is { } state)
        {
            items.Add(new(state, StateItem));
            items.Add(TrayMenuItem.Separator);
        }
        items.Add(new(localization.GetString("Tray_Open"), OpenItem));
        items.Add(new(localization.GetString("Tray_SwitchProfile"), -1, Children: profileItems));
        if (status.AppUpdates > 0)
            items.Add(new(localization.GetString("Tray_Menu_UpdateApps", status.AppUpdates), UpdateAppsItem));
        items.Add(new(localization.GetString("Tray_CheckForUpdates"), CheckItem));
        items.Add(TrayMenuItem.Separator);
        items.Add(new(localization.GetString("Tray_Exit"), ExitItem));
        return items;
    }
}

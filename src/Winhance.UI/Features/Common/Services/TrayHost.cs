using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Extensions.DI;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.SoftwareApps.Services;
using Winhance.UI.Helpers;

namespace Winhance.UI.Features.Common.Services;

// What waits in the notification area once the window closes to the tray: the icon, its menu and the app-update
// schedule, on a hidden window with no XAML loaded. Every action starts the full app, which takes the icon over and
// tells this process to leave. The full app hidden in the tray held about 780 MB on a VM, most of it native rendering
// memory that hiding the window does not give back. PowerToys Awake has the same shape: a hidden window and a message loop.
internal sealed class TrayHost(
    SystemTrayIcon icon,
    IUserPreferencesService preferences,
    ILocalizationService localization,
    IReadOnlyList<SettingsProfile> profiles,
    IVersionService versionService,
    ISupportAskSchedule supportAsk,
    ILogService logService)
{
    private const string MutexName = @"Local\Winhance-TrayHost";
    private const string QuitEventName = @"Local\Winhance-TrayHost-Quit";
    // Set by the full app when it hands over: it has just run its own update check, so the first one here waits a full tick.
    private const string HandOffVariable = "WINHANCE_TRAY_HANDOFF";
    // SystemTrayIcon subclasses the same window with id 1.
    private const nuint SubclassId = 2;
    private const nuint CheckTimer = 1;
    // Posted to the window once a Winhance update check has saved its result; wParam 1 asks for the notification.
    private const uint WinhanceCheckedMessage = PInvoke.WM_APP + 2;

    internal static readonly string[] ScheduledCheckArguments =
        [TrayController.CheckAppUpdatesFlag, nameof(AppUpdateCheckLaunch.Scheduled), SignInTask.TrayFlag];

    private HWND _hwnd;
    // Held in a field: Windows keeps only a pointer to the delegate.
    private SUBCLASSPROC? _subclassProc;
    private TrayStatus _status = new();

    public static bool Start(ILogService logService)
    {
        var start = StartInfo([SignInTask.TrayFlag]);
        start.Environment[HandOffVariable] = "1";
        try
        {
            Process.Start(start)?.Dispose();
            return true;
        }
        catch (Win32Exception ex)
        {
            logService.LogError("Could not start the tray host", ex);
            return false;
        }
    }

    // True when a host was running; it removes its icon and leaves.
    public static bool SignalQuit()
    {
        if (!EventWaitHandle.TryOpenExisting(QuitEventName, out var quit))
            return false;

        using (quit)
            quit.Set();
        return true;
    }

    public static void Run(Func<bool> mainInstanceRunning, bool maintain)
    {
        using var mutex = new Mutex(true, MutexName, out var owned);
        if (!owned)
            return;

        // Created before the check below: a full app that starts after the check signals this event when it attaches.
        using var quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
        if (mainInstanceRunning())
            return;

        var firstCheck = Environment.GetEnvironmentVariable(HandOffVariable) is null ? AppUpdateMonitor.FirstCheckDelay : AppUpdateMonitor.Tick;
        Environment.SetEnvironmentVariable(HandOffVariable, null);

        using var services = StartServices(out var logService);
        var preferences = services.GetRequiredService<IUserPreferencesService>();
        var localization = services.GetRequiredService<ILocalizationService>();
        localization.SetLanguage(preferences.GetPreference(UserPreferenceKeys.Language, "en"));
        var fileSystem = services.GetRequiredService<IFileSystemService>();
        var profiles = ProfileService.List(localization, fileSystem, ProfileService.FolderFor(services.GetRequiredService<IInteractiveUserService>(), fileSystem));

        var maintenance = maintain ? Task.Run(() => MaintainAsync(services.GetRequiredService<IBackgroundTaskService>(), logService)) : null;

        var icon = new SystemTrayIcon(logService);
        new TrayHost(icon, preferences, localization, profiles, services.GetRequiredService<IVersionService>(), services.GetRequiredService<ISupportAskSchedule>(), logService).RunLoop(quit, firstCheck);

        // Quitting mid-pass would leave removals half done and dispose the services under them.
        maintenance?.Wait();
    }

    // The keep-removed job on its own (--maintain). Like the tray it shows nothing, so it never starts WinUI.
    public static void RunMaintenance()
    {
        using var services = StartServices(out var logService);
        logService.LogInformation("Keeping removed apps removed: run starting");
        MaintainAsync(services.GetRequiredService<IBackgroundTaskService>(), logService).GetAwaiter().GetResult();
    }

    // Only the Infrastructure container: neither host loads any XAML.
    private static ServiceProvider StartServices(out ILogService logService)
    {
        var services = new ServiceCollection().AddInfrastructureServices().BuildServiceProvider();
        logService = services.GetRequiredService<ILogService>();
        logService.StartLog();
        return services;
    }

    private static ProcessStartInfo StartInfo(IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        return start;
    }

    private static async Task MaintainAsync(IBackgroundTaskService backgroundTasks, ILogService logService)
    {
        try
        {
            await backgroundTasks.MaintainAsync();
        }
        catch (Exception ex)
        {
            logService.LogError("Keeping removed apps removed failed", ex);
        }
    }

    // The command line that starts the full app for a menu item, or null when the item starts nothing.
    internal static string[]? ArgumentsFor(int item, IReadOnlyList<SettingsProfile> profiles) => item switch
    {
        TrayController.OpenItem => [],
        TrayController.StateItem => [],
        TrayController.UpdateAppsItem => [TrayController.CheckAppUpdatesFlag, nameof(AppUpdateCheckLaunch.Install), SignInTask.TrayFlag],
        TrayController.CheckItem => [TrayController.CheckAppUpdatesFlag, nameof(AppUpdateCheckLaunch.Manual), SignInTask.TrayFlag],
        >= TrayController.FirstProfileItem when item - TrayController.FirstProfileItem < profiles.Count =>
            [TrayController.ProfileFlag, profiles[item - TrayController.FirstProfileItem].Id, SignInTask.TrayFlag],
        _ => null,
    };

    private unsafe void RunLoop(WaitHandle quit, TimeSpan firstCheck)
    {
        // A top-level window, not a message-only one: only top-level windows get Explorer's TaskbarCreated broadcast.
        _hwnd = PInvoke.CreateWindowEx(
            dwExStyle: 0,
            lpClassName: "STATIC",
            lpWindowName: "Winhance",
            dwStyle: 0,
            X: 0,
            Y: 0,
            nWidth: 0,
            nHeight: 0,
            hWndParent: HWND.Null,
            hMenu: null,
            hInstance: null,
            lpParam: null);
        if (_hwnd.IsNull)
        {
            logService.LogError("Could not create the tray window");
            return;
        }

        _subclassProc = WindowProc;
        PInvoke.SetWindowSubclass(_hwnd, _subclassProc, SubclassId, 0);
        icon.Attach(_hwnd);
        icon.Invoked += (_, _) => Launch([]);
        icon.NotificationClicked += (_, _) => Launch([]);
        icon.MenuItemInvoked += OnMenuItemInvoked;
        icon.SetMenuTheme(TrayStatus.MenuThemeFor(preferences.GetPreference(UserPreferenceKeys.Theme, string.Empty)));
        ApplyStatus();
        icon.Show(_status.Tooltip(localization));
        if (!preferences.GetPreference(UserPreferenceKeys.TrayHintShown, false))
        {
            preferences.SetPreferenceAsync(UserPreferenceKeys.TrayHintShown, true).FireAndForget(logService);
            icon.ShowNotification(localization.GetString("Tray_StillRunning_Title"), localization.GetString("Tray_StillRunning_Message"));
        }
        PInvoke.SetTimer(_hwnd, CheckTimer, (uint)firstCheck.TotalMilliseconds, null);

        var hwnd = _hwnd;
        var wait = ThreadPool.RegisterWaitForSingleObject(quit, (_, _) => PInvoke.PostMessage(hwnd, PInvoke.WM_CLOSE, default, default), null, Timeout.Infinite, executeOnlyOnce: true);
        while (PInvoke.GetMessage(out var message, HWND.Null, 0, 0).Value > 0)
        {
            PInvoke.TranslateMessage(in message);
            PInvoke.DispatchMessage(in message);
        }
        wait.Unregister(null);

        icon.Dispose();
        PInvoke.RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
        PInvoke.DestroyWindow(_hwnd);
    }

    private void ApplyStatus()
    {
        _status = TrayStatus.Load(preferences, versionService.GetCurrentVersion());
        icon.SetBadge(_status.Badge, _status.Tooltip(localization));
        icon.SetMenu(TrayController.BuildMenu(localization, profiles, ActiveProfileId(), busy: false, _status));
    }

    // Runs off the window's thread: it saves the result and posts the rest back.
    private async Task CheckWinhanceUpdateAsync()
    {
        var (done, version) = await AppUpdateMonitor.CheckWinhanceAsync(versionService, preferences, DateTime.UtcNow);
        if (!done)
            return;

        var announce = await TrayStatus.RecordWinhanceUpdateAsync(preferences, version, versionService.GetCurrentVersion());
        PInvoke.PostMessage(_hwnd, WinhanceCheckedMessage, new WPARAM(announce ? 1u : 0u), default);
    }

    private string? ActiveProfileId() => ProfileService.LastSwitchFailedIn(preferences) ? null : ProfileService.ActiveProfileIdIn(preferences);

    private void OnMenuItemInvoked(object? sender, int item)
    {
        if (item == TrayController.ExitItem)
            ExitAsync().FireAndForget(logService);
        else if (ArgumentsFor(item, profiles) is { } arguments)
            Launch(arguments);
    }

    // A due support ask goes to the full app, which shows it and closes. Posted rather than quit directly: this
    // continuation may not run on the window's thread.
    private async Task ExitAsync()
    {
        if (await ApplicationCloseService.AskIsDueAsync(supportAsk, logService) && Launch([TrayController.ExitFlag]))
            return;

        PInvoke.PostMessage(_hwnd, PInvoke.WM_CLOSE, default, default);
    }

    // The full app signals the quit event once it has the icon, so a launch that fails leaves this one in the tray.
    private bool Launch(string[] arguments)
    {
        try
        {
            using var process = Process.Start(StartInfo(arguments));
            if (process is not null)
                PInvoke.AllowSetForegroundWindow((uint)process.Id);
            return true;
        }
        catch (Win32Exception ex)
        {
            logService.LogError("Could not start Winhance", ex);
            return false;
        }
    }

    private LRESULT WindowProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam, nuint subclassId, nuint refData)
    {
        switch (message)
        {
            case PInvoke.WM_TIMER when wParam.Value == CheckTimer:
                PInvoke.SetTimer(_hwnd, CheckTimer, (uint)AppUpdateMonitor.Tick.TotalMilliseconds, null);
                if (AppUpdateMonitor.IsDue(preferences, DateTime.UtcNow))
                    Launch(ScheduledCheckArguments);
                if (AppUpdateMonitor.WinhanceCheckIsDue(preferences, DateTime.UtcNow))
                    CheckWinhanceUpdateAsync().FireAndForget(logService);
                return new LRESULT(0);
            case WinhanceCheckedMessage:
                ApplyStatus();
                if (wParam.Value == 1)
                    icon.ShowNotification(localization.GetString("Tray_WinhanceUpdate_Title"), localization.GetString("Tray_WinhanceUpdate_Message", _status.WinhanceVersion!));
                return new LRESULT(0);
            case PInvoke.WM_CLOSE:
                PInvoke.PostQuitMessage(0);
                return new LRESULT(0);
        }

        return PInvoke.DefSubclassProc(hwnd, message, wParam, lParam);
    }
}

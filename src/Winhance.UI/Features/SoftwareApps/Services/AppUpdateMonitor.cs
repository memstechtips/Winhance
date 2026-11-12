using System.Globalization;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.SoftwareApps.Services;

public enum AppUpdateNoticeKind
{
    Available,
    Installed,
    UpToDate,
    CheckFailed,
}

public sealed record AppUpdateNotice(AppUpdateNoticeKind Kind, string Title, string Message);

// Pending is what still has an update after a finished check; InstallFailed when installing left some of them.
public sealed record AppUpdateOutcome(int Pending, bool InstallFailed);

// Checks for app updates on the user's schedule while Winhance runs, shown or in the tray, then tells the user or
// installs them. It scans through External Software and Windows Apps, so the tabs show exactly what the check found.
// Start it on the UI thread: every await resumes there.
public sealed class AppUpdateMonitor(
    ExternalAppsViewModel externalApps,
    WindowsAppsViewModel windowsApps,
    IUserPreferencesService preferences,
    ILocalizationService localization,
    IVersionService versionService,
    ILogService logService) : IDisposable
{
    internal static readonly TimeSpan Tick = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(2);
    private CancellationTokenSource? _cts;
    private Task<bool>? _check;

    public event EventHandler<AppUpdateNotice>? NoticeRaised;

    public event EventHandler<AppUpdateOutcome>? Finished;

    // The newer Winhance version a daily check found, or null when the running one is current.
    public event EventHandler<string?>? WinhanceUpdateChecked;

    public bool IsChecking => _check is not null;

    public void Start()
    {
        if (_cts is not null)
            return;
        _cts = new CancellationTokenSource();
        RunAsync(_cts.Token).FireAndForget(logService);
    }

    internal static bool IsDue(AppUpdateCheckFrequency frequency, DateTime? lastCheckUtc, DateTime nowUtc) => frequency switch
    {
        AppUpdateCheckFrequency.Daily => lastCheckUtc is null || nowUtc - lastCheckUtc >= TimeSpan.FromDays(1),
        AppUpdateCheckFrequency.Weekly => lastCheckUtc is null || nowUtc - lastCheckUtc >= TimeSpan.FromDays(7),
        _ => false,
    };

    internal static bool IsDue(IUserPreferencesService preferences, DateTime nowUtc) =>
        IsDue(preferences.GetPreference(UserPreferenceKeys.AppUpdateCheckFrequency, AppUpdateCheckFrequency.Daily), LastCheckUtc(preferences), nowUtc);

    // The same schedule for Winhance itself, but never slower than daily; Off turns it off with the app update check.
    internal static bool WinhanceCheckIsDue(IUserPreferencesService preferences, DateTime nowUtc) =>
        preferences.GetPreference(UserPreferenceKeys.AppUpdateCheckFrequency, AppUpdateCheckFrequency.Daily) != AppUpdateCheckFrequency.Off
        && IsDue(AppUpdateCheckFrequency.Daily, LastCheckUtc(preferences, UserPreferenceKeys.LastWinhanceUpdateCheck), nowUtc);

    // Done is false when the check could not reach GitHub, so the last-check time stays and the next tick tries again.
    internal static async Task<(bool Done, string? Version)> CheckWinhanceAsync(IVersionService versionService, IUserPreferencesService preferences, DateTime nowUtc)
    {
        var latest = await versionService.CheckForUpdateAsync();
        if (string.IsNullOrEmpty(latest.Version))
            return (false, null);

        await preferences.SetPreferenceAsync(UserPreferenceKeys.LastWinhanceUpdateCheck, nowUtc.ToString("o", CultureInfo.InvariantCulture));
        return (true, latest.IsUpdateAvailable ? latest.Version : null);
    }

    // A manual check never installs unless install is set (the tray's Update apps); a scheduled one stays quiet about updates it already reported. A check that could
    // not finish (the scan failed, or the updates could not start behind another task) leaves the last-check time
    // alone, so the next tick tries again. Only a manual check says that it failed. A manual check that finds one
    // already running waits for it and reports what that one left on the rows.
    public async Task CheckAsync(bool manual, bool install = false)
    {
        if (_check is { } running)
        {
            if (manual)
                ReportRows(await running);
            return;
        }

        _check = RunCheckAsync(manual, install);
        try
        {
            await _check;
        }
        finally
        {
            _check = null;
        }
    }

    // The page Refresh: one scan feeds both tabs and the tray count, and no notice is raised. A scan that could not
    // read the updates is tried once more. Waits for a check already running instead of scanning again.
    public async Task RefreshAsync()
    {
        if (_check is { } running)
        {
            await running;
            return;
        }

        _check = RefreshRowsAsync();
        try
        {
            await _check;
        }
        finally
        {
            _check = null;
        }
    }

    private async Task<bool> RefreshRowsAsync()
    {
        try
        {
            if (!await ScanRowsAsync() && !await ScanRowsAsync())
                return false;

            Finished?.Invoke(this, new AppUpdateOutcome(UpdatableRows().Count(), false));
            return true;
        }
        catch (Exception ex)
        {
            logService.LogError("App update refresh failed", ex);
            return false;
        }
    }

    // True when every loaded tab had its updates known. Both tabs start together, so they share one read of the PC. A
    // tab that has not loaded yet scans for itself when it does.
    private async Task<bool> ScanRowsAsync()
    {
        var external = externalApps.IsInitialized ? externalApps.RefreshInstalledAppsAsync() : Task.FromResult(true);
        var windows = windowsApps.IsInitialized ? windowsApps.RefreshUpdatesAsync() : Task.FromResult(true);
        return (await Task.WhenAll(external, windows)).All(known => known);
    }

    // True when the check finished, so the rows show the updates this PC has now.
    private async Task<bool> RunCheckAsync(bool manual, bool install)
    {
        try
        {
            if (!externalApps.IsInitialized)
                await externalApps.LoadItemsAsync();
            if (!windowsApps.IsInitialized)
                await windowsApps.LoadItemsAsync();
            if (!await ScanRowsAsync())
            {
                if (manual)
                    ReportRows(finished: false);
                return false;
            }

            var updates = UpdatableRows().ToList();
            var installs = updates.Count > 0
                && (install || !manual && preferences.GetPreference(UserPreferenceKeys.AppUpdateAction, AppUpdateAction.Notify) == AppUpdateAction.Install);
            if (installs && !await UpdateAllAsync(updates))
                return false;

            await preferences.SetPreferenceAsync(UserPreferenceKeys.LastAppUpdateCheck, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            var pending = installs ? UpdatableRows().Count() : updates.Count;
            Finished?.Invoke(this, new AppUpdateOutcome(pending, installs && pending > 0));
            if (updates.Count == 0)
            {
                if (manual)
                    ReportRows(finished: true);
                return true;
            }

            if (installs)
            {
                var updated = updates.Count - pending;
                if (updated > 0)
                    Raise(AppUpdateNoticeKind.Installed, "Notify_UpdatesInstalled_Title", "Notify_UpdatesInstalled_Message", updated);
                return true;
            }

            var reported = string.Join('|', updates.Select(a => $"{a.Installed!.Key}={a.AvailableVersion}").Order(StringComparer.Ordinal));
            if (!manual && reported == preferences.GetPreference(UserPreferenceKeys.LastNotifiedAppUpdates, string.Empty))
                return true;

            await preferences.SetPreferenceAsync(UserPreferenceKeys.LastNotifiedAppUpdates, reported);
            Raise(AppUpdateNoticeKind.Available, "Notify_UpdatesAvailable_Title", "Notify_UpdatesAvailable_Message", updates.Count);
            return true;
        }
        catch (Exception ex)
        {
            logService.LogError("App update check failed", ex);
            return false;
        }
    }

    private IEnumerable<AppItemViewModel> UpdatableRows() =>
        externalApps.Items.Where(a => a.HasUpdate).Concat(windowsApps.Items.Where(a => a.HasUpdate));

    // False when an update run could not start behind another task.
    private async Task<bool> UpdateAllAsync(IReadOnlyList<AppItemViewModel> updates)
    {
        var external = updates.Where(externalApps.Items.Contains).ToList();
        var windows = updates.Except(external).ToList();
        return (external.Count == 0 || (await externalApps.UpdateAppsAsync(external)).Started)
            && (windows.Count == 0 || (await windowsApps.UpdateAppsAsync(windows)).Started);
    }

    private void ReportRows(bool finished)
    {
        var updates = UpdatableRows().Count();
        if (!finished)
            Raise(AppUpdateNoticeKind.CheckFailed, "Notify_CheckFailed_Title", "Notify_CheckFailed_Message");
        else if (updates == 0)
            Raise(AppUpdateNoticeKind.UpToDate, "Notify_UpToDate_Title", "Notify_UpToDate_Message");
        else
            Raise(AppUpdateNoticeKind.Available, "Notify_UpdatesAvailable_Title", "Notify_UpdatesAvailable_Message", updates);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(FirstCheckDelay, cancellationToken);
            using var timer = new PeriodicTimer(Tick);
            do
            {
                if (IsDue(preferences, DateTime.UtcNow))
                    await CheckAsync(manual: false);
                await CheckWinhanceUpdateAsync();
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task CheckWinhanceUpdateAsync()
    {
        if (!WinhanceCheckIsDue(preferences, DateTime.UtcNow))
            return;

        try
        {
            var (done, version) = await CheckWinhanceAsync(versionService, preferences, DateTime.UtcNow);
            if (done)
                WinhanceUpdateChecked?.Invoke(this, version);
        }
        catch (Exception ex)
        {
            logService.LogError("Winhance update check failed", ex);
        }
    }

    private static DateTime? LastCheckUtc(IUserPreferencesService preferences, string key = UserPreferenceKeys.LastAppUpdateCheck) =>
        DateTime.TryParse(preferences.GetPreference(key, string.Empty),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last) ? last.ToUniversalTime() : null;

    private void Raise(AppUpdateNoticeKind kind, string titleKey, string messageKey, int count = 0) =>
        NoticeRaised?.Invoke(this, new AppUpdateNotice(kind, localization.GetString(titleKey), localization.GetString(messageKey, count)));
}

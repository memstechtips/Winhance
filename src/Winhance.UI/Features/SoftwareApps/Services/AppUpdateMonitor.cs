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

// Checks for app updates on the user's schedule while Winhance runs, shown or in the tray, then tells the user or
// installs them. It scans through External Software, so the tab shows exactly what the check found. Start it on the
// UI thread: every await resumes there.
public sealed class AppUpdateMonitor(
    ExternalAppsViewModel externalApps,
    IUserPreferencesService preferences,
    ILocalizationService localization,
    ILogService logService) : IDisposable
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(2);
    private CancellationTokenSource? _cts;
    private Task<bool>? _check;

    public event EventHandler<AppUpdateNotice>? NoticeRaised;

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

    // A manual check never installs; a scheduled one stays quiet about updates it already reported. A check that could
    // not finish (the scan failed, or the updates could not start behind another task) leaves the last-check time
    // alone, so the next tick tries again. Only a manual check says that it failed. A manual check that finds one
    // already running waits for it and reports what that one left on the rows.
    public async Task CheckAsync(bool manual)
    {
        if (_check is { } running)
        {
            if (manual)
                ReportRows(await running);
            return;
        }

        _check = RunCheckAsync(manual);
        try
        {
            await _check;
        }
        finally
        {
            _check = null;
        }
    }

    // True when the check finished, so the rows show the updates this PC has now.
    private async Task<bool> RunCheckAsync(bool manual)
    {
        try
        {
            if (!externalApps.IsInitialized)
                await externalApps.LoadItemsAsync();
            if (!await externalApps.RefreshInstalledAppsAsync())
            {
                if (manual)
                    ReportRows(finished: false);
                return false;
            }

            var updates = externalApps.Items.Where(a => a.HasUpdate).ToList();
            var installs = updates.Count > 0 && !manual
                && preferences.GetPreference(UserPreferenceKeys.AppUpdateAction, AppUpdateAction.Notify) == AppUpdateAction.Install;
            if (installs && !(await externalApps.UpdateAppsAsync(updates)).Started)
                return false;

            await preferences.SetPreferenceAsync(UserPreferenceKeys.LastAppUpdateCheck, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            if (updates.Count == 0)
            {
                if (manual)
                    ReportRows(finished: true);
                return true;
            }

            if (installs)
            {
                var updated = updates.Count - externalApps.Items.Count(a => a.HasUpdate);
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

    private void ReportRows(bool finished)
    {
        var updates = externalApps.Items.Count(a => a.HasUpdate);
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
                var frequency = preferences.GetPreference(UserPreferenceKeys.AppUpdateCheckFrequency, AppUpdateCheckFrequency.Daily);
                if (IsDue(frequency, LastCheckUtc(), DateTime.UtcNow))
                    await CheckAsync(manual: false);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private DateTime? LastCheckUtc() =>
        DateTime.TryParse(preferences.GetPreference(UserPreferenceKeys.LastAppUpdateCheck, string.Empty),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last) ? last.ToUniversalTime() : null;

    private void Raise(AppUpdateNoticeKind kind, string titleKey, string messageKey, int count = 0) =>
        NoticeRaised?.Invoke(this, new AppUpdateNotice(kind, localization.GetString(titleKey), localization.GetString(messageKey, count)));
}

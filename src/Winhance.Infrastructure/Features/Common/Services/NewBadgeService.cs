using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

public class NewBadgeService : INewBadgeService
{
    private readonly IUserPreferencesService _prefs;
    private readonly ILogService _logService;
    private Version _baseline = new(99, 99, 99);
    private HashSet<string> _dismissed = new(StringComparer.OrdinalIgnoreCase);

    public NewBadgeService(IUserPreferencesService prefs, ILogService logService)
    {
        _prefs = prefs;
        _logService = logService;
    }

    public void Initialize()
    {
        var currentVersionStr = GetAppVersion();
        var lastRunStr = _prefs.GetPreference("LastRunVersion", "");

        if (string.IsNullOrEmpty(lastRunStr))
        {
            // First run with badge system — set baseline to 0.0.0 so all tagged settings show as new
            _baseline = new Version(0, 0, 0);
            _prefs.SetPreferenceAsync("LastRunVersion", currentVersionStr);
            _prefs.SetPreferenceAsync("NewBadgeBaseline", "0.0.0");
            _prefs.SetPreferenceAsync("NewBadgeDismissed", "");
            _logService.LogInformation($"[NewBadge] First run with badge system. Baseline set to 0.0.0");
            return;
        }

        if (!lastRunStr.Equals(currentVersionStr, StringComparison.OrdinalIgnoreCase))
        {
            // Version changed — upgrade detected
            _baseline = ParseVersion(lastRunStr);
            _prefs.SetPreferenceAsync("NewBadgeBaseline", lastRunStr);
            _prefs.SetPreferenceAsync("LastRunVersion", currentVersionStr);
            _prefs.SetPreferenceAsync("NewBadgeDismissed", "");
            _logService.LogInformation($"[NewBadge] Upgrade detected: {lastRunStr} -> {currentVersionStr}. Baseline set to {lastRunStr}");
            return;
        }

        // Same version — load existing baseline and dismissed list
        var baselineStr = _prefs.GetPreference("NewBadgeBaseline", currentVersionStr);
        _baseline = ParseVersion(baselineStr);

        var dismissedStr = _prefs.GetPreference("NewBadgeDismissed", "");
        if (!string.IsNullOrEmpty(dismissedStr))
        {
            _dismissed = new HashSet<string>(
                dismissedStr.Split(',', StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        _logService.LogDebug($"[NewBadge] Same version {currentVersionStr}. Baseline={baselineStr}, {_dismissed.Count} dismissed");
    }

    public bool IsSettingNew(string? addedInVersion, string settingId)
    {
        if (string.IsNullOrEmpty(addedInVersion))
            return false;

        if (_dismissed.Contains(settingId))
            return false;

        var settingVersion = ParseVersion(addedInVersion);
        return settingVersion > _baseline;
    }

    public void DismissBadge(string settingId)
    {
        _dismissed.Add(settingId);
        _prefs.SetPreferenceAsync("NewBadgeDismissed", string.Join(",", _dismissed));
    }

    private static string GetAppVersion()
    {
        var attr = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = attr?.InformationalVersion ?? "0.0.0";
        // Strip leading 'v' and any '+commithash' suffix
        version = version.TrimStart('v');
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];
        return version;
    }

    private static Version ParseVersion(string versionStr)
    {
        versionStr = versionStr.TrimStart('v');
        // Handle YY.MM.DD format (2-part or 3-part)
        return Version.TryParse(versionStr, out var v) ? v : new Version(0, 0, 0);
    }
}

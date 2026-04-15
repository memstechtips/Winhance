using System;
using System.Reflection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

public class NewBadgeService : INewBadgeService
{
    private readonly IUserPreferencesService _prefs;
    private readonly ILogService _logService;
    private readonly string? _versionOverride;
    private Version _baseline = new(99, 99, 99);

    public NewBadgeService(IUserPreferencesService prefs, ILogService logService, string? versionOverride = null)
    {
        _prefs = prefs;
        _logService = logService;
        _versionOverride = versionOverride;
    }

    public bool ShowNewBadges
    {
        get => _prefs.GetPreference(UserPreferenceKeys.ShowNewBadges, true);
        set => _prefs.SetPreferenceAsync(UserPreferenceKeys.ShowNewBadges, value);
    }

    public void Initialize()
    {
        var currentVersionStr = _versionOverride ?? GetAppVersion();
        var lastRunStr = _prefs.GetPreference("LastRunVersion", "");

        if (string.IsNullOrEmpty(lastRunStr))
        {
            // First run with badge system — set baseline to 0.0.0 so all tagged settings show as new
            _baseline = new Version(0, 0, 0);
            _prefs.SetPreferenceAsync("LastRunVersion", currentVersionStr);
            _prefs.SetPreferenceAsync("NewBadgeBaseline", "0.0.0");
            ShowNewBadges = true;
            _logService.LogInformation($"[NewBadge] First run with badge system. Baseline set to 0.0.0");
            return;
        }

        if (!lastRunStr.Equals(currentVersionStr, StringComparison.OrdinalIgnoreCase))
        {
            // Version changed — upgrade detected; force NEW badges back on
            _baseline = ParseVersion(lastRunStr);
            _prefs.SetPreferenceAsync("NewBadgeBaseline", lastRunStr);
            _prefs.SetPreferenceAsync("LastRunVersion", currentVersionStr);
            ShowNewBadges = true;
            _logService.LogInformation($"[NewBadge] Upgrade detected: {lastRunStr} -> {currentVersionStr}. Baseline set to {lastRunStr}; ShowNewBadges reset to true");
            return;
        }

        // Same version — load existing baseline, leave ShowNewBadges as-is
        var baselineStr = _prefs.GetPreference("NewBadgeBaseline", currentVersionStr);
        _baseline = ParseVersion(baselineStr);

        _logService.LogDebug($"[NewBadge] Same version {currentVersionStr}. Baseline={baselineStr}, ShowNewBadges={ShowNewBadges}");
    }

    public bool IsSettingNew(string? addedInVersion, string settingId)
    {
        if (string.IsNullOrEmpty(addedInVersion))
            return false;

        var settingVersion = ParseVersion(addedInVersion);
        return settingVersion > _baseline;
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
        return Version.TryParse(versionStr, out var v) ? v : new Version(0, 0, 0);
    }
}

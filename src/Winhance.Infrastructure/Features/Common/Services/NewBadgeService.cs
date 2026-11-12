using System.Reflection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class NewBadgeService : INewBadgeService
{
    private readonly IUserPreferencesService _prefs;
    private readonly ILogService _logService;
    private const string AutoHidePendingKey = "NewBadgesAutoHidePending";

    private Version _baseline = new(99, 99, 99);

    public NewBadgeService(IUserPreferencesService prefs, ILogService logService)
    {
        _prefs = prefs;
        _logService = logService;
    }

    public bool ShowNewBadges
    {
        get => _prefs.GetPreference(UserPreferenceKeys.ShowNewBadges, true);
        set => _prefs.SetPreferenceAsync(UserPreferenceKeys.ShowNewBadges, value);
    }

    public void Initialize(IEnumerable<string?> allAddedInVersions)
    {
        // LastRunVersion is kept only for future migration use; it does not drive badges.
        var currentAssemblyVersion = GetAppVersion();
        _prefs.SetPreferenceAsync("LastRunVersion", currentAssemblyVersion);

        Version? highestInRegistry = null;
        if (allAddedInVersions != null)
        {
            foreach (var raw in allAddedInVersions)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                if (!TryParseVersion(raw, out var parsed))
                    continue;
                if (highestInRegistry is null || parsed > highestInRegistry)
                    highestInRegistry = parsed;
            }
        }

        var storedHighestStr  = _prefs.GetPreference(UserPreferenceKeys.HighestSeenAddedInVersion, "");
        var storedBaselineStr = _prefs.GetPreference("NewBadgeBaseline", "");

        // No usable stored state: first install, preferences that predate the badge system, or one
        // of the two keys missing or unparseable. Everything is new to this user, so nothing gets a
        // NEW badge; the baseline is seeded at the registry's highest so the next upgrade works.
        var highestOk  = TryParseVersion(storedHighestStr,  out var storedHighest);
        var baselineOk = TryParseVersion(storedBaselineStr, out var storedBaseline);
        if (!highestOk || !baselineOk)
        {
            if (highestInRegistry is not null)
            {
                _baseline = highestInRegistry;
                _prefs.SetPreferenceAsync(
                    UserPreferenceKeys.HighestSeenAddedInVersion,
                    VersionToString(highestInRegistry));
                _prefs.SetPreferenceAsync("NewBadgeBaseline", VersionToString(highestInRegistry));
            }
            _prefs.SetPreferenceAsync(AutoHidePendingKey, false);
            _logService.LogInformation(
                "No usable badge state. Baseline set to the registry highest; no NEW badges.");
            return;
        }

        if (highestInRegistry is not null && highestInRegistry > storedHighest)
        {
            _baseline = storedHighest;
            _prefs.SetPreferenceAsync(
                UserPreferenceKeys.HighestSeenAddedInVersion,
                VersionToString(highestInRegistry));
            _prefs.SetPreferenceAsync("NewBadgeBaseline", VersionToString(storedHighest));
            ShowNewBadges = true;
            _prefs.SetPreferenceAsync(AutoHidePendingKey, true);
            _logService.LogInformation(
                $"Effective upgrade: registry highest {highestInRegistry} > stored {storedHighest}. " +
                $"Baseline={storedHighest}; ShowNewBadges reset to true.");
            return;
        }

        // No upgrade since last run. The upgrade launch set the pending flag; this launch consumes
        // it and hides the badges. A later manual re-enable finds the flag clear and sticks.
        _baseline = storedBaseline;
        if (_prefs.GetPreference(AutoHidePendingKey, false))
        {
            ShowNewBadges = false;
            _prefs.SetPreferenceAsync(AutoHidePendingKey, false);
        }
        _logService.LogDebug(
            $"No upgrade. Baseline={_baseline}, ShowNewBadges={ShowNewBadges}.");
    }

    public bool IsSettingNew(string? addedInVersion, string settingId)
    {
        if (string.IsNullOrEmpty(addedInVersion))
            return false;

        if (!TryParseVersion(addedInVersion, out var settingVersion))
            return false;

        return settingVersion > _baseline;
    }

    private static string GetAppVersion()
    {
        var attr = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = attr?.InformationalVersion ?? "0.0.0";
        version = version.TrimStart('v');
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];
        return version;
    }

    private static bool TryParseVersion(string versionStr, out Version parsed)
    {
        if (string.IsNullOrWhiteSpace(versionStr))
        {
            parsed = new Version(0, 0, 0);
            return false;
        }
        versionStr = versionStr.Trim().TrimStart('v');
        return Version.TryParse(versionStr, out parsed!);
    }

    private static string VersionToString(Version v)
    {
        // Version.Build is -1 when not specified; normalise to 0.
        var build = v.Build < 0 ? 0 : v.Build;
        return $"{v.Major}.{v.Minor}.{build}";
    }
}

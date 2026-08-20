using System.Reflection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class NewBadgeService : INewBadgeService
{
    private readonly IUserPreferencesService _prefs;
    private readonly ILogService _logService;
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

        // Uninitialized state — first-ever install, returning user whose preferences predate the
        // badge system, OR a half-populated state where one of the two keys is missing (or
        // unparseable). All roads lead to: baseline = 0.0.0, every tagged setting renders as NEW,
        // both keys get seeded so the next launch has a consistent pair.
        var highestOk  = TryParseVersion(storedHighestStr,  out var storedHighest);
        var baselineOk = TryParseVersion(storedBaselineStr, out var storedBaseline);
        if (!highestOk || !baselineOk)
        {
            _baseline = new Version(0, 0, 0);
            if (highestInRegistry is not null)
            {
                _prefs.SetPreferenceAsync(
                    UserPreferenceKeys.HighestSeenAddedInVersion,
                    VersionToString(highestInRegistry));
            }
            _prefs.SetPreferenceAsync("NewBadgeBaseline", VersionToString(_baseline));
            // Do NOT touch ShowNewBadges — leave whatever the user already has.
            _logService.LogInformation(
                "[NewBadge] Uninitialized or half-populated state. Baseline set to 0.0.0 (all tagged settings treated as new).");
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
            _logService.LogInformation(
                $"[NewBadge] Effective upgrade: registry highest {highestInRegistry} > stored {storedHighest}. " +
                $"Baseline={storedHighest}; ShowNewBadges reset to true.");
            return;
        }

        // No upgrade since last run — use the stored NewBadgeBaseline so NEW badges persist across
        // app launches until the next upgrade.
        _baseline = storedBaseline;
        _logService.LogDebug(
            $"[NewBadge] No upgrade. Baseline={_baseline}, ShowNewBadges={ShowNewBadges}.");
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

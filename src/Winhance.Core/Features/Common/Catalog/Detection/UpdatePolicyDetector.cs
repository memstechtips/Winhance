namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Detects the Windows Update policy state, reproducing the old <c>UpdateService</c> special-handler
/// precedence (<c>GetCurrentUpdatePolicyIndexAsync</c>) from the new detection context - the custom detector the
/// <see cref="IStateDetector"/> abstraction always named as intended but never built. Registry value-matching
/// cannot read this setting: the Disabled and Paused states both write <c>NoAutoUpdate=1</c>/<c>AUOptions=1</c>, and
/// the authoritative Disabled signal is a FILESYSTEM DLL rename, not a stored value. Precedence (highest first):
/// renamed critical DLLs -> Disabled; a live pause -> Paused; <c>DeferFeatureUpdates == 1</c> -> the deferred
/// "security-only" state; otherwise the Windows default. The four state labels are supplied explicitly (they must
/// equal the setting's authored <see cref="SettingState.Label"/>s so the resolved label maps back to an option).</summary>
public sealed class UpdatePolicyDetector : IStateDetector
{
    private const string UxSettings = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    private readonly string _defaultLabel;
    private readonly string _deferLabel;
    private readonly string _pausedLabel;
    private readonly string _disabledLabel;

    public UpdatePolicyDetector(string defaultLabel, string deferLabel, string pausedLabel, string disabledLabel)
    {
        _defaultLabel = defaultLabel;
        _deferLabel = deferLabel;
        _pausedLabel = pausedLabel;
        _disabledLabel = disabledLabel;
    }

    // Exposed so the retired SettingStructuralComparer could verify the authored labels match the converter's (authored == Convert).
    public string DefaultLabel => _defaultLabel;
    public string DeferLabel => _deferLabel;
    public string PausedLabel => _pausedLabel;
    public string DisabledLabel => _disabledLabel;

    public string? Detect(Setting setting, IDetectionContext context)
    {
        // Precedence mirrors UpdateService.GetCurrentUpdatePolicyIndexAsync exactly (index 3 -> 2 -> 1 -> 0).
        if (context.CriticalUpdateDllsRenamed())
            return _disabledLabel;

        if (IsPaused(context))
            return _pausedLabel;

        if (context.GetValue(UxSettings, "DeferFeatureUpdates") is int defer && defer == 1)
            return _deferLabel;

        return _defaultLabel;
    }

    /// <summary>Mirrors UpdateService.IsUpdatesPaused: any of the four pause markers present means paused.</summary>
    private static bool IsPaused(IDetectionContext context) =>
        context.GetValue(UxSettings, "PauseUpdatesStartTime") != null
        || context.GetValue(UxSettings, "PauseUpdatesExpiryTime") != null
        || context.GetValue(UxSettings, "PausedQualityDate") != null
        || context.GetValue(UxSettings, "PausedFeatureDate") != null;
}

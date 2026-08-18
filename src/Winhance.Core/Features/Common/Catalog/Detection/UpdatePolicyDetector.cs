namespace Winhance.Core.Features.Common.Catalog;

// Registry value-matching cannot read this setting: Disabled and Paused both write NoAutoUpdate=1 / AUOptions=1,
// and the authoritative Disabled signal is a filesystem DLL rename. Precedence: renamed DLLs -> Disabled; a live
// pause -> Paused; DeferFeatureUpdates == 1 -> the security-only state; else the Windows default. The labels must
// equal the setting's authored state labels so the result maps back to an option.
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

    private static bool IsPaused(IDetectionContext context) =>
        context.GetValue(UxSettings, "PauseUpdatesStartTime") != null
        || context.GetValue(UxSettings, "PauseUpdatesExpiryTime") != null
        || context.GetValue(UxSettings, "PausedQualityDate") != null
        || context.GetValue(UxSettings, "PausedFeatureDate") != null;
}

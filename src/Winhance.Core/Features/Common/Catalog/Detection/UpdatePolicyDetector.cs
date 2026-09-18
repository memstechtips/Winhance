using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Features.Common.Catalog;

// Registry value-matching cannot read this setting: Disabled and Paused both write NoAutoUpdate=1 / AUOptions=1,
// and the authoritative Disabled signal is a filesystem DLL rename. Precedence: renamed DLLs -> Disabled; a live
// pause -> Paused; DeferFeatureUpdates == 1 -> the security-only state; else the Windows default. The labels must
// equal the setting's authored state labels so the result maps back to an option.
public sealed class UpdatePolicyDetector : IStateDetector
{
    private const string UxSettings = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    private readonly LocKey _defaultLabel;
    private readonly LocKey _deferLabel;
    private readonly LocKey _pausedLabel;
    private readonly LocKey _disabledLabel;

    public UpdatePolicyDetector(LocKey defaultLabel, LocKey deferLabel, LocKey pausedLabel, LocKey disabledLabel)
    {
        _defaultLabel = defaultLabel;
        _deferLabel = deferLabel;
        _pausedLabel = pausedLabel;
        _disabledLabel = disabledLabel;
    }

    public string? Detect(Setting setting, IDetectionContext context)
    {
        if (context.CriticalUpdateDllsRenamed())
            return _disabledLabel.Value;

        if (IsPaused(context))
            return _pausedLabel.Value;

        if (context.GetValue(UxSettings, "DeferFeatureUpdates") is int defer && defer == 1)
            return _deferLabel.Value;

        return _defaultLabel.Value;
    }

    private static bool IsPaused(IDetectionContext context) =>
        context.GetValue(UxSettings, "PauseUpdatesStartTime") != null
        || context.GetValue(UxSettings, "PauseUpdatesExpiryTime") != null
        || context.GetValue(UxSettings, "PausedQualityDate") != null
        || context.GetValue(UxSettings, "PausedFeatureDate") != null;
}

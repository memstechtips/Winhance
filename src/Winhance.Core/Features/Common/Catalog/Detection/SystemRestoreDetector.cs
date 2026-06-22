namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Detects whether System Restore is on for the system drive, mapping to the injected enabled or
/// disabled state label.</summary>
public sealed class SystemRestoreDetector : IStateDetector
{
    private readonly string _enabledLabel;
    private readonly string _disabledLabel;

    public SystemRestoreDetector(string enabledLabel, string disabledLabel)
    {
        _enabledLabel = enabledLabel;
        _disabledLabel = disabledLabel;
    }

    public string EnabledLabel => _enabledLabel;
    public string DisabledLabel => _disabledLabel;

    public string? Detect(Setting setting, IDetectionContext context)
        => context.IsSystemRestoreEnabled() ? _enabledLabel : _disabledLabel;
}

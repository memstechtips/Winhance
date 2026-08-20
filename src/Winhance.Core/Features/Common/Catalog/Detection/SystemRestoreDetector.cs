namespace Winhance.Core.Features.Common.Catalog;

public sealed class SystemRestoreDetector : IStateDetector
{
    private readonly string _enabledLabel;
    private readonly string _disabledLabel;

    public SystemRestoreDetector(string enabledLabel, string disabledLabel)
    {
        _enabledLabel = enabledLabel;
        _disabledLabel = disabledLabel;
    }

    public string? Detect(Setting setting, IDetectionContext context)
        => context.IsSystemRestoreEnabled() ? _enabledLabel : _disabledLabel;
}

using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Features.Common.Catalog;

public sealed class SystemRestoreDetector : IStateDetector
{
    private readonly LocKey _enabledLabel;
    private readonly LocKey _disabledLabel;

    public SystemRestoreDetector(LocKey enabledLabel, LocKey disabledLabel)
    {
        _enabledLabel = enabledLabel;
        _disabledLabel = disabledLabel;
    }

    public string? Detect(Setting setting, IDetectionContext context)
        => context.IsSystemRestoreEnabled() ? _enabledLabel.Value : _disabledLabel.Value;
}

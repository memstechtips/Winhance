using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Features.Common.Catalog;

public sealed class DnsServerDetector : IStateDetector
{
    private readonly LocKey _automaticLabel;
    private readonly IReadOnlyDictionary<string, LocKey> _primaryIpToLabel;

    public DnsServerDetector(LocKey automaticLabel, IReadOnlyDictionary<string, LocKey> primaryIpToLabel)
    {
        _automaticLabel = automaticLabel;
        _primaryIpToLabel = primaryIpToLabel;
    }

    public string? Detect(Setting setting, IDetectionContext context)
    {
        var primary = context.PrimaryDnsV4OfActiveAdapter();
        if (string.IsNullOrEmpty(primary))
            return _automaticLabel.Value; // DHCP / no active adapter

        return _primaryIpToLabel.TryGetValue(primary, out var label) ? label.Value : null; // null = Custom
    }
}

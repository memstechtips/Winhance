namespace Winhance.Core.Features.Common.Catalog;

public sealed class DnsServerDetector : IStateDetector
{
    private readonly string _automaticLabel;
    private readonly IReadOnlyDictionary<string, string> _primaryIpToLabel;

    public DnsServerDetector(string automaticLabel, IReadOnlyDictionary<string, string> primaryIpToLabel)
    {
        _automaticLabel = automaticLabel;
        _primaryIpToLabel = primaryIpToLabel;
    }

    public string? Detect(Setting setting, IDetectionContext context)
    {
        var primary = context.PrimaryDnsV4OfActiveAdapter();
        if (string.IsNullOrEmpty(primary))
            return _automaticLabel; // DHCP / no active adapter

        return _primaryIpToLabel.TryGetValue(primary, out var label) ? label : null; // null = Custom
    }
}

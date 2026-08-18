namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Detects the DNS-server selection: an automatic (DHCP) adapter resolves to the automatic state;
/// a manual primary IPv4 DNS resolves to the state whose server it matches, or Custom when it matches none.
/// The automatic label and the server-IP to state-label map are injected so the detector is reusable.</summary>
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

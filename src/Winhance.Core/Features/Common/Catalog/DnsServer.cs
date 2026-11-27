namespace Winhance.Core.Features.Common.Catalog;

// Off, then Windows Settings' "On (automatic template)" and "On (manual template)", in its order.
public enum DohMode
{
    Off,
    Automatic,
    Manual,
}

public sealed record DnsServer(string Address, DohMode Doh = DohMode.Off, string? Template = null, bool Fallback = false)
{
    // DohFlags as Settings writes them (read off a 26H2 VM): the DNS_DOH_SERVER_SETTINGS bits 0x1 automatic template,
    // 0x2 manual template and 0x4 fallback to plaintext, plus 0x10, which Settings adds to every automatic entry.
    private const long AutomaticFlag = 0x1;
    private const long AutomaticFlags = AutomaticFlag | 0x10;
    private const long ManualFlag = 0x2;
    private const long FallbackFlag = 0x4;

    public bool IsIPv6 => DnsAddress.IsIPv6(Address);

    public long DohFlags => Doh switch
    {
        DohMode.Off => 0,
        DohMode.Automatic => AutomaticFlags | (Fallback ? FallbackFlag : 0),
        _ => ManualFlag | (Fallback ? FallbackFlag : 0),
    };

    public static DnsServer FromFlags(string address, long flags, string? template)
    {
        var doh = (flags & ManualFlag) != 0 ? DohMode.Manual : (flags & AutomaticFlag) != 0 ? DohMode.Automatic : DohMode.Off;
        return doh == DohMode.Off ? new DnsServer(address) : new DnsServer(address, doh, template, (flags & FallbackFlag) != 0);
    }
}

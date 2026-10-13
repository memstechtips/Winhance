using System.Net;
using System.Net.Sockets;

namespace Winhance.Core.Features.Common.Catalog;

public static class DnsAddress
{
    // Four dotted parts: IPAddress.TryParse alone also takes "1" and "1.2" as shorthand addresses.
    public static bool IsIPv4(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Split('.').Length == 4
            && IPAddress.TryParse(trimmed, out var address)
            && address.AddressFamily == AddressFamily.InterNetwork;
    }

    public static bool IsIPv6(string text) =>
        text.Contains(':') && IPAddress.TryParse(text.Trim(), out var address)
            && address.AddressFamily == AddressFamily.InterNetworkV6;

    // Windows takes only an https template.
    public static bool IsTemplate(string text) =>
        Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Optimize.Services;

// Writes what Windows Settings' DNS dialog writes for each adapter, then has the DNS Client service re-read it: the
// registry alone is only picked up on the next reconnect.
internal sealed class DnsSettingsService(IWindowsRegistryService reg, INetworkAdapters adapters, ILogService log) : IDnsSettingsService
{
    internal const string Interfaces = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    internal const string Interfaces6 = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces";
    internal const string InterfaceDoh = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters";
    internal const string WellKnownServers = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DohWellKnownServers";

    public bool SetDns(IReadOnlyList<DnsServer> servers)
    {
        bool ok = true;
        foreach (var adapter in adapters.Ids().Where(id => reg.KeyExists($@"{Interfaces}\{id}")))
        {
            ok &= reg.SetValue($@"{Interfaces}\{adapter}", "NameServer", Join(servers, ipv6: false), RegistryValueKind.String);
            if (reg.KeyExists($@"{Interfaces6}\{adapter}"))
                ok &= reg.SetValue($@"{Interfaces6}\{adapter}", "NameServer", Join(servers, ipv6: true), RegistryValueKind.String);
            ok &= WriteDoh(adapter, servers);
        }
        // The registry is what detection reads and it holds the servers either way, so a failed nudge is not a failed write.
        if (!adapters.NudgeDnsClient())
            log.Log(LogLevel.Warning, "The DNS Client service did not take the new settings; they apply on the next reconnect");
        return ok;
    }

    internal static IReadOnlyList<DnsServer> Read(IWindowsRegistryService reg, string adapter) =>
        [.. ReadFamily(reg, adapter, ipv6: false), .. ReadFamily(reg, adapter, ipv6: true)];

    internal static string DohKey(string adapter, bool ipv6) =>
        $@"{InterfaceDoh}\{adapter}\DohInterfaceSettings\{(ipv6 ? "Doh6" : "Doh")}";

    private static string Join(IReadOnlyList<DnsServer> servers, bool ipv6) =>
        string.Join(",", servers.Where(s => s.IsIPv6 == ipv6).Select(s => s.Address));

    private static IEnumerable<DnsServer> ReadFamily(IWindowsRegistryService reg, string adapter, bool ipv6)
    {
        var nameServer = reg.GetValue($@"{(ipv6 ? Interfaces6 : Interfaces)}\{adapter}", "NameServer") as string;
        var addresses = (nameServer ?? "").Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Where(a => ipv6 ? DnsAddress.IsIPv6(a) : DnsAddress.IsIPv4(a));
        foreach (var address in addresses)
        {
            var key = $@"{DohKey(adapter, ipv6)}\{address}";
            long flags = reg.GetValue(key, "DohFlags") is { } raw ? Convert.ToInt64(raw) : 0;
            yield return DnsServer.FromFlags(address, flags, reg.GetValue(key, "DohTemplate") as string);
        }
    }

    // Settings copies the built-in template into an automatic entry too; a server Windows has none for stays
    // unencrypted, the same as in Settings.
    private bool WriteDoh(string adapter, IReadOnlyList<DnsServer> servers)
    {
        reg.DeleteKey(DohKey(adapter, ipv6: false));
        reg.DeleteKey(DohKey(adapter, ipv6: true));
        bool ok = true;
        foreach (var server in servers.Where(s => s.Doh != DohMode.Off))
        {
            var key = $@"{DohKey(adapter, server.IsIPv6)}\{server.Address}";
            var template = server.Doh == DohMode.Manual
                ? server.Template
                : reg.GetValue($@"{WellKnownServers}\{server.Address}", "Template") as string;
            ok &= reg.CreateKey(key) && reg.SetValue(key, "DohFlags", server.DohFlags, RegistryValueKind.QWord);
            if (template is not null)
                ok &= reg.SetValue(key, "DohTemplate", template, RegistryValueKind.String);
        }
        return ok;
    }
}

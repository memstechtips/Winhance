using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: the <see cref="IDetectionContext"/> the DNS equivalence harness needs.
/// Its <see cref="PrimaryDnsV4OfActiveAdapter"/> reproduces the old DetectDnsServerIndex read EXACTLY - the
/// active non-loopback adapter; DHCP detected via an empty NameServer registry value (-> automatic); otherwise
/// the primary IPv4 DNS address. The registry/task members are never reached by the DnsServerDetector and throw
/// if called. Deleted once the migration is complete.</summary>
public sealed class DnsDetectionContext : IDetectionContext
{
    private readonly IWindowsRegistryService _reg;

    public DnsDetectionContext(IWindowsRegistryService reg) => _reg = reg;

    public WinBuild CurrentBuild => new(int.MaxValue); // DNS settings are not build-gated

    public string? PrimaryDnsV4OfActiveAdapter()
    {
        var activeAdapter = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
        if (activeAdapter == null)
            return null;

        // DNS via DHCP leaves NameServer empty; the old code treats that as the Automatic state.
        var nameServer = _reg.GetValue(
            $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{activeAdapter.Id}",
            "NameServer") as string;
        if (string.IsNullOrEmpty(nameServer))
            return null;

        var primaryDns = activeAdapter.GetIPProperties().DnsAddresses
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?
            .ToString();
        return string.IsNullOrEmpty(primaryDns) ? null : primaryDns;
    }

    public object? GetValue(string keyPath, string? valueName) =>
        throw new NotSupportedException("not needed for the DNS harness");

    public string[] GetSubKeyNames(string keyPath) =>
        throw new NotSupportedException("not needed for the DNS harness");

    public bool KeyExists(string keyPath) =>
        throw new NotSupportedException("not needed for the DNS harness");

    public bool IsSystemRestoreEnabled() =>
        throw new NotSupportedException("not needed for the DNS harness");

    public bool? ScheduledTaskEnabled(string taskPath) =>
        throw new NotSupportedException("not needed for the DNS harness");

    public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) =>
        throw new NotSupportedException("not needed for the DNS harness");
}

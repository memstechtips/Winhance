using System.Net.NetworkInformation;
using Windows.Win32;

namespace Winhance.Infrastructure.Features.Optimize.Services;

internal sealed class NetworkAdapters : INetworkAdapters
{
    // The adapters Get-NetAdapter lists: loopback and tunnel pseudo-interfaces carry no servers of their own.
    public IEnumerable<string> Ids() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => n.Id);

    public bool NudgeDnsClient()
    {
        using var manager = PInvoke.OpenSCManager(null, null, PInvoke.SC_MANAGER_CONNECT);
        using var service = PInvoke.OpenService(manager, "Dnscache", PInvoke.SERVICE_PAUSE_CONTINUE);
        return !service.IsInvalid && PInvoke.ControlService(service, PInvoke.SERVICE_CONTROL_PARAMCHANGE, out _);
    }
}

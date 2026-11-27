using System.ServiceProcess;
using System.Text.RegularExpressions;
using Windows.Win32;
using Windows.Win32.System.Services;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

// The Service Control Manager reads a service's Start value at boot and keeps it in memory, so a registry write
// alone changes nothing until the next restart. Telling the SCM as well makes the new start type hold at once.
internal sealed class ServiceStartControl(ILogService log) : IServiceStartControl
{
    private const int Automatic = 2;
    private const int Disabled = 4;

    private static readonly Regex ServiceKeyRegex = new(
        @"^HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\([^\\]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static string? ServiceNameOf(string path) =>
        ServiceKeyRegex.Match(path) is { Success: true } m ? m.Groups[1].Value : null;

    // Disabled also stops the service and Automatic starts it, as the Services console does; Manual leaves a
    // running service running. Best effort: the registry value is already written and wins at the next boot.
    public void Apply(string serviceName, int start)
    {
        try
        {
            if (!ChangeStartType(serviceName, start))
            {
                log.Log(LogLevel.Warning, $"The service manager did not take the new start type for {serviceName}; it applies after a restart");
                return;
            }

            using var service = new ServiceController(serviceName);
            if (start == Disabled && service.Status is not (ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending))
                service.Stop(stopDependentServices: false);
            else if (start == Automatic && service.Status == ServiceControllerStatus.Stopped)
                service.Start();
        }
        catch (Exception ex)
        {
            log.Log(LogLevel.Warning, $"Could not change {serviceName} live: {ex.Message}");
        }
    }

    private static unsafe bool ChangeStartType(string serviceName, int start)
    {
        using var manager = PInvoke.OpenSCManager(null, null, PInvoke.SC_MANAGER_CONNECT);
        using var service = PInvoke.OpenService(manager, serviceName, PInvoke.SERVICE_CHANGE_CONFIG);
        return !service.IsInvalid && PInvoke.ChangeServiceConfig(
            service,
            (ENUM_SERVICE_TYPE)PInvoke.SERVICE_NO_CHANGE,
            (SERVICE_START_TYPE)start,
            (SERVICE_ERROR)PInvoke.SERVICE_NO_CHANGE,
            null, null, null, null, null, null, null);
    }
}

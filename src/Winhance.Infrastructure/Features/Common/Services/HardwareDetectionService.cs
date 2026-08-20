using System.Management;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class HardwareDetectionService : IHardwareDetectionService
{
    private readonly ILogService _logService;

    // ExecutionAndPublication so a second caller arriving mid-query waits rather than starting its own
    // WMI round trip.
    private readonly Lazy<bool?> _hasBattery;

    public HardwareDetectionService(ILogService logService)
    {
        _logService = logService;
        _hasBattery = new Lazy<bool?>(QueryHasBattery, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool? HasBattery() => _hasBattery.Value;

    private bool? QueryHasBattery()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            using var collection = searcher.Get();
            return collection.Count > 0;
        }
        catch (Exception ex)
        {
            // null, not false: "no battery" and "could not tell" lead callers to different defaults.
            _logService.Log(LogLevel.Error, $"Error detecting battery: {ex.Message}");
            return null;
        }
    }

    public bool SupportsHybridSleep()
    {
        try
        {
            if (!PowerProf.GetPwrCapabilities(out var caps))
            {
                _logService.Log(LogLevel.Warning, "GetPwrCapabilities call failed");
                return false;
            }

            bool supported = caps.FastSystemS4;
            _logService.Log(LogLevel.Info, $"Hybrid sleep supported (FastSystemS4): {supported}");
            return supported;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error detecting hybrid sleep support: {ex.Message}");
            return false;
        }
    }
}

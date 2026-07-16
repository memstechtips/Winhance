using System;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

public class HardwareDetectionService : IHardwareDetectionService
{
    private readonly ILogService _logService;

    public HardwareDetectionService(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public Task<bool> HasBatteryAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                using var collection = searcher.Get();
                return collection.Count > 0;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error detecting battery: {ex.Message}");
                return false;
            }
        });
    }

    public Task<bool> SupportsHybridSleepAsync()
    {
        return Task.Run(() =>
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
        });
    }
}

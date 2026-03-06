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

    private static readonly int[] LaptopChassisTypes = new int[]
    {
        3, 8, 9, 10, 11, 14, 30, 31, 32
    };

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

    public async Task<bool> HasLidAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT PCSystemType FROM Win32_ComputerSystem"))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject system in collection)
                    {
                        using (system)
                        {
                            if (system["PCSystemType"] != null)
                            {
                                int pcSystemType = Convert.ToInt32(system["PCSystemType"]);
                                if (pcSystemType == 2)
                                    return true;
                                else if (pcSystemType == 1)
                                    return false;
                            }
                        }
                    }
                }

                using (var searcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject enclosure in collection)
                    {
                        using (enclosure)
                        {
                            if (enclosure["ChassisTypes"] is Array chassisTypes && chassisTypes.Length > 0)
                            {
                                foreach (var chassisType in chassisTypes)
                                {
                                    int type = Convert.ToInt32(chassisType);
                                    if (LaptopChassisTypes.Contains(type))
                                        return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error detecting if device has a lid: {ex.Message}");
                return true;
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> SupportsBrightnessControlAsync()
    {
        return await Task.Run(async () =>
        {
            try
            {
                var hasBattery = await HasBatteryAsync().ConfigureAwait(false);
                var hasLid = await HasLidAsync().ConfigureAwait(false);

                if (hasBattery && hasLid)
                    return true;

                using var searcher = new ManagementObjectSearcher("SELECT * FROM WmiMonitorBrightness");
                using var collection = searcher.Get();
                return collection.Count > 0;
            }
            catch (Exception)
            {
                // Don't log the expected error, it means brightness is not supported
                return false;
            }
        }).ConfigureAwait(false);
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

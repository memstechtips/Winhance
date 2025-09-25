using System;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
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

        public Task<int?> GetBatteryPercentageAsync()
        {
            return Task.Run<int?>(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                    using var collection = searcher.Get();

                    if (collection.Count == 0)
                        return null;

                    foreach (ManagementObject mo in collection)
                    {
                        return Convert.ToInt32(mo["EstimatedChargeRemaining"]);
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error getting battery percentage: {ex.Message}");
                    return null;
                }
            });
        }

        public Task<bool> IsRunningOnBatteryAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                    using var collection = searcher.Get();

                    if (collection.Count == 0)
                        return false;

                    foreach (ManagementObject battery in collection)
                    {
                        if (battery["BatteryStatus"] != null)
                        {
                            int status = Convert.ToInt32(battery["BatteryStatus"]);
                            return status == 1;
                        }
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error checking power source: {ex.Message}");
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

                    using (var searcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject enclosure in collection)
                        {
                            if (enclosure["ChassisTypes"] is Array chassisTypes && chassisTypes.Length > 0)
                            {
                                foreach (var chassisType in chassisTypes)
                                {
                                    int type = Convert.ToInt32(chassisType);
                                    var reliableLaptopTypes = new[] { 9, 10, 14, 31, 32 };
                                    if (reliableLaptopTypes.Contains(type))
                                        return true;
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
            });
        }

        public async Task<bool> SupportsBrightnessControlAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    var hasBattery = await HasBatteryAsync();
                    var hasLid = await HasLidAsync();
                    
                    if (hasBattery && hasLid)
                        return true;

                    using var searcher = new ManagementObjectSearcher("SELECT * FROM WmiMonitorBrightness");
                    using var collection = searcher.Get();
                    return collection.Count > 0;
                }
                catch (Exception ex)
                {
                    // Don't log the expected error, it means brightness is not supported
                    return false;
                }
            });
        }
    }
}

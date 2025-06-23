using System;
using System.Management;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Implementation of the battery service using WMI to query system power information.
    /// </summary>
    public class BatteryService : IBatteryService
    {
        private readonly ILogService _logService;

        // ChassisTypes that indicate a laptop/portable device with a lid
        private static readonly int[] LaptopChassisTypes = new int[] 
        { 
            3,  // Laptop
            8,  // Portable
            9,  // Laptop
            10, // Notebook
            11, // Hand Held
            14, // Sub Notebook
            30, // Tablet
            31, // Convertible
            32  // Detachable
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="BatteryService"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public BatteryService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public Task<bool> HasBatteryAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    _logService.Log(LogLevel.Info, "Checking for battery presence using WMI");
                    
                    // Query WMI for battery information
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                    using var collection = searcher.Get();
                    
                    int batteryCount = collection.Count;
                    _logService.Log(LogLevel.Info, $"Found {batteryCount} batteries in the system");
                    
                    // If any battery objects are found, a battery is present
                    bool hasBattery = batteryCount > 0;
                    
                    _logService.Log(LogLevel.Info, $"Battery detection result: {hasBattery}");
                    
                    // On desktop PCs, we should return false
                    return hasBattery;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error detecting battery: {ex.Message}");
                    _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                    
                    // Default to false in case of error on desktop PCs
                    _logService.Log(LogLevel.Info, "Defaulting to no battery due to error");
                    return false;
                }
            });
        }

        /// <inheritdoc/>
        public Task<int?> GetBatteryPercentageAsync()
        {
            return Task.Run<int?>(() =>
            {
                try
                {
                    // Check if battery exists first
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_Battery"
                    );
                    using var collection = searcher.Get();

                    if (collection.Count == 0)
                    {
                        _logService.Log(
                            LogLevel.Info,
                            "No battery found when querying battery percentage"
                        );
                        return null;
                    }

                    // Get the first battery's charge percentage
                    foreach (ManagementObject mo in collection)
                    {
                        int estimatedChargeRemaining = Convert.ToInt32(
                            mo["EstimatedChargeRemaining"]
                        );
                        _logService.Log(
                            LogLevel.Info,
                            $"Battery charge percentage: {estimatedChargeRemaining}%"
                        );
                        return estimatedChargeRemaining;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    _logService.Log(
                        LogLevel.Error,
                        $"Error getting battery percentage: {ex.Message}"
                    );
                    _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                    return null;
                }
            });
        }

        /// <inheritdoc/>
        public Task<bool> IsRunningOnBatteryAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    _logService.Log(LogLevel.Info, "Checking if running on battery power");
                    
                    // Query WMI for battery status
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                    using var collection = searcher.Get();
                    
                    // If no battery is found, we're definitely on AC power
                    if (collection.Count == 0)
                    {
                        _logService.Log(LogLevel.Info, "No battery found, running on AC power");
                        return false;
                    }
                    
                    // Check each battery's power state
                    foreach (ManagementObject battery in collection)
                    {
                        // BatteryStatus = 1 means the battery is discharging (running on battery)
                        // BatteryStatus = 2 means the battery is charging (on AC power)
                        // BatteryStatus = 3 means the battery is fully charged (on AC power)
                        if (battery["BatteryStatus"] != null)
                        {
                            int status = Convert.ToInt32(battery["BatteryStatus"]);
                            bool onBattery = status == 1;
                            
                            _logService.Log(LogLevel.Info, $"Battery status: {status}, running on battery: {onBattery}");
                            return onBattery;
                        }
                    }
                    
                    // Default to false (AC power) if we couldn't determine the status
                    _logService.Log(LogLevel.Warning, "Could not determine battery status, assuming AC power");
                    return false;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error checking power source: {ex.Message}");
                    _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                    
                    // Default to AC power in case of error
                    return false;
                }
            });
        }
        
        /// <inheritdoc/>
        public Task<bool> HasLidAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    _logService.Log(LogLevel.Info, "Checking if device has a lid (is a laptop)");
                    
                    // First check: Look for chassis type that indicates a laptop
                    using (var searcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_ComputerSystem"))
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject system in collection)
                        {
                            if (system["ChassisTypes"] is Array chassisTypes && chassisTypes.Length > 0)
                            {
                                foreach (var chassisType in chassisTypes)
                                {
                                    int type = Convert.ToInt32(chassisType);
                                    _logService.Log(LogLevel.Info, $"Detected chassis type: {type}");
                                    
                                    // Check if this is a laptop chassis type
                                    if (LaptopChassisTypes.Contains(type))
                                    {
                                        _logService.Log(LogLevel.Info, "Device is a laptop based on chassis type");
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    
                    // Second check: Look for portable computer system type
                    using (var searcher = new ManagementObjectSearcher("SELECT PCSystemType FROM Win32_ComputerSystem"))
                    using (var collection = searcher.Get())
                    {
                        foreach (ManagementObject system in collection)
                        {
                            if (system["PCSystemType"] != null)
                            {
                                int pcSystemType = Convert.ToInt32(system["PCSystemType"]);
                                _logService.Log(LogLevel.Info, $"PC System Type: {pcSystemType}");
                                
                                // PCSystemType = 2 means Mobile/Laptop
                                if (pcSystemType == 2)
                                {
                                    _logService.Log(LogLevel.Info, "Device is a laptop based on PC System Type");
                                    return true;
                                }
                            }
                        }
                    }
                    
                    // Third check: Look for battery presence as a fallback
                    // Many laptops will have a battery, so this is a reasonable fallback
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery"))
                    using (var collection = searcher.Get())
                    {
                        if (collection.Count > 0)
                        {
                            _logService.Log(LogLevel.Info, "Device has a battery, likely a laptop");
                            return true;
                        }
                    }
                    
                    _logService.Log(LogLevel.Info, "Device does not appear to be a laptop");
                    return false;
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error detecting if device has a lid: {ex.Message}");
                    _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                    
                    // Default to showing lid settings in case of error
                    // Better to show unnecessary settings than to hide needed ones
                    return true;
                }
            });
        }
    }
}

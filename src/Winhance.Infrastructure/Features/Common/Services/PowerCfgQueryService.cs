using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Utils;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Optimize.Models;
using System.Runtime.InteropServices;

namespace Winhance.Infrastructure.Features.Common.Services;

public class PowerCfgQueryService(ICommandService commandService, ILogService logService) : IPowerCfgQueryService
{
    private List<PowerPlan>? _cachedPlans;
    private DateTime _cacheTime;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(2);
    private readonly Dictionary<string, (int? min, int? max)> _capabilityCache = new();

    public async Task<List<PowerPlan>> GetAvailablePowerPlansAsync()
    {
        if (_cachedPlans != null && DateTime.UtcNow - _cacheTime < _cacheTimeout)
        {
            logService.Log(LogLevel.Debug, $"[PowerCfgQueryService] Using cached power plans ({_cachedPlans.Count} plans)");
            return _cachedPlans;
        }

        try
        {
            logService.Log(LogLevel.Info, "[PowerCfgQueryService] Enumerating power plans via Native API");
            
            var plans = new List<PowerPlan>();
            var activeGuid = GetActivePowerSchemeGuid();

            uint index = 0;
            uint bufferSize = 16; // Guid size
            IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

            try
            {
                while (true)
                {
                    uint ret = PowerProf.PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, PowerProf.ACCESS_SCHEME, index, buffer, ref bufferSize);
                    
                    if (ret == PowerProf.ERROR_NO_MORE_ITEMS)
                        break;

                    if (ret == PowerProf.ERROR_SUCCESS)
                    {
                        var guidBytes = new byte[16];
                        Marshal.Copy(buffer, guidBytes, 0, 16);
                        var guid = new Guid(guidBytes);

                        var name = GetPowerPlanName(guid);
                        var isActive = guid == activeGuid;

                        plans.Add(new PowerPlan
                        {
                            Guid = guid.ToString(),
                            Name = name,
                            IsActive = isActive
                        });
                    }
                    index++;
                    bufferSize = 16; // Reset for next call
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            _cachedPlans = plans.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToList();
            _cacheTime = DateTime.UtcNow;

            var activePlan = _cachedPlans.FirstOrDefault(p => p.IsActive);
            logService.Log(LogLevel.Info, $"[PowerCfgQueryService] Discovered {_cachedPlans.Count} system power plans. Active: {activePlan?.Name ?? "None"} ({activePlan?.Guid ?? "N/A"})");
            
            return _cachedPlans;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[PowerCfgQueryService] Error getting available power plans: {ex.Message}");
            return new List<PowerPlan>();
        }
    }

    private Guid GetActivePowerSchemeGuid()
    {
        IntPtr ptr = IntPtr.Zero;
        try
        {
            uint ret = PowerProf.PowerGetActiveScheme(IntPtr.Zero, out ptr);
            if (ret == PowerProf.ERROR_SUCCESS && ptr != IntPtr.Zero)
            {
                return Marshal.PtrToStructure<Guid>(ptr);
            }
            return Guid.Empty;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
            {
                PowerProf.LocalFree(ptr);
            }
        }
    }

    private string GetPowerPlanName(Guid schemeGuid)
    {
        uint bufferSize = 0;
        // First call to get size
        PowerProf.PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref bufferSize);

        if (bufferSize == 0) return "Unknown Power Plan";

        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            uint ret = PowerProf.PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, buffer, ref bufferSize);
            if (ret == PowerProf.ERROR_SUCCESS)
            {
                return Marshal.PtrToStringUni(buffer) ?? "Unknown Power Plan";
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return "Unknown Power Plan";
    }

    public void InvalidateCache()
    {
        _cachedPlans = null;
        _capabilityCache.Clear();
    }

    public async Task<PowerPlan> GetActivePowerPlanAsync()
    {
        try
        {
            var activeGuid = GetActivePowerSchemeGuid();
            if (activeGuid == Guid.Empty)
            {
                return new PowerPlan { Guid = "", Name = "Unknown", IsActive = true };
            }

            var name = GetPowerPlanName(activeGuid);
            return new PowerPlan
            {
                Guid = activeGuid.ToString(),
                Name = name,
                IsActive = true
            };
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting active power plan: {ex.Message}");
            return new PowerPlan { Guid = "", Name = "Unknown", IsActive = true };
        }
    }

    public async Task<PowerPlan?> GetPowerPlanByGuidAsync(string guid)
    {
        try
        {
            var availablePlans = await GetAvailablePowerPlansAsync();
            return availablePlans.FirstOrDefault(p => string.Equals(p.Guid, guid, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting power plan by GUID: {ex.Message}");
            return null;
        }
    }

    public async Task<int> GetPowerPlanIndexAsync(string guid, List<string> options)
    {
        try
        {
            var availablePlans = await GetAvailablePowerPlansAsync();
            var activePlanData = availablePlans.FirstOrDefault(p => p.IsActive);

            if (activePlanData == null)
                return 0;

            for (int i = 0; i < options.Count; i++)
            {
                var optionName = options[i].Trim();
                var matchingPlan = availablePlans.FirstOrDefault(p =>
                    p.Name.Trim().Equals(optionName, StringComparison.OrdinalIgnoreCase));

                if (matchingPlan != null && matchingPlan.Guid.Equals(activePlanData.Guid, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error resolving power plan index: {ex.Message}");
            return 0;
        }
    }

    public async Task<int?> GetPowerSettingValueAsync(PowerCfgSetting powerCfgSetting)
    {
        try
        {
            var command = $"powercfg /query SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid}";
            var result = await commandService.ExecuteCommandAsync(command);

            if (!result.Success || string.IsNullOrEmpty(result.Output))
                return null;

            return OutputParser.PowerCfg.ParsePowerSettingValue(result.Output, "Current AC Power Setting Index:");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error getting power setting value: {ex.Message}");
            return null;
        }
    }

    public async Task<(int? acValue, int? dcValue)> GetPowerSettingACDCValuesAsync(PowerCfgSetting powerCfgSetting)
    {
        try
        {
            return await Task.Run(() =>
            {
                var schemeGuid = GetActivePowerSchemeGuid();
                if (schemeGuid == Guid.Empty) return (null, null);

                var subGuid = Guid.Parse(powerCfgSetting.SubgroupGuid);
                var setGuid = Guid.Parse(powerCfgSetting.SettingGuid);

                uint acIndex, dcIndex;
                int? ac = null, dc = null;

                if (PowerProf.PowerReadACValueIndex(IntPtr.Zero, ref schemeGuid, ref subGuid, ref setGuid, out acIndex) == PowerProf.ERROR_SUCCESS)
                    ac = (int)acIndex;

                if (PowerProf.PowerReadDCValueIndex(IntPtr.Zero, ref schemeGuid, ref subGuid, ref setGuid, out dcIndex) == PowerProf.ERROR_SUCCESS)
                    dc = (int)dcIndex;

                return (ac, dc);
            });
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error getting power setting AC/DC values: {ex.Message}");
            return (null, null);
        }
    }

    public async Task<Dictionary<string, (int? acValue, int? dcValue)>> GetAllPowerSettingsACDCAsync(string powerPlanGuid = "SCHEME_CURRENT")
    {
        try
        {
            return await Task.Run(() =>
            {
                var results = new Dictionary<string, (int? acValue, int? dcValue)>();
                
                Guid schemeGuid;
                if (string.Equals(powerPlanGuid, "SCHEME_CURRENT", StringComparison.OrdinalIgnoreCase))
                {
                    schemeGuid = GetActivePowerSchemeGuid();
                }
                else if (!Guid.TryParse(powerPlanGuid, out schemeGuid))
                {
                     return results;
                }

                if (schemeGuid == Guid.Empty) return results;

                // Enumerate Subgroups
                uint subIndex = 0;
                uint bufferSize = 16;
                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

                try
                {
                    while (true)
                    {
                        uint ret = PowerProf.PowerEnumerate(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, PowerProf.ACCESS_SUBGROUP, subIndex, buffer, ref bufferSize);
                        if (ret == PowerProf.ERROR_NO_MORE_ITEMS) break;
                        if (ret == PowerProf.ERROR_SUCCESS)
                        {
                            var subBytes = new byte[16];
                            Marshal.Copy(buffer, subBytes, 0, 16);
                            var subGuid = new Guid(subBytes);

                            // Enumerate Settings in Subgroup
                            uint setIndex = 0;
                            uint setBufferSize = 16;
                            IntPtr setBuffer = Marshal.AllocHGlobal((int)setBufferSize);

                            try
                            {
                                while (true)
                                {
                                    uint setRet = PowerProf.PowerEnumerate(IntPtr.Zero, ref schemeGuid, ref subGuid, PowerProf.ACCESS_INDIVIDUAL_SETTING, setIndex, setBuffer, ref setBufferSize);
                                    if (setRet == PowerProf.ERROR_NO_MORE_ITEMS) break;
                                    if (setRet == PowerProf.ERROR_SUCCESS)
                                    {
                                        var setBytes = new byte[16];
                                        Marshal.Copy(setBuffer, setBytes, 0, 16);
                                        var setGuid = new Guid(setBytes);

                                        uint acIndex, dcIndex;
                                        int? ac = null, dc = null;

                                        if (PowerProf.PowerReadACValueIndex(IntPtr.Zero, ref schemeGuid, ref subGuid, ref setGuid, out acIndex) == PowerProf.ERROR_SUCCESS)
                                            ac = (int)acIndex;

                                        if (PowerProf.PowerReadDCValueIndex(IntPtr.Zero, ref schemeGuid, ref subGuid, ref setGuid, out dcIndex) == PowerProf.ERROR_SUCCESS)
                                            dc = (int)dcIndex;

                                        if (ac.HasValue || dc.HasValue)
                                        {
                                            results[setGuid.ToString()] = (ac, dc);
                                        }
                                    }
                                    setIndex++;
                                    setBufferSize = 16;
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(setBuffer);
                            }
                        }
                        subIndex++;
                        bufferSize = 16;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                return results;
            });
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error in GetAllPowerSettingsACDCAsync: {ex.Message}");
            return new Dictionary<string, (int?, int?)>();
        }
    }

    public async Task<(int? minValue, int? maxValue)> GetPowerSettingCapabilitiesAsync(PowerCfgSetting powerCfgSetting)
    {
        var cacheKey = powerCfgSetting.SettingGuid;

        if (_capabilityCache.TryGetValue(cacheKey, out var cached))
        {
            logService.Log(LogLevel.Debug, $"Using cached capabilities for {powerCfgSetting.SettingGUIDAlias ?? cacheKey}");
            return cached;
        }

        try
        {
            var command = $"powercfg /query SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid}";
            logService.Log(LogLevel.Debug, $"Querying power setting capabilities: {command}");

            var result = await commandService.ExecuteCommandAsync(command);

            if (!result.Success || string.IsNullOrEmpty(result.Output))
            {
                logService.Log(LogLevel.Warning, $"Failed to query capabilities for {powerCfgSetting.SettingGUIDAlias ?? cacheKey}");
                return (null, null);
            }

            var capabilities = OutputParser.PowerCfg.ParsePowerSettingMinMax(result.Output);
            _capabilityCache[cacheKey] = capabilities;

            logService.Log(LogLevel.Info,
                $"Power setting '{powerCfgSetting.SettingGUIDAlias ?? cacheKey}' capabilities: Min={capabilities.minValue}, Max={capabilities.maxValue}");

            return capabilities;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error,
                $"Error querying capabilities for {powerCfgSetting.SettingGuid}: {ex.Message}");
            return (null, null);
        }
    }

    public async Task<bool> IsSettingHardwareControlledAsync(PowerCfgSetting powerCfgSetting)
    {
        var (minValue, maxValue) = await GetPowerSettingCapabilitiesAsync(powerCfgSetting);

        bool isHardwareControlled = minValue == 0 && maxValue == 0;

        if (isHardwareControlled)
        {
            logService.Log(LogLevel.Info,
                $"Setting '{powerCfgSetting.SettingGUIDAlias ?? powerCfgSetting.SettingGuid}' is hardware-controlled (Min=0, Max=0)");
        }

        return isHardwareControlled;
    }

}
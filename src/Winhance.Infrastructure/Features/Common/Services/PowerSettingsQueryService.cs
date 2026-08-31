using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using System.Runtime.InteropServices;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class PowerSettingsQueryService(ILogService logService) : IPowerSettingsQueryService
{
    private volatile List<PowerPlan>? _cachedPlans;
    private DateTime _cacheTime;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(2);
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, (int? min, int? max)> _capabilityCache = new();

    public async Task<List<PowerPlan>> GetAvailablePowerPlansAsync()
    {
        var cached = _cachedPlans;
        if (cached != null)
        {
            lock (_cacheLock)
            {
                if (DateTime.UtcNow - _cacheTime < _cacheTimeout)
                {
                    logService.Log(LogLevel.Debug, $"[PowerSettingsQueryService] Using cached power plans ({cached.Count} plans)");
                    return cached;
                }
            }
        }

        try
        {
            logService.Log(LogLevel.Info, "[PowerSettingsQueryService] Enumerating power plans via Native API");
            
            var plans = new List<PowerPlan>();
            var activeGuid = GetActivePowerSchemeGuid();

            uint index = 0;
            uint bufferSize = 16; // Guid size
            IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

            try
            {
                unsafe
                {
                while (true)
                {
                    var ret = PInvoke.PowerEnumerate(null, null, null, POWER_DATA_ACCESSOR.ACCESS_SCHEME, index, (byte*)buffer, ref bufferSize);
                    
                    if (ret == WIN32_ERROR.ERROR_NO_MORE_ITEMS)
                        break;

                    if (ret == WIN32_ERROR.ERROR_SUCCESS)
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
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            var sortedPlans = plans.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToList();
            lock (_cacheLock)
            {
                _cachedPlans = sortedPlans;
                _cacheTime = DateTime.UtcNow;
            }

            var activePlan = sortedPlans.FirstOrDefault(p => p.IsActive);
            logService.Log(LogLevel.Info, $"[PowerSettingsQueryService] Discovered {sortedPlans.Count} system power plans. Active: {activePlan?.Name ?? "None"} ({activePlan?.Guid ?? "N/A"})");

            return sortedPlans;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[PowerSettingsQueryService] Error getting available power plans: {ex.Message}");
            return new List<PowerPlan>();
        }
    }

    private unsafe Guid GetActivePowerSchemeGuid()
    {
        Guid* active = null;
        try
        {
            if (PInvoke.PowerGetActiveScheme(null, out active) == WIN32_ERROR.ERROR_SUCCESS && active is not null)
            {
                return *active;
            }
            return Guid.Empty;
        }
        finally
        {
            if (active is not null)
            {
                PInvoke.LocalFree((HLOCAL)(IntPtr)active);
            }
        }
    }

    private unsafe string GetPowerPlanName(Guid schemeGuid)
    {
        uint bufferSize = 0;
        // First call to get size
        _ = PInvoke.PowerReadFriendlyName(null, schemeGuid, null, null, null, ref bufferSize);

        if (bufferSize == 0) return "Unknown Power Plan";

        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
        try
        {
            var ret = PInvoke.PowerReadFriendlyName(null, schemeGuid, null, null, (byte*)buffer, ref bufferSize);
            if (ret == WIN32_ERROR.ERROR_SUCCESS)
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
        lock (_cacheLock)
        {
            _cachedPlans = null;
            _capabilityCache.Clear();
        }
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

                if (PInvoke.PowerReadACValueIndex(null, schemeGuid, subGuid, setGuid, out acIndex) == WIN32_ERROR.ERROR_SUCCESS)
                    ac = (int)acIndex;

                if (PInvoke.PowerReadDCValueIndex(null, schemeGuid, subGuid, setGuid, out dcIndex) == (uint)WIN32_ERROR.ERROR_SUCCESS)
                    dc = (int)dcIndex;

                return (ac, dc);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error getting power setting AC/DC values: {ex.Message}");
            return (null, null);
        }
    }

    // Reads only what the caller asked for: two native calls per setting against one scheme lookup, where
    // GetAllPowerSettingsACDCAsync below costs ~225 PowerEnumerate + ~400 PowerRead* no matter how small the
    // batch. Keyed and filtered identically to it, so a caller can swap between the two.
    public async Task<Dictionary<string, (int? acValue, int? dcValue)>> GetPowerSettingsACDCAsync(IReadOnlyCollection<(string subgroupGuid, string settingGuid)> settings)
    {
        try
        {
            return await Task.Run(() =>
            {
                var results = new Dictionary<string, (int? acValue, int? dcValue)>(StringComparer.OrdinalIgnoreCase);

                var schemeGuid = GetActivePowerSchemeGuid();
                if (schemeGuid == Guid.Empty) return results;

                foreach (var (subgroup, setting) in settings)
                {
                    if (!Guid.TryParse(subgroup, out var subGuid) || !Guid.TryParse(setting, out var setGuid))
                        continue;

                    uint acIndex, dcIndex;
                    int? ac = null, dc = null;

                    if (PInvoke.PowerReadACValueIndex(null, schemeGuid, subGuid, setGuid, out acIndex) == WIN32_ERROR.ERROR_SUCCESS)
                        ac = (int)acIndex;

                    if (PInvoke.PowerReadDCValueIndex(null, schemeGuid, subGuid, setGuid, out dcIndex) == (uint)WIN32_ERROR.ERROR_SUCCESS)
                        dc = (int)dcIndex;

                    // Omitted rather than stored as (null, null): absent from the map is how a caller reads
                    // "this setting does not exist on this machine", the same contract the full walk has.
                    if (ac.HasValue || dc.HasValue)
                        results[setGuid.ToString()] = (ac, dc);
                }

                return results;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error in GetPowerSettingsACDCAsync: {ex.Message}");
            return new Dictionary<string, (int?, int?)>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<Dictionary<string, (int? acValue, int? dcValue)>> GetAllPowerSettingsACDCAsync(string powerPlanGuid = "SCHEME_CURRENT")
    {
        try
        {
            return await Task.Run(() =>
            {
                // OrdinalIgnoreCase because two paths now fill this map and catalog GUIDs are authored as
                // strings, while the walk keys on Guid.ToString().
                var results = new Dictionary<string, (int? acValue, int? dcValue)>(StringComparer.OrdinalIgnoreCase);

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

                uint subIndex = 0;
                uint bufferSize = 16;
                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

                try
                {
                    unsafe
                    {
                    while (true)
                    {
                        var ret = PInvoke.PowerEnumerate(null, schemeGuid, null, POWER_DATA_ACCESSOR.ACCESS_SUBGROUP, subIndex, (byte*)buffer, ref bufferSize);
                        if (ret == WIN32_ERROR.ERROR_NO_MORE_ITEMS) break;
                        if (ret == WIN32_ERROR.ERROR_SUCCESS)
                        {
                            var subBytes = new byte[16];
                            Marshal.Copy(buffer, subBytes, 0, 16);
                            var subGuid = new Guid(subBytes);

                            uint setIndex = 0;
                            uint setBufferSize = 16;
                            IntPtr setBuffer = Marshal.AllocHGlobal((int)setBufferSize);

                            try
                            {
                                while (true)
                                {
                                    var setRet = PInvoke.PowerEnumerate(null, schemeGuid, subGuid, POWER_DATA_ACCESSOR.ACCESS_INDIVIDUAL_SETTING, setIndex, (byte*)setBuffer, ref setBufferSize);
                                    if (setRet == WIN32_ERROR.ERROR_NO_MORE_ITEMS) break;
                                    if (setRet == WIN32_ERROR.ERROR_SUCCESS)
                                    {
                                        var setBytes = new byte[16];
                                        Marshal.Copy(setBuffer, setBytes, 0, 16);
                                        var setGuid = new Guid(setBytes);

                                        uint acIndex, dcIndex;
                                        int? ac = null, dc = null;

                                        if (PInvoke.PowerReadACValueIndex(null, schemeGuid, subGuid, setGuid, out acIndex) == WIN32_ERROR.ERROR_SUCCESS)
                                            ac = (int)acIndex;

                                        if (PInvoke.PowerReadDCValueIndex(null, schemeGuid, subGuid, setGuid, out dcIndex) == (uint)WIN32_ERROR.ERROR_SUCCESS)
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
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                return results;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error in GetAllPowerSettingsACDCAsync: {ex.Message}");
            return new Dictionary<string, (int?, int?)>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<(int? minValue, int? maxValue)> GetPowerSettingCapabilitiesAsync(PowerCfgSetting powerCfgSetting)
    {
        var cacheKey = powerCfgSetting.SettingGuid;

        lock (_cacheLock)
        {
            if (_capabilityCache.TryGetValue(cacheKey, out var cached))
            {
                logService.Log(LogLevel.Debug, $"Using cached capabilities for {powerCfgSetting.SettingGUIDAlias ?? cacheKey}");
                return cached;
            }
        }

        try
        {
            var capabilities = await Task.Run(() =>
            {
                var subGuid = Guid.Parse(powerCfgSetting.SubgroupGuid);
                var setGuid = Guid.Parse(powerCfgSetting.SettingGuid);

                int? min = null, max = null;

                if (PInvoke.PowerReadValueMin(null, subGuid, setGuid, out uint minVal) == WIN32_ERROR.ERROR_SUCCESS)
                    min = (int)minVal;

                if (PInvoke.PowerReadValueMax(null, subGuid, setGuid, out uint maxVal) == WIN32_ERROR.ERROR_SUCCESS)
                    max = (int)maxVal;

                return (min, max);
            }).ConfigureAwait(false);

            lock (_cacheLock)
            {
                _capabilityCache[cacheKey] = capabilities;
            }

            logService.Log(LogLevel.Info,
                $"Power setting '{powerCfgSetting.SettingGUIDAlias ?? cacheKey}' capabilities: Min={capabilities.min}, Max={capabilities.max}");

            return capabilities;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error,
                $"Error querying capabilities for {powerCfgSetting.SettingGuid}: {ex.Message}");
            return (null, null);
        }
    }

    // The transient PowerCfgSetting is an argument-holder only; its Recommended/Default values are unused by the capability query.
    public Task<bool> IsSettingHardwareControlledAsync(string subgroupGuid, string settingGuid)
        => IsSettingHardwareControlledAsync(new PowerCfgSetting
        {
            SubgroupGuid = subgroupGuid,
            SettingGuid = settingGuid,
            RecommendedValueAC = null,
            RecommendedValueDC = null,
            DefaultValueAC = null,
            DefaultValueDC = null,
        });

    public async Task<bool> IsSettingHardwareControlledAsync(PowerCfgSetting powerCfgSetting)
    {
        var (minValue, maxValue) = await GetPowerSettingCapabilitiesAsync(powerCfgSetting).ConfigureAwait(false);

        bool isHardwareControlled = minValue == 0 && maxValue == 0;

        if (isHardwareControlled)
        {
            logService.Log(LogLevel.Info,
                $"Setting '{powerCfgSetting.SettingGUIDAlias ?? powerCfgSetting.SettingGuid}' is hardware-controlled (Min=0, Max=0)");
        }

        return isHardwareControlled;
    }

}
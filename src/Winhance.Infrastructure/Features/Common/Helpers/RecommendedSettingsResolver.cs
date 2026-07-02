using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Helpers;

internal class OSInfo
{
    public int BuildNumber { get; set; }
    public int BuildRevision { get; set; }
    public bool IsWindows10 { get; set; }
    public bool IsWindows11 { get; set; }
}

internal static class RecommendedSettingsResolver
{
    internal static OSInfo BuildOSInfo(IWindowsVersionService versionService) =>
        new OSInfo
        {
            BuildNumber = versionService.GetWindowsBuildNumber(),
            BuildRevision = versionService.GetWindowsBuildRevision(),
            IsWindows10 = !versionService.IsWindows11(),
            IsWindows11 = versionService.IsWindows11()
        };

    internal static bool IsCompatibleWithCurrentOS(SettingDefinition setting, OSInfo osInfo)
    {
        if (setting.IsWindows10Only && !osInfo.IsWindows10) return false;
        if (setting.IsWindows11Only && !osInfo.IsWindows11) return false;

        if (setting.SupportedBuildRanges?.Count > 0)
        {
            bool inSupportedRange = setting.SupportedBuildRanges.Any(range =>
                osInfo.BuildNumber >= range.MinBuild && osInfo.BuildNumber <= range.MaxBuild);
            if (!inSupportedRange) return false;
        }
        else if (!BuildVersionGate.IsCompatible(
            osInfo.BuildNumber,
            osInfo.BuildRevision,
            setting.MinimumBuildNumber,
            setting.MinimumBuildRevision,
            setting.MaximumBuildNumber,
            setting.MaximumBuildRevision))
        {
            return false;
        }

        return true;
    }

    internal static bool HasRecommendedValue(SettingDefinition setting)
    {
        if (SettingDefinitionToggleState.GetRecommendedToggleState(setting).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsRecommended) == true) return true;
        return false;
    }

    internal static bool HasDefaultValue(SettingDefinition setting)
    {
        if (SettingDefinitionToggleState.GetDefaultToggleState(setting).HasValue) return true;
        if (setting.PowerCfgSettings?.Any(p => p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true) return true;
        if (setting.ComboBox?.Options?.Any(o => o.IsDefault) == true) return true;
        return false;
    }

    internal static object? GetRecommendedValueForSetting(SettingDefinition setting)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.RecommendedValue != null);
        return registrySetting?.RecommendedValue;
    }

    internal static object? GetDefaultValueForSetting(SettingDefinition setting)
    {
        var registrySetting = setting.RegistrySettings?.FirstOrDefault(rs => rs.DefaultValue != null);
        return registrySetting?.DefaultValue;
    }

    internal static int? GetRecommendedIndex(SettingDefinition setting)
    {
        var opts = setting.ComboBox?.Options;
        if (opts is null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (opts[i].IsRecommended) return i;
        return null;
    }

    internal static int? GetDefaultIndex(SettingDefinition setting)
    {
        var opts = setting.ComboBox?.Options;
        if (opts is null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (opts[i].IsDefault) return i;
        return null;
    }

    // For PowerCfg-backed Selection/NumericRange settings, build the value shape that
    // SettingApplicationService → PowerCfgApplier expects (matches what SettingItemViewModel
    // sends for AC/DC quick-set buttons). Returns null if the setting isn't PowerCfg-backed
    // or if neither AC nor DC has a target value.
    internal static object? BuildPowerCfgApplyValue(SettingDefinition setting, bool useRecommended)
    {
        var pcfg = setting.PowerCfgSettings?.FirstOrDefault();
        if (pcfg == null) return null;

        int? acRaw = useRecommended ? pcfg.RecommendedValueAC : pcfg.DefaultValueAC;
        int? dcRaw = useRecommended ? pcfg.RecommendedValueDC : pcfg.DefaultValueDC;
        if (!acRaw.HasValue && !dcRaw.HasValue) return null;

        bool isSeparate = pcfg.PowerModeSupport == PowerModeSupport.Separate;

        if (setting.InputType == InputType.Selection)
        {
            int? acIdx = FindOptionIndexForPowerCfgValue(setting, acRaw);
            int? dcIdx = FindOptionIndexForPowerCfgValue(setting, dcRaw);

            if (isSeparate)
            {
                if (!acIdx.HasValue && !dcIdx.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acIdx ?? 0,
                    ["DCValue"] = dcIdx ?? 0
                };
            }
            return (object?)(acIdx ?? dcIdx);
        }

        if (setting.InputType == InputType.NumericRange)
        {
            // Stored values are system units (e.g. Seconds). PowerCfgApplier converts
            // display→system on its end, so we hand it display units here.
            string displayUnits = GetPowerCfgDisplayUnits(setting);
            int? acDisplay = acRaw.HasValue ? ConvertSystemToDisplayUnits(acRaw.Value, displayUnits) : null;
            int? dcDisplay = dcRaw.HasValue ? ConvertSystemToDisplayUnits(dcRaw.Value, displayUnits) : null;

            if (isSeparate)
            {
                if (!acDisplay.HasValue && !dcDisplay.HasValue) return null;
                return new Dictionary<string, object?>
                {
                    ["ACValue"] = acDisplay ?? 0,
                    ["DCValue"] = dcDisplay ?? 0
                };
            }
            return (object?)(acDisplay ?? dcDisplay);
        }

        return null;
    }

    internal static int? FindOptionIndexForPowerCfgValue(SettingDefinition setting, int? targetValue)
    {
        if (!targetValue.HasValue) return null;
        var opts = setting.ComboBox?.Options;
        if (opts == null) return null;
        for (int i = 0; i < opts.Count; i++)
        {
            if (opts[i].ValueMappings is { } m && m.TryGetValue("PowerCfgValue", out var v) && v != null)
            {
                try { if (Convert.ToInt32(v) == targetValue.Value) return i; }
                catch { }
            }
        }
        return null;
    }

    internal static string GetPowerCfgDisplayUnits(SettingDefinition setting)
    {
        if (setting.NumericRange?.Units is { } unitsStr) return unitsStr;
        return setting.PowerCfgSettings?[0]?.Units ?? string.Empty;
    }

    // Inverse of PowerCfgApplier.ConvertToSystemUnits.
    internal static int ConvertSystemToDisplayUnits(int systemValue, string? units)
    {
        return units?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            // 1:1 — see UnitConversionHelper for the rationale.
            "milliseconds" => systemValue,
            _ => systemValue
        };
    }
}

using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Rebuilds the per-setting "custom-state value bag" (the untyped <c>RawValues</c> dictionary)
/// from the typed state fields, so the Builder/config-export and autounattend custom-state consumers keep
/// working. Builds the values the two consumers need (for a Selection resolved to Custom), per mechanism:
/// <list type="bullet">
///   <item>registry -> the live per-target <see cref="SettingStateResult.Readings"/>.</item>
///   <item>powercfg -> <c>ACValue</c>/<c>DCValue</c>/<c>PowerCfgValue</c> rebuilt from the typed
///     <see cref="SettingStateResult.AcValue"/>/<see cref="SettingStateResult.DcValue"/>
///     (<c>PowerCfgValue == ACValue ==</c> the AC reading).</item>
///   <item>DNS / system-tray detector -> <c>DetectedIndex</c> = the resolved
///     <see cref="SettingStateResult.CurrentValue"/> (the option index, incl. -1 Custom).</item>
/// </list>
/// Callers still apply their own <c>.Where(v =&gt; v != null)</c> filter.</summary>
public static class CustomStateValueReconstructor
{
    public static IReadOnlyDictionary<string, object?> Build(Setting setting, SettingStateResult state)
    {
        // UNION the applicable mechanisms' keys (a setting is usually single-mechanism, but union is faithful even if
        // one ever carried both registry + a detector).
        var values = new Dictionary<string, object?>();

        // Registry: the per-target readings become the bag's registry keys.
        if (state.Readings is { } readings)
            foreach (var kv in readings)
                values[kv.Key] = kv.Value;

        // Powercfg: write ACValue=acValue, DCValue=dcValue, PowerCfgValue=acValue.
        if (setting.Targets.OfType<PowerCfgTarget>().Any() && state.AcValue is int ac)
        {
            values["ACValue"] = ac;
            values["DCValue"] = state.DcValue;
            values["PowerCfgValue"] = ac;
        }

        // DNS / system-tray: store the resolved CurrentValue as DetectedIndex.
        if (setting.Detector is DnsServerDetector or SystemTrayDetector && state.CurrentValue is int idx)
            values["DetectedIndex"] = idx;

        return values;
    }
}

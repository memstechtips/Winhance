using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

// Rebuilds the untyped RawValues bag from the typed fields for the Builder/config-export and autounattend consumers:
// registry -> Readings; powercfg -> ACValue/DCValue/PowerCfgValue (= AC); DNS / system-tray -> DetectedIndex (incl. -1 Custom).
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

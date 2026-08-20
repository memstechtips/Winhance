using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

// Rebuilds the untyped RawValues bag from the typed fields for the Builder/config-export and autounattend consumers:
// registry -> Readings; powercfg -> ACValue/DCValue/PowerCfgValue (= AC); DNS / system-tray -> DetectedIndex
// (incl. -1 Custom), plus primary/secondary for a Custom DNS.
public static class CustomStateValueReconstructor
{
    public static IReadOnlyDictionary<string, object?> Build(Setting setting, SettingStateResult state)
    {
        // UNION the applicable mechanisms' keys (a setting is usually single-mechanism, but union is faithful even if
        // one ever carried both registry + a detector).
        var values = new Dictionary<string, object?>();

        if (state.Readings is { } readings)
            foreach (var kv in readings)
                values[kv.Key] = kv.Value;

        if (setting.Targets.OfType<PowerCfgTarget>().Any() && state.AcValue is int ac)
        {
            values["ACValue"] = ac;
            values["DCValue"] = state.DcValue;
            values["PowerCfgValue"] = ac;
        }

        if (setting.Detector is DnsServerDetector or SystemTrayDetector && state.CurrentValue is int idx)
        {
            values["DetectedIndex"] = idx;

            // The index alone says "not one of the presets", which no other machine can act on. The catalog's
            // CustomStateScripts interpolate {{primary}} and {{secondary}}, so a Custom DNS carries the two
            // addresses under exactly those names. A server the machine does not have omits its key: an empty
            // string would substitute into the script as a real (blank) address.
            if (setting.Detector is DnsServerDetector
                && idx == ComboBoxConstants.CustomStateIndex
                && state.DnsServers is { Count: > 0 } servers)
            {
                values["primary"] = servers[0];
                if (servers.Count > 1)
                    values["secondary"] = servers[1];
            }
        }

        return values;
    }
}

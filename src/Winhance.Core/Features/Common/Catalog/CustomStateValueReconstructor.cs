using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Rebuilds the per-setting "custom-state value bag" the OLD discovery exposed as
/// <see cref="SettingStateResult.RawValues"/>, from the NEW engine's typed fields - so the Builder/config-export and
/// autounattend custom-state consumers keep working after old discovery + RawValues are retired. Reproduces the
/// non-null RawValues the two consumers captured (for a Selection resolved to Custom) EXACTLY, per mechanism:
/// <list type="bullet">
///   <item>registry -> the live per-target <see cref="SettingStateResult.Readings"/> (proven == the old RawValues
///     registry keys by CustomStateReadingsEquivalenceTests, 423/423).</item>
///   <item>powercfg -> <c>ACValue</c>/<c>DCValue</c>/<c>PowerCfgValue</c> rebuilt from the typed
///     <see cref="SettingStateResult.AcValue"/>/<see cref="SettingStateResult.DcValue"/>. Old discovery set all three
///     from the AC/DC reading (<c>PowerCfgValue == ACValue == acValue</c>), and the overlay already threads the SAME
///     AC/DC into RawValues, so this is value-identical.</item>
///   <item>DNS / system-tray detector -> <c>DetectedIndex</c> = the resolved <see cref="SettingStateResult.CurrentValue"/>,
///     which equals the old <c>DetectDnsServerIndex</c>/<c>DetectSystemTrayIndex</c> (same option index, incl. -1 Custom;
///     detection equivalence proven by Dns/SystemTray EquivalenceTests).</item>
/// </list>
/// Callers still apply their own <c>.Where(v =&gt; v != null)</c> filter, exactly as they did over RawValues.</summary>
public static class CustomStateValueReconstructor
{
    public static IReadOnlyDictionary<string, object?> Build(Setting setting, SettingStateResult state)
    {
        // UNION the applicable mechanisms' keys (a setting is usually single-mechanism, but union is faithful even if
        // one ever carried both registry + a detector - matching the old discovery, which layered all applicable keys).
        var values = new Dictionary<string, object?>();

        // Registry: the per-target readings ARE the old RawValues registry keys (D4-proven).
        if (state.Readings is { } readings)
            foreach (var kv in readings)
                values[kv.Key] = kv.Value;

        // Powercfg: old wrote ACValue=acValue, DCValue=dcValue, PowerCfgValue=acValue.
        if (setting.Targets.OfType<PowerCfgTarget>().Any() && state.AcValue is int ac)
        {
            values["ACValue"] = ac;
            values["DCValue"] = state.DcValue;
            values["PowerCfgValue"] = ac;
        }

        // DNS / system-tray: old stored the resolved DetectedIndex; the new engine resolves it as CurrentValue.
        if (setting.Detector is DnsServerDetector or SystemTrayDetector && state.CurrentValue is int idx)
            values["DetectedIndex"] = idx;

        return values;
    }
}

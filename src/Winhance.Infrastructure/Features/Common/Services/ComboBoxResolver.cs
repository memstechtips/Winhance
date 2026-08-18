using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ComboBoxResolver : IComboBoxResolver
{
    private readonly IWindowsVersionService _versionService;

    public ComboBoxResolver(IWindowsVersionService versionService)
    {
        _versionService = versionService;
    }

    public int ResolveRawValuesToIndex(Setting setting, Dictionary<string, object?> rawValues)
    {
        // DetectedIndex from a custom detector (e.g. DnsServer) - catalog-agnostic.
        if (rawValues.TryGetValue("DetectedIndex", out var detectedIndex) && detectedIndex is int di)
            return di;

        if (setting.Control != ControlKind.Selection)
            return 0;

        // A resolved group-policy index short-circuits value matching.
        if (rawValues.TryGetValue("CurrentPolicyIndex", out var policyIndex))
            return policyIndex is int index ? index : 0;

        // The states ARE the ComboBox options 1:1. A selection whose options carry NO
        // ValueMappings (the DNS / system-tray detector selections, whose states have an empty Set) has no
        // value-match to run and resolves to index 0.
        var states = setting.States;
        if (states.All(s => s.Set.Count == 0))
            return 0;

        // Each StateValue folds in the target-DefaultValue substitution (StateValue.OrAbsent, authored where a mapping
        // value equals the target's DefaultValue).
        for (int i = 0; i < states.Count; i++)
        {
            var set = states[i].Set;
            if (set.Count == 0)
                continue;

            bool allMatch = true;
            foreach (var entry in set)
            {
                var readKey = ReadKeyForTarget(setting, entry.Key);
                bool present = rawValues.TryGetValue(readKey, out var current) && current != null;
                if (!entry.Value.Matches(present ? current : null, present))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
                return i;
        }

        // allBackingValuesAbsent: a key is 'absent' here only when its read is absent AND it has no
        // folded default. A key carrying an Of(x).OrAbsent() StateValue (AcceptsAbsent with a concrete accepted
        // value) on any state has a live default and is NEVER all-absent (e.g. explorer-click-items' IconUnderline
        // = Of(3).OrAbsent() keeps allBackingValuesAbsent false). The PowerCfgValue key
        // counts only when the reads carry it (a powercfg selection with no PowerCfgValue read has
        // an EMPTY backing set, so it is NOT all-absent).
        //
        // CAVEAT: a precedence-CORRECTED selection (CatalogDetectionModelConformanceTests.PrecedenceCorrectedIds - among
        // selections only gaming-touch-keyboard-service) authors an Of(x).OrAbsent() that is NOT a DefaultValue-fold
        // but a deliberate detection fix. CatalogDetectionModelConformanceTests runs each of those ids through
        // CatalogDiscovery.Detect over CONSTRUCTED readings (clean / recommended-applied / group-policy /
        // mirror-split) and proves the corrected reading is the one Windows would show. It pins the detection MODEL
        // this fallback implements, not this method directly.
        var keysWithFoldedDefault = new HashSet<string>();
        foreach (var st in states)
            foreach (var e in st.Set)
                if (e.Value.AcceptsAbsent && e.Value.AcceptedValues.Count > 0)
                    keysWithFoldedDefault.Add(e.Key);

        bool anyBacking = false;
        bool allBackingAbsent = true;
        if (setting.Targets.OfType<PowerCfgTarget>().Any()
            && rawValues.TryGetValue("PowerCfgValue", out var pcv))
        {
            anyBacking = true;
            if (pcv != null) allBackingAbsent = false;
        }
        foreach (var rt in setting.Targets.OfType<RegTarget>())
        {
            anyBacking = true;
            bool present = rawValues.TryGetValue(rt.Key, out var rv) && rv != null;
            if (present || keysWithFoldedDefault.Contains(rt.Key)) allBackingAbsent = false;
        }
        bool allBackingValuesAbsent = anyBacking && allBackingAbsent;

        if (allBackingValuesAbsent || states.Any(s => s.IsFallback))
        {
            // Build-aware for the Always-context default so a merged Selection whose Windows default is build-scoped
            // (theme-mode-windows) resolves to the live build's default option instead of Custom. The AC-context
            // check stays context-based (power roles are never build-scoped).
            var build = new WinBuild(_versionService.GetWindowsBuildNumber(), _versionService.GetWindowsBuildRevision());
            for (int i = 0; i < states.Count; i++)
                if (states[i].HasRole(RoleKind.WindowsDefault, build, PowerContext.Always)
                    || states[i].HasRole(RoleKind.WindowsDefault, PowerContext.AC))
                    return i;
        }

        return ComboBoxConstants.CustomStateIndex;
    }

    // A RegTarget's key already IS its ValueName ?? "KeyExists"; a PowerCfgTarget's "Power" key re-keys to "PowerCfgValue".
    private static string ReadKeyForTarget(Setting setting, string targetKey)
        => setting.Targets.FirstOrDefault(t => t.Key == targetKey) is PowerCfgTarget ? "PowerCfgValue" : targetKey;


}

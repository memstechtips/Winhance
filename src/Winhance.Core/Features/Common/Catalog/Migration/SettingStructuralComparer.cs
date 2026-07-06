using System;
using System.Collections.Generic;
using System.Linq;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: deep structural comparison of two Settings. Records compare their
/// list/dictionary/object members by reference, so this walks them explicitly. Returns the field paths that
/// differ (empty list = structurally equal). Used to prove a hand-authored catalog Setting equals the
/// converter's output for the same old definition. Deleted once the migration is complete.</summary>
public static class SettingStructuralComparer
{
    public static IReadOnlyList<string> Diff(Setting a, Setting b)
    {
        var d = new List<string>();

        if (a.Id != b.Id) d.Add($"Id: {a.Id} != {b.Id}");
        if (!a.Display.Equals(b.Display)) d.Add($"Display: {a.Display} != {b.Display}");          // scalar/record members -> structural
        if (a.UiParentId != b.UiParentId) d.Add($"UiParentId: {a.UiParentId} != {b.UiParentId}");
        if (!a.Apply.Equals(b.Apply)) d.Add($"Apply: {a.Apply} != {b.Apply}");                    // RestartTarget records compare structurally

        DiffDetector(a.Detector, b.Detector, d);
        DiffOptionSource(a.OptionSource, b.OptionSource, d);
        DiffSequence(a.Contexts, b.Contexts, "Contexts", d);
        DiffAvailability(a.Availability, b.Availability, d);
        DiffTargets(a.Targets, b.Targets, d);
        DiffStates(a.States, b.States, d);
        DiffEffects(a.Effects, b.Effects, d);
        DiffNumeric(a.Numeric, b.Numeric, d);

        return d;
    }

    private static void DiffDetector(IStateDetector? a, IStateDetector? b, List<string> d)
    {
        if ((a is null) != (b is null)) { d.Add("Detector nullness differs"); return; }
        if (a is null) return;
        if (a.GetType() != b!.GetType()) { d.Add("Detector type differs"); return; }

        // Detectors are classes with injected config (labels, the DNS IP->label map); GetType() alone would
        // miss a wrong label or map, so compare each known detector's config explicitly.
        switch (a)
        {
            case SystemTrayDetector ta when b is SystemTrayDetector tb:
                if (ta.ShowAllLabel != tb.ShowAllLabel || ta.HideAllLabel != tb.HideAllLabel)
                    d.Add("Detector(SystemTray) labels differ");
                break;
            case SystemRestoreDetector ra when b is SystemRestoreDetector rb:
                if (ra.EnabledLabel != rb.EnabledLabel || ra.DisabledLabel != rb.DisabledLabel)
                    d.Add("Detector(SystemRestore) labels differ");
                break;
            case DnsServerDetector da when b is DnsServerDetector db:
                if (da.AutomaticLabel != db.AutomaticLabel || !DictEqual(da.PrimaryIpToLabel, db.PrimaryIpToLabel))
                    d.Add("Detector(DnsServer) config differs");
                break;
            case PowerPlanDetector when b is PowerPlanDetector:
                break;
            case UpdatePolicyDetector ua when b is UpdatePolicyDetector ub:
                if (ua.DefaultLabel != ub.DefaultLabel || ua.DeferLabel != ub.DeferLabel
                    || ua.PausedLabel != ub.PausedLabel || ua.DisabledLabel != ub.DisabledLabel)
                    d.Add("Detector(UpdatePolicy) labels differ");
                break;
            default:
                d.Add("Detector type not structurally compared (unknown detector)");
                break;
        }
    }

    // Dynamic-option sources are config-free markers (the runtime enumeration lives in the wired-in source), so a
    // null-ness + type comparison is sufficient - mirrors how config-free detectors compare.
    private static void DiffOptionSource(IDynamicOptionSource? a, IDynamicOptionSource? b, List<string> d)
    {
        if ((a is null) != (b is null)) { d.Add("OptionSource nullness differs"); return; }
        if (a is null) return;
        if (a.GetType() != b!.GetType()) d.Add("OptionSource type differs");
    }

    private static void DiffAvailability(Availability a, Availability b, List<string> d)
    {
        if (a.Builds.Count != b.Builds.Count) { d.Add($"Availability.Builds count {a.Builds.Count} != {b.Builds.Count}"); return; }
        for (int i = 0; i < a.Builds.Count; i++)
            if (!a.Builds[i].Equals(b.Builds[i])) d.Add($"Availability.Builds[{i}]: {a.Builds[i]} != {b.Builds[i]}");

        if (!a.Hardware.SequenceEqual(b.Hardware)) d.Add("Availability.Hardware differ");
        if (a.RequiresAdvancedUnlock != b.RequiresAdvancedUnlock) d.Add("Availability.RequiresAdvancedUnlock differs");
        if (a.ValidatesExistence != b.ValidatesExistence) d.Add("Availability.ValidatesExistence differs");
    }

    private static void DiffNumeric(Numeric? a, Numeric? b, List<string> d)
    {
        if ((a is null) != (b is null)) { d.Add("Numeric nullness differs"); return; }
        if (a is null) return;
        if (a.Min != b!.Min) d.Add($"Numeric.Min {a.Min} != {b.Min}");
        if (a.Max != b.Max) d.Add($"Numeric.Max {a.Max} != {b.Max}");
        if (a.Units != b.Units) d.Add($"Numeric.Units {a.Units} != {b.Units}");
        if (!a.Recommended.SequenceEqual(b.Recommended)) d.Add("Numeric.Recommended differ");
        if (!a.WindowsDefault.SequenceEqual(b.WindowsDefault)) d.Add("Numeric.WindowsDefault differ");
    }

    private static void DiffTargets(IReadOnlyList<Target> a, IReadOnlyList<Target> b, List<string> d)
    {
        if (a.Count != b.Count) { d.Add($"Targets count {a.Count} != {b.Count}"); return; }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].GetType() != b[i].GetType()) { d.Add($"Targets[{i}] type differs"); continue; }
            if (a[i].Key != b[i].Key) d.Add($"Targets[{i}].Key {a[i].Key} != {b[i].Key}");
            if (!a[i].AppliesTo.SequenceEqual(b[i].AppliesTo)) d.Add($"Targets[{i}].AppliesTo differs");

            if (a[i] is RegTarget ra && b[i] is RegTarget rb && RegAttrsDiffer(ra, rb))
                d.Add($"Targets[{i}] reg attributes differ");

            if (a[i] is TaskTarget ta && b[i] is TaskTarget tb && ta.TaskPath != tb.TaskPath)
                d.Add($"Targets[{i}].TaskPath differs");

            if (a[i] is PowerCfgTarget pa && b[i] is PowerCfgTarget pb)
            {
                bool keyDiffers =
                    (pa.EnablementKey is null) != (pb.EnablementKey is null)
                    || (pa.EnablementKey is { } ea && pb.EnablementKey is { } eb
                        && (ea.Key != eb.Key || !ea.AppliesTo.SequenceEqual(eb.AppliesTo) || RegAttrsDiffer(ea, eb)));
                if (pa.SubgroupGuid != pb.SubgroupGuid || pa.SettingGuid != pb.SettingGuid
                    || pa.Mode != pb.Mode || pa.Units != pb.Units
                    || pa.CheckForHardwareControl != pb.CheckForHardwareControl || keyDiffers)
                    d.Add($"Targets[{i}] powercfg attributes differ");
            }
        }
    }

    private static bool RegAttrsDiffer(RegTarget a, RegTarget b)
        => !a.Paths.SequenceEqual(b.Paths)
           || a.ValueName != b.ValueName || a.Type != b.Type || a.ByteIndex != b.ByteIndex
           || a.BitMask != b.BitMask || a.ByteOnly != b.ByteOnly || a.CompositeStringKey != b.CompositeStringKey
           || a.PerNetworkInterface != b.PerNetworkInterface || a.PerMonitor != b.PerMonitor
           || a.IsGroupPolicy != b.IsGroupPolicy || a.LockWhenValue != b.LockWhenValue;

    private static void DiffStates(IReadOnlyList<SettingState> a, IReadOnlyList<SettingState> b, List<string> d)
    {
        if (a.Count != b.Count) { d.Add($"States count {a.Count} != {b.Count}"); return; }
        for (int i = 0; i < a.Count; i++)
        {
            var (x, y) = (a[i], b[i]);
            if (x.Label != y.Label) d.Add($"States[{i}].Label {x.Label} != {y.Label}");
            if (x.Tooltip != y.Tooltip) d.Add($"States[{i}].Tooltip {x.Tooltip} != {y.Tooltip}");
            if (x.IsFallback != y.IsFallback) d.Add($"States[{i}].IsFallback differs");
            if (!x.Roles.SequenceEqual(y.Roles)) d.Add($"States[{i}].Roles differ");
            if (!x.Effects.SequenceEqual(y.Effects)) d.Add($"States[{i}].Effects differ");           // Effect records -> structural
            DiffSequence(x.Links, y.Links, $"States[{i}].Links", d);                                 // Link record -> structural (moved per-state, Phase 6.6)
            DiffControls(x.Controls, y.Controls, i, d);
            DiffSet(x.Set, y.Set, i, "Set", d);
            DiffResetSet(x.ResetSet, y.ResetSet, i, d);
        }
    }

    private static void DiffEffects(IReadOnlyList<Effect> a, IReadOnlyList<Effect> b, List<string> d)
    {
        if (a.Count != b.Count) { d.Add($"Effects count {a.Count} != {b.Count}"); return; }
        for (int i = 0; i < a.Count; i++)
        {
            // RegistryWriteEffect carries an object-typed Value; for a byte[] that is REG_BINARY content,
            // record equality would compare by reference. Compare it field-wise with the value-content comparer.
            if (a[i] is RegistryWriteEffect ra && b[i] is RegistryWriteEffect rb)
            {
                if (ra.Path != rb.Path || ra.ValueName != rb.ValueName || ra.Kind != rb.Kind
                    || ra.IsGroupPolicy != rb.IsGroupPolicy
                    || !CatalogValueComparer.AreEqual(ra.Value, rb.Value))
                    d.Add($"Effects[{i}] RegistryWriteEffect differs");
            }
            else if (!a[i].Equals(b[i]))   // ScriptEffect / RegContentEffect / NativePowerEffect: record equality is correct
            {
                d.Add($"Effects[{i}] differs");
            }
        }
    }

    private static void DiffControls(IReadOnlyDictionary<string, string>? a, IReadOnlyDictionary<string, string>? b, int i, List<string> d)
    {
        if ((a is null) != (b is null)) { d.Add($"States[{i}].Controls nullness differs"); return; }
        if (a is null) return;
        if (!DictEqual(a, b!)) d.Add($"States[{i}].Controls differ");
    }

    private static void DiffSet(IReadOnlyDictionary<string, StateValue> a, IReadOnlyDictionary<string, StateValue> b, int i, string field, List<string> d)
    {
        if (a.Count != b.Count) { d.Add($"States[{i}].{field} count {a.Count} != {b.Count}"); return; }
        foreach (var (k, av) in a)
        {
            if (!b.TryGetValue(k, out var bv)) { d.Add($"States[{i}].{field} missing key {k}"); continue; }
            if (av.AcceptsAbsent != bv.AcceptsAbsent
                || av.AcceptsAnyPresent != bv.AcceptsAnyPresent
                || av.DeleteOnWrite != bv.DeleteOnWrite
                || !CatalogValueComparer.AreEqual(av.WritePayload, bv.WritePayload)
                || av.AcceptedValues.Count != bv.AcceptedValues.Count
                || av.AcceptedValues.Where((val, idx) => !CatalogValueComparer.AreEqual(val, bv.AcceptedValues[idx])).Any())
                d.Add($"States[{i}].{field}[{k}] StateValue differs");
        }
    }

    /// <summary>Compares the optional per-state reset-write override (Phase 6.4b 3A). Null on both = equal; a
    /// nullness mismatch is a diff; otherwise the dictionaries are compared key-by-key like a Set.</summary>
    private static void DiffResetSet(IReadOnlyDictionary<string, StateValue>? a, IReadOnlyDictionary<string, StateValue>? b, int i, List<string> d)
    {
        if ((a is null) != (b is null)) { d.Add($"States[{i}].ResetSet nullness differs"); return; }
        if (a is null) return;
        DiffSet(a, b!, i, "ResetSet", d);
    }

    private static bool DictEqual(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
        => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    private static void DiffSequence<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, string name, List<string> d)
    {
        if (!a.SequenceEqual(b)) d.Add($"{name} differ");
    }
}

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
        if (a.Detector?.GetType() != b.Detector?.GetType()) d.Add("Detector type differs");
        if (!a.Apply.Equals(b.Apply)) d.Add($"Apply: {a.Apply} != {b.Apply}");                    // RestartTarget records compare structurally

        DiffSequence(a.Contexts, b.Contexts, "Contexts", d);
        DiffSequence(a.Links, b.Links, "Links", d);                                                // Link record -> structural
        DiffAvailability(a.Availability, b.Availability, d);
        DiffTargets(a.Targets, b.Targets, d);
        DiffStates(a.States, b.States, d);

        return d;
    }

    private static void DiffAvailability(Availability a, Availability b, List<string> d)
    {
        if (a.Builds.Count != b.Builds.Count) { d.Add($"Availability.Builds count {a.Builds.Count} != {b.Builds.Count}"); return; }
        for (int i = 0; i < a.Builds.Count; i++)
            if (!a.Builds[i].Equals(b.Builds[i])) d.Add($"Availability.Builds[{i}]: {a.Builds[i]} != {b.Builds[i]}");
    }

    private static void DiffTargets(IReadOnlyList<Target> a, IReadOnlyList<Target> b, List<string> d)
    {
        if (a.Count != b.Count) { d.Add($"Targets count {a.Count} != {b.Count}"); return; }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].GetType() != b[i].GetType()) { d.Add($"Targets[{i}] type differs"); continue; }
            if (a[i].Key != b[i].Key) d.Add($"Targets[{i}].Key {a[i].Key} != {b[i].Key}");
            if (!a[i].AppliesTo.SequenceEqual(b[i].AppliesTo)) d.Add($"Targets[{i}].AppliesTo differs");

            if (a[i] is RegTarget ra && b[i] is RegTarget rb)
            {
                if (!ra.Paths.SequenceEqual(rb.Paths)) d.Add($"Targets[{i}].Paths differs");
                if (ra.ValueName != rb.ValueName || ra.Type != rb.Type || ra.ByteIndex != rb.ByteIndex
                    || ra.BitMask != rb.BitMask || ra.ByteOnly != rb.ByteOnly || ra.CompositeStringKey != rb.CompositeStringKey
                    || ra.PerNetworkInterface != rb.PerNetworkInterface || ra.PerMonitor != rb.PerMonitor
                    || ra.IsGroupPolicy != rb.IsGroupPolicy || ra.LockKeyAccess != rb.LockKeyAccess)
                    d.Add($"Targets[{i}] reg attributes differ");
            }
            if (a[i] is TaskTarget ta && b[i] is TaskTarget tb && ta.TaskPath != tb.TaskPath)
                d.Add($"Targets[{i}].TaskPath differs");
        }
    }

    private static void DiffStates(IReadOnlyList<SettingState> a, IReadOnlyList<SettingState> b, List<string> d)
    {
        if (a.Count != b.Count) { d.Add($"States count {a.Count} != {b.Count}"); return; }
        for (int i = 0; i < a.Count; i++)
        {
            var (x, y) = (a[i], b[i]);
            if (x.Label != y.Label) d.Add($"States[{i}].Label {x.Label} != {y.Label}");
            if (x.IsFallback != y.IsFallback) d.Add($"States[{i}].IsFallback differs");
            if (!x.Roles.SequenceEqual(y.Roles)) d.Add($"States[{i}].Roles differ");
            if (!x.Effects.SequenceEqual(y.Effects)) d.Add($"States[{i}].Effects differ");           // Effect records -> structural
            DiffControls(x.Controls, y.Controls, i, d);
            DiffSet(x.Set, y.Set, i, d);
        }
    }

    private static void DiffControls(IReadOnlyDictionary<string, string>? a, IReadOnlyDictionary<string, string>? b, int i, List<string> d)
    {
        if ((a is null) != (b is null)) { d.Add($"States[{i}].Controls nullness differs"); return; }
        if (a is null) return;
        if (a.Count != b!.Count || a.Any(kv => !b.TryGetValue(kv.Key, out var v) || v != kv.Value))
            d.Add($"States[{i}].Controls differ");
    }

    private static void DiffSet(IReadOnlyDictionary<string, StateValue> a, IReadOnlyDictionary<string, StateValue> b, int i, List<string> d)
    {
        if (a.Count != b.Count) { d.Add($"States[{i}].Set count {a.Count} != {b.Count}"); return; }
        foreach (var (k, av) in a)
        {
            if (!b.TryGetValue(k, out var bv)) { d.Add($"States[{i}].Set missing key {k}"); continue; }
            if (av.AcceptsAbsent != bv.AcceptsAbsent
                || av.AcceptsAnyPresent != bv.AcceptsAnyPresent
                || av.DeleteOnWrite != bv.DeleteOnWrite
                || !CatalogValueComparer.AreEqual(av.WritePayload, bv.WritePayload)
                || av.AcceptedValues.Count != bv.AcceptedValues.Count
                || av.AcceptedValues.Where((val, idx) => !CatalogValueComparer.AreEqual(val, bv.AcceptedValues[idx])).Any())
                d.Add($"States[{i}].Set[{k}] StateValue differs");
        }
    }

    private static void DiffSequence<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, string name, List<string> d)
    {
        if (!a.SequenceEqual(b)) d.Add($"{name} differ");
    }
}

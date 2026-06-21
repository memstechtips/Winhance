using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: proves the new <see cref="ApplyPlanBuilder"/> produces the same registry
/// WRITE INTENT the old live apply (<c>WindowsRegistryService.ApplySetting</c>) does, for a setting + target
/// state. Pure - both sides are computed without touching the registry, then compared as a normalised set of
/// write-intent strings. First slice: PLAIN registry toggles (value set/delete + key existence). Binary
/// (bit/byte), composite, per-NIC/monitor, and apply-only effects are excluded here and handled in later
/// slices. Deleted once the migration is complete.</summary>
public static class ApplyEquivalenceHarness
{
    /// <summary>A plain registry toggle whose apply is a pure value/key write - no binary, composite,
    /// per-subkey, effect, or non-registry mechanism. The cleanest first apply slice.</summary>
    public static bool IsPlainRegistryToggleForApply(SettingDefinition def)
    {
        if (!RegistryToggleEquivalenceHarness.IsPureRegistryToggle(def))
            return false;
        // Apply-only effects are a separate slice (the old apply also runs the script/.reg/native write).
        if (def.PowerShellScripts.Count > 0 || def.RegContents.Count > 0 || def.NativePowerApiSettings.Count > 0)
            return false;
        // These write paths are binary-surgical, read-merge, or need live subkey enumeration - later slices.
        return def.RegistrySettings.All(r =>
            !r.ApplyPerNetworkInterface
            && !r.ApplyPerMonitor
            && r.CompositeStringKey == null
            && !r.BitMask.HasValue
            && !r.ModifyByteOnly);
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per (toggle, state): OLD is the live apply's write
    /// intent (mirrored from ApplySetting's plain branches), NEW is the ApplyPlanBuilder plan, both normalised.
    /// Callers should pre-filter with <see cref="IsPlainRegistryToggleForApply"/>.</summary>
    public static IReadOnlyList<EquivalenceRow> RunToggleApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsPlainRegistryToggleForApply(def))
                continue;

            var setting = SettingDefinitionConverter.ConvertToggle(def);

            foreach (var (label, isEnabled) in new[] { ("Enabled", true), ("Disabled", false) })
            {
                var oldWrites = def.RegistrySettings
                    .SelectMany(rs => OldApplyWrite(rs, isEnabled, specificValue: null))
                    .OrderBy(s => s).ToList();

                var newWrites = NewWrites(ApplyPlanBuilder.Build(setting, label))
                    .OrderBy(s => s).ToList();

                bool match = oldWrites.SequenceEqual(newWrites);
                rows.Add(new EquivalenceRow(
                    $"{def.Id} [{label}]",
                    string.Join(" | ", oldWrites),
                    string.Join(" | ", newWrites),
                    match));
            }
        }

        return rows;
    }

    /// <summary>A plain registry selection whose apply is pure value writes - no key-existence, binary,
    /// composite, per-subkey, effect, or non-registry mechanism. The cleanest selection apply slice.</summary>
    public static bool IsPlainRegistrySelectionForApply(SettingDefinition def)
    {
        if (!RegistryToggleEquivalenceHarness.IsPureRegistrySelection(def))
            return false;
        if (def.PowerShellScripts.Count > 0 || def.RegContents.Count > 0 || def.NativePowerApiSettings.Count > 0)
            return false;
        return def.RegistrySettings.All(r =>
            r.ValueName != null
            && !r.ApplyPerNetworkInterface
            && !r.ApplyPerMonitor
            && r.CompositeStringKey == null
            && !r.BitMask.HasValue
            && !r.ModifyByteOnly);
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per (selection, option). OLD mirrors the live selection
    /// apply (SettingOperationExecutor: per registry setting, ApplySetting(rs, true, optionValue) when the option
    /// maps that ValueName, else ApplySetting(rs, false)); NEW is the ApplyPlanBuilder plan for that option's
    /// state. Both normalised. Callers should pre-filter with <see cref="IsPlainRegistrySelectionForApply"/>.</summary>
    public static IReadOnlyList<EquivalenceRow> RunSelectionApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsPlainRegistrySelectionForApply(def))
                continue;

            var setting = SettingDefinitionConverter.ConvertSelection(def);
            var options = def.ComboBox!.Options;

            foreach (var opt in options)
            {
                var mapped = opt.ValueMappings ?? EmptyValues;

                var oldWrites = def.RegistrySettings.SelectMany(rs =>
                {
                    var key = rs.ValueName ?? "KeyExists";
                    object? specificValue = mapped.TryGetValue(key, out var v) ? v : null;
                    // Live apply: a mapped non-null value -> ApplySetting(rs, true, value); otherwise
                    // ApplySetting(rs, false) (which, for a selection, deletes - DisabledValue is unset).
                    return OldApplyWrite(rs, isEnabled: specificValue != null, specificValue);
                }).OrderBy(s => s).ToList();

                var newWrites = NewWrites(ApplyPlanBuilder.Build(setting, opt.DisplayName))
                    .OrderBy(s => s).ToList();

                bool match = oldWrites.SequenceEqual(newWrites);
                rows.Add(new EquivalenceRow(
                    $"{def.Id} [{opt.DisplayName}]",
                    string.Join(" | ", oldWrites),
                    string.Join(" | ", newWrites),
                    match));
            }
        }

        return rows;
    }

    private static readonly Dictionary<string, object?> EmptyValues = new();

    /// <summary>The old live apply's write intent for one plain registry setting, mirroring the relevant
    /// branches of <c>WindowsRegistryService.ApplySetting(setting, isEnabled, specificValue)</c> WITHOUT
    /// executing. <paramref name="specificValue"/> overrides the Enabled/DisabledValue (selection apply path).</summary>
    private static IEnumerable<string> OldApplyWrite(RegistrySetting rs, bool isEnabled, object? specificValue)
    {
        // ValueName == null: state is key existence (create on enable, delete on disable).
        if (rs.ValueName == null)
        {
            yield return isEnabled ? $"CREATEKEY {rs.KeyPath}" : $"DELETEKEY {rs.KeyPath}";
            yield break;
        }

        // Plain value: a specific value (selection) wins; else the enabled/disabled value. Null -> delete.
        var valueToSet = specificValue ?? GetWriteValue(isEnabled ? rs.EnabledValue : rs.DisabledValue);
        yield return valueToSet == null
            ? $"DELETE {rs.KeyPath}\\{rs.ValueName}"
            : $"SET {rs.KeyPath}\\{rs.ValueName} = {Format(valueToSet)} ({rs.ValueType})";
    }

    /// <summary>The new plan's write intent, normalised to the same strings as the old side.</summary>
    private static IEnumerable<string> NewWrites(IReadOnlyList<ApplyOp> plan)
    {
        foreach (var op in plan)
        {
            switch (op)
            {
                case RegistryWriteOp w:
                    yield return $"SET {w.Path}\\{w.Target.ValueName} = {Format(w.Value)} ({w.Target.Type})";
                    break;
                case RegistryDeleteOp d:
                    yield return d.Target.ValueName == null
                        ? $"DELETEKEY {d.Path}"
                        : $"DELETE {d.Path}\\{d.Target.ValueName}";
                    break;
                case RegistryEnsureKeyOp e:
                    yield return $"CREATEKEY {e.Path}";
                    break;
                // TaskSetOp / EffectOp do not occur for a plain registry toggle (filtered out).
            }
        }
    }

    /// <summary>The first non-null entry of an old EnabledValue/DisabledValue array - matches the old apply's
    /// <c>GetWriteValue</c>.</summary>
    private static object? GetWriteValue(object?[]? values) => values?.FirstOrDefault(v => v != null);

    private static string Format(object value) => value switch
    {
        byte[] bytes => System.Convert.ToHexString(bytes),   // compare REG_BINARY by content, not "System.Byte[]"
        System.IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: proves the new <see cref="ApplyPlanBuilder"/> produces the same registry
/// WRITE INTENT the old live apply (<c>WindowsRegistryService.ApplySetting</c>) does, for a setting + target
/// state. Pure - both sides are computed without touching the registry, then compared as a normalised set of
/// write-intent strings. Covers registry toggles and selections including binary bit/byte surgical writes,
/// composite packed-string sub-key writes, and apply-only effects (SCRIPT / REGIMPORT / NATIVEPOWER). A setting
/// that applies via a .reg import skips its registry writes (detect-only targets), mirroring the old apply.
/// Per-NIC/monitor (live subkey enumeration) is excluded here and handled in a later slice. Deleted once the
/// migration is complete.</summary>
public static class ApplyEquivalenceHarness
{
    /// <summary>A registry toggle whose apply is a self-contained registry write - a value set/delete, a
    /// key-existence create/delete, a surgical binary bit/byte edit, a composite packed-string sub-key write,
    /// and/or apply-only effects (script / .reg import / native power). Per-subkey (per-NIC/monitor) needs a
    /// live context and is excluded (later slice).</summary>
    public static bool IsPlainRegistryToggleForApply(SettingDefinition def)
    {
        if (!RegistryToggleEquivalenceHarness.IsPureRegistryToggle(def))
            return false;
        // Per-subkey enumeration needs a live context - later slice. Binary bit/byte, composite sub-key writes,
        // and apply-only effects are covered here (BITSET/BYTESET/COMPOSITESET + SCRIPT/REGIMPORT/NATIVEPOWER).
        return def.RegistrySettings.All(r =>
            !r.ApplyPerNetworkInterface
            && !r.ApplyPerMonitor);
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
                // A setting that applies via a .reg import does NOT write its registry targets (mirrors the old
                // apply, which skips registry writes when RegContents is present); the import is the apply.
                var oldRegWrites = def.RegContents.Count == 0
                    ? def.RegistrySettings.SelectMany(rs => OldApplyWrite(rs, isEnabled, specificValue: null))
                    : Enumerable.Empty<string>();

                var oldWrites = oldRegWrites
                    .Concat(OldEffectWrites(def, isEnabled))
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

    /// <summary>A registry selection whose apply is self-contained registry writes - value sets and surgical
    /// binary bit/byte edits (every target has a ValueName). Composite (read-merge), per-subkey, apply-only
    /// effect, and non-registry mechanisms are excluded (later slices).</summary>
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
            && r.CompositeStringKey == null);
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

        // Composite: set or remove one sub-key inside the packed ";"-string (mirrors ApplySetting's
        // CompositeStringKey branch). A specific value (selection) wins; else the enabled/disabled value;
        // a null value removes the sub-key.
        if (rs.CompositeStringKey != null)
        {
            var subValue = specificValue?.ToString()
                ?? (isEnabled ? GetWriteValue(rs.EnabledValue)?.ToString() : GetWriteValue(rs.DisabledValue)?.ToString());
            yield return subValue != null
                ? $"COMPOSITESET {rs.KeyPath}\\{rs.ValueName}[{rs.CompositeStringKey}] = {subValue}"
                : $"COMPOSITEDEL {rs.KeyPath}\\{rs.ValueName}[{rs.CompositeStringKey}]";
            yield break;
        }

        // Binary bit: surgical set/clear of one bit (mirrors ApplySetting's ModifyBinaryBit branch). A bit
        // value (bool / int!=0 / byte!=0) from a selection wins; a plain toggle uses isEnabled.
        if (rs.BitMask.HasValue && rs.BinaryByteIndex.HasValue)
        {
            bool setBit = specificValue switch
            {
                bool b => b,
                int i => i != 0,
                byte b => b != 0,
                _ => isEnabled,
            };
            yield return $"BITSET {rs.KeyPath}\\{rs.ValueName}[{rs.BinaryByteIndex.Value}] mask=0x{rs.BitMask.Value:X2} set={setBit}";
            yield break;
        }

        // Binary byte: surgical overwrite of one byte (mirrors ApplySetting's ModifyByteOnly branch). A
        // specific value (selection) wins; else the enabled/disabled value, coerced to a byte.
        if (rs.ModifyByteOnly && rs.BinaryByteIndex.HasValue)
        {
            byte byteValue = specificValue switch
            {
                byte b => b,
                int i => (byte)i,
                _ => ToByte(GetWriteValue(isEnabled ? rs.EnabledValue : rs.DisabledValue)),
            };
            yield return $"BYTESET {rs.KeyPath}\\{rs.ValueName}[{rs.BinaryByteIndex.Value}] = 0x{byteValue:X2}";
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
                case RegistryBitSetOp b:
                    yield return $"BITSET {b.Path}\\{b.Target.ValueName}[{b.ByteIndex}] mask=0x{b.BitMask:X2} set={b.Set}";
                    break;
                case RegistryByteSetOp y:
                    yield return $"BYTESET {y.Path}\\{y.Target.ValueName}[{y.ByteIndex}] = 0x{y.Value:X2}";
                    break;
                case RegistryCompositeSetOp c:
                    yield return c.SubValue != null
                        ? $"COMPOSITESET {c.Path}\\{c.Target.ValueName}[{c.CompositeKey}] = {c.SubValue}"
                        : $"COMPOSITEDEL {c.Path}\\{c.Target.ValueName}[{c.CompositeKey}]";
                    break;
                case EffectOp fx:
                    yield return EffectIntent(fx.Effect);
                    break;
                // TaskSetOp does not occur for a registry toggle/selection (filtered out).
            }
        }
    }

    /// <summary>The old apply's effect intent for one setting + state, mirroring the script / .reg / native-power
    /// branches of <c>SettingOperationExecutor</c>. Scripts and .reg imports only run when their body is non-empty
    /// (old guards with IsNullOrEmpty); native power always runs.</summary>
    private static IEnumerable<string> OldEffectWrites(SettingDefinition def, bool isEnabled)
    {
        foreach (var ps in def.PowerShellScripts)
        {
            var script = isEnabled ? ps.EnabledScript : ps.DisabledScript;
            if (!string.IsNullOrEmpty(script))
                yield return EffectIntent(new ScriptEffect(script!, ps.RunContext));
        }

        foreach (var rc in def.RegContents)
        {
            var content = isEnabled ? rc.EnabledContent : rc.DisabledContent;
            if (!string.IsNullOrEmpty(content))
                yield return EffectIntent(new RegContentEffect(content!));
        }

        foreach (var np in def.NativePowerApiSettings)
            yield return EffectIntent(new NativePowerEffect(np.InformationLevel, isEnabled ? np.EnabledValue : np.DisabledValue));
    }

    /// <summary>One effect's apply intent, normalised identically for the old and new sides.</summary>
    private static string EffectIntent(Effect effect) => effect switch
    {
        ScriptEffect s => $"SCRIPT run={s.Run} {s.Script}",
        RegContentEffect r => $"REGIMPORT {r.Content}",
        NativePowerEffect n => $"NATIVEPOWER level={n.InformationLevel} value={n.Value}",
        _ => $"EFFECT {effect}",
    };

    /// <summary>The first non-null entry of an old EnabledValue/DisabledValue array - matches the old apply's
    /// <c>GetWriteValue</c>.</summary>
    private static object? GetWriteValue(object?[]? values) => values?.FirstOrDefault(v => v != null);

    /// <summary>Coerces an old enabled/disabled value to the byte the old ModifyByteOnly branch would write
    /// (byte as-is, int truncated, anything else 0).</summary>
    private static byte ToByte(object? value) => value switch
    {
        byte b => b,
        int i => (byte)i,
        _ => (byte)0,
    };

    private static string Format(object value) => value switch
    {
        byte[] bytes => System.Convert.ToHexString(bytes),   // compare REG_BINARY by content, not "System.Byte[]"
        System.IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}

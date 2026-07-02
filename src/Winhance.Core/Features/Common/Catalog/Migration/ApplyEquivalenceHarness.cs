using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: proves the new <see cref="ApplyPlanBuilder"/> produces the same registry
/// WRITE INTENT the old live apply (<c>WindowsRegistryService.ApplySetting</c>) does, for a setting + target
/// state. Pure - both sides are computed without touching the registry, then compared as a normalised set of
/// write-intent strings. Covers EVERY registry toggle/selection apply mechanism: value set/delete, key existence,
/// binary bit/byte surgical writes, composite packed-string sub-key writes, per-NIC/per-monitor "write each
/// sub-key" expansion, and apply-only effects (SCRIPT / REGIMPORT / NATIVEPOWER). A setting that applies via a
/// .reg import skips its registry writes (detect-only targets), mirroring the old apply. The live sub-key
/// enumeration for composite/per-subkey defers to the writer; the harness compares the deferred INTENT. Deleted
/// once the migration is complete.</summary>
public static class ApplyEquivalenceHarness
{
    /// <summary>Any pure registry toggle qualifies for the apply comparison - every toggle apply mechanism is now
    /// handled (value set/delete, key existence, binary bit/byte, composite sub-key, per-NIC/per-monitor, and
    /// apply-only effects). Non-registry DETECTION (combobox, powercfg, scheduled-task, custom detector) is gated
    /// out by <see cref="RegistryToggleEquivalenceHarness.IsPureRegistryToggle"/>.</summary>
    public static bool IsPlainRegistryToggleForApply(SettingDefinition def) =>
        RegistryToggleEquivalenceHarness.IsPureRegistryToggle(def);

    /// <summary>A powercfg SELECTION (ComboBox over the AC value index) whose apply writes the chosen option's
    /// value to both AC and DC. Reuses the detection predicate - the apply population is the same set.</summary>
    public static bool IsPlainPowerCfgSelectionForApply(SettingDefinition def) =>
        RegistryToggleEquivalenceHarness.IsPurePowerCfgSelection(def);

    /// <summary>A powercfg NUMERIC (slider) whose apply writes the per-context display value (converted to system
    /// units) to each context. Reuses the detection predicate - the apply population is the same set.</summary>
    public static bool IsPlainPowerCfgNumericForApply(SettingDefinition def) =>
        RegistryToggleEquivalenceHarness.IsPurePowerCfgNumeric(def);

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

    /// <summary>Builds one <see cref="EquivalenceRow"/> per PLAIN registry toggle that has a Windows-default
    /// direction: OLD is the live RESET apply's write intent (the old reset routes to the default direction -
    /// a normal enabled write for a default-ON toggle, or a <c>useDefaultValue:true</c> disabled write, which
    /// substitutes GetParentDisableValue for the plain-value branch - and runs the default direction's effects),
    /// NEW is the ApplyPlanBuilder plan with <c>reset:true</c> over the WindowsDefault state label. Both normalised.
    /// A toggle with no Windows-default direction (GetDefaultToggleState == null) is skipped - the new resolver
    /// also returns null there and the reset stays on the old apply path. Callers should pre-filter with
    /// <see cref="IsPlainRegistryToggleForApply"/>.</summary>
    public static IReadOnlyList<EquivalenceRow> RunResetApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsPlainRegistryToggleForApply(def))
                continue;

            // The reset only routes to the new engine when the setting has a Windows-default direction; otherwise
            // the resolver returns null (-> old apply) and there is no default label to build. Skip those here too.
            var defaultEnabled = SettingDefinitionToggleState.GetDefaultToggleState(def);
            if (defaultEnabled is null)
                continue;

            var setting = SettingDefinitionConverter.ConvertToggle(def);
            var label = defaultEnabled.Value ? "Enabled" : "Disabled";

            // A setting that applies via a .reg import does NOT write its registry targets (mirrors the old apply,
            // which skips registry writes when RegContents is present); the import is the apply.
            var oldRegWrites = def.RegContents.Count == 0
                ? def.RegistrySettings.SelectMany(rs => OldResetWrite(rs, defaultEnabled.Value))
                : Enumerable.Empty<string>();

            // The reset runs the DEFAULT direction's effects (script / .reg / native-power for that direction).
            var oldWrites = oldRegWrites
                .Concat(OldEffectWrites(def, isEnabled: defaultEnabled.Value))
                .OrderBy(s => s).ToList();

            var newWrites = NewWrites(ApplyPlanBuilder.Build(setting, label, build: null, reset: true))
                .OrderBy(s => s).ToList();

            bool match = oldWrites.SequenceEqual(newWrites);
            rows.Add(new EquivalenceRow(
                $"{def.Id} [reset]",
                string.Join(" | ", oldWrites),
                string.Join(" | ", newWrites),
                match));
        }

        return rows;
    }

    /// <summary>A registry selection whose apply is self-contained registry writes - value sets and surgical
    /// binary bit/byte edits (every target has a ValueName) - plus optional per-option PowerShell-script effects.
    /// .reg-import / native-power selections (none exist) and per-subkey (per-NIC/monitor) are excluded.</summary>
    public static bool IsPlainRegistrySelectionForApply(SettingDefinition def)
    {
        if (!RegistryToggleEquivalenceHarness.IsPureRegistrySelection(def))
            return false;
        // .reg-import and native-power SELECTIONS are not handled here (the catalog has none; if one is added it
        // needs the regcontent write-skip + native mapping like the toggle slice). Script selections ARE covered.
        if (def.RegContents.Count > 0 || def.NativePowerApiSettings.Count > 0)
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
                })
                    .Concat(OldSelectionEffectWrites(def, opt))
                    .OrderBy(s => s).ToList();

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

    /// <summary>Builds one <see cref="EquivalenceRow"/> per (powercfg selection, option). OLD mirrors the live
    /// powercfg apply (PowerCfgApplier): the option's int PowerCfgValue is written to BOTH the AC and DC value
    /// indices of the active scheme (the battery-gate / value-differs / commit are runtime concerns, not intent).
    /// NEW is the ApplyPlanBuilder plan for that option's state. Both normalised. Callers should pre-filter with
    /// <see cref="IsPlainPowerCfgSelectionForApply"/>.</summary>
    public static IReadOnlyList<EquivalenceRow> RunPowerCfgSelectionApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsPlainPowerCfgSelectionForApply(def))
                continue;

            var setting = SettingDefinitionConverter.ConvertPowerCfg(def);
            var pcs = def.PowerCfgSettings![0];
            var options = def.ComboBox!.Options;

            foreach (var opt in options)
            {
                int v = System.Convert.ToInt32(opt.ValueMappings!["PowerCfgValue"]);

                var oldWrites = new[]
                {
                    $"POWERWRITEAC sub={pcs.SubgroupGuid} setting={pcs.SettingGuid} = {v}",
                    $"POWERWRITEDC sub={pcs.SubgroupGuid} setting={pcs.SettingGuid} = {v}",
                }.OrderBy(s => s).ToList();

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

    /// <summary>Builds one <see cref="EquivalenceRow"/> per (powercfg numeric, quick-set). OLD mirrors the live
    /// powercfg apply (PowerCfgApplier): the RAW system value the catalog stores (RecommendedValueAC/DC or
    /// DefaultValueAC/DC, skipping a null context) is written to that context's value index. NEW is the
    /// ApplyPlanBuilder plan over the converter's display-unit Recommended/WindowsDefault list (which
    /// BuildPowerCfgNumeric rounds back to system units). A match proves the display->system round-trip is
    /// lossless. Both normalised. Callers should pre-filter with <see cref="IsPlainPowerCfgNumericForApply"/>.</summary>
    public static IReadOnlyList<EquivalenceRow> RunPowerCfgNumericApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsPlainPowerCfgNumericForApply(def))
                continue;

            var setting = SettingDefinitionConverter.ConvertPowerCfg(def);
            var pcs = def.PowerCfgSettings![0];

            var quickSets = new (string Label, IReadOnlyList<ContextValue> Values, int? RawAc, int? RawDc)[]
            {
                ("Recommended", setting.Numeric?.Recommended ?? System.Array.Empty<ContextValue>(),
                    pcs.RecommendedValueAC, pcs.RecommendedValueDC),
                ("WindowsDefault", setting.Numeric?.WindowsDefault ?? System.Array.Empty<ContextValue>(),
                    pcs.DefaultValueAC, pcs.DefaultValueDC),
            };

            foreach (var (label, values, rawAc, rawDc) in quickSets)
            {
                var oldList = new List<string>();
                if (rawAc is { } ac)
                    oldList.Add($"POWERWRITEAC sub={pcs.SubgroupGuid} setting={pcs.SettingGuid} = {ac}");
                if (rawDc is { } dc)
                    oldList.Add($"POWERWRITEDC sub={pcs.SubgroupGuid} setting={pcs.SettingGuid} = {dc}");
                var oldWrites = oldList.OrderBy(s => s).ToList();

                var newWrites = NewWrites(ApplyPlanBuilder.BuildPowerCfgNumeric(setting, values))
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

    /// <summary>An Action is a stateless one-shot; the old apply hardcodes enable=true. Eligible when the def is
    /// InputType.Action.</summary>
    public static bool IsActionForApply(SettingDefinition def) => def.InputType == InputType.Action;

    /// <summary>One EquivalenceRow per Action: OLD is the live apply's enabled-branch write intent (each
    /// RegistrySetting at enable=true, plus the enabled effects), NEW is the BuildAction plan, both normalised.
    /// A .reg-import Action would skip its registry writes (none exist today), mirroring the toggle path.</summary>
    public static IReadOnlyList<EquivalenceRow> RunActionApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsActionForApply(def))
                continue;

            var setting = SettingDefinitionConverter.ConvertAction(def);

            var oldRegWrites = def.RegContents.Count == 0
                ? def.RegistrySettings.SelectMany(rs => OldApplyWrite(rs, isEnabled: true, specificValue: null))
                : Enumerable.Empty<string>();

            var oldWrites = oldRegWrites
                .Concat(OldEffectWrites(def, isEnabled: true))
                .OrderBy(s => s).ToList();

            var newWrites = NewWrites(ApplyPlanBuilder.BuildAction(setting))
                .OrderBy(s => s).ToList();

            bool match = oldWrites.SequenceEqual(newWrites);
            rows.Add(new EquivalenceRow(
                def.Id,
                string.Join(" | ", oldWrites),
                string.Join(" | ", newWrites),
                match));
        }

        return rows;
    }

    /// <summary>The system-tray-icons selection (DetectionType.SystemTrayIcons): detection runs via the custom
    /// SystemTrayDetector but APPLY (Phase 6.4b Slice 5) is now the per-option PowerShell script the new engine
    /// runs as an EffectOp. It has no registry targets, so the apply comparison is effects-only.</summary>
    public static bool IsSystemTrayForApply(SettingDefinition def) =>
        def.DetectionType == DetectionType.SystemTrayIcons && def.ComboBox is not null;

    /// <summary>The system-restore toggle (DetectionType.SystemRestore): detection runs via the custom
    /// SystemRestoreDetector but APPLY (Phase 6.4b Slice 5) is now the per-direction PowerShell script the new
    /// engine runs as an EffectOp. It has no registry targets, so the apply comparison is effects-only.</summary>
    public static bool IsSystemRestoreForApply(SettingDefinition def) =>
        def.DetectionType == DetectionType.SystemRestore && def.InputType == InputType.Toggle;

    /// <summary>Builds one <see cref="EquivalenceRow"/> per (system-tray selection, option). These settings carry
    /// NO registry targets - their apply is purely the per-option PowerShell script - so OLD is the live selection
    /// script intent (<see cref="OldSelectionEffectWrites"/>) and NEW is the EffectOp the ApplyPlanBuilder plan
    /// produces for that option's state. Both normalised. Callers should pre-filter with <see cref="IsSystemTrayForApply"/>.</summary>
    public static IReadOnlyList<EquivalenceRow> RunSystemTrayApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsSystemTrayForApply(def))
                continue;

            var setting = SettingDefinitionConverter.ConvertSystemTray(def);
            var options = def.ComboBox!.Options;

            foreach (var opt in options)
            {
                var oldWrites = OldSelectionEffectWrites(def, opt)
                    .OrderBy(s => s).ToList();

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

    /// <summary>Builds one <see cref="EquivalenceRow"/> per (system-restore toggle, direction). These settings
    /// carry NO registry targets - their apply is purely the per-direction PowerShell script - so OLD is the live
    /// toggle script intent (<see cref="OldEffectWrites"/>) and NEW is the EffectOp the ApplyPlanBuilder plan
    /// produces for that state. Both normalised. Callers should pre-filter with <see cref="IsSystemRestoreForApply"/>.</summary>
    public static IReadOnlyList<EquivalenceRow> RunSystemRestoreApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsSystemRestoreForApply(def))
                continue;

            var setting = SettingDefinitionConverter.ConvertSystemRestore(def);

            foreach (var (label, isEnabled) in new[] { ("Enabled", true), ("Disabled", false) })
            {
                var oldWrites = OldEffectWrites(def, isEnabled)
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

    /// <summary>The DNS-server selection (DetectionType.DnsServer): detection runs via the custom DnsServerDetector
    /// but APPLY (Phase 6.4b Slice 6) is now the per-option PowerShell scripts the new engine runs as EffectOps -
    /// the two shared script bodies with the option's {{primary}}/{{secondary}}/{{dohtemplate}} ScriptVariables
    /// substituted. It has no registry targets, so the apply comparison is effects-only.</summary>
    public static bool IsDnsServerForApply(SettingDefinition def) =>
        def.DetectionType == DetectionType.DnsServer && def.ComboBox is not null;

    /// <summary>Builds one <see cref="EquivalenceRow"/> per (DNS-server selection, option). These settings carry NO
    /// registry targets - their apply is purely the per-option PowerShell scripts (with ScriptVariables substituted)
    /// - so OLD is the live selection script intent (<see cref="OldSelectionEffectWrites"/>, which already does the
    /// substitution) and NEW is the EffectOps the ApplyPlanBuilder plan produces for that option's state. Both
    /// normalised. Callers should pre-filter with <see cref="IsDnsServerForApply"/>.</summary>
    public static IReadOnlyList<EquivalenceRow> RunDnsServerApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsDnsServerForApply(def))
                continue;

            var setting = SettingDefinitionConverter.ConvertDnsServer(def);
            var options = def.ComboBox!.Options;

            foreach (var opt in options)
            {
                var oldWrites = OldSelectionEffectWrites(def, opt)
                    .OrderBy(s => s).ToList();

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

    /// <summary>Builds one <see cref="EquivalenceRow"/> per effects-based custom-detector setting whose states carry a
    /// WindowsDefault role (today only the system-restore toggle): OLD is the live executor RESET, which - because the
    /// funnel applies the WindowsDefault DIRECTION on reset (SetToggleToDefaultCommand -> Enable = ToggleDefaultState) -
    /// runs that direction's script; NEW is the ApplyPlanBuilder plan for the WindowsDefault state with reset:true. Both
    /// effects-only (these settings carry no registry targets, so reset:true is inert). A custom-detector setting with
    /// NO WindowsDefault state (system-tray / DNS selections) is skipped: the resolver's reset block returns null for it,
    /// so its reset stays on the old apply and is not part of this migration.</summary>
    public static IReadOnlyList<EquivalenceRow> RunCustomDetectorReset(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            Setting? setting = def.DetectionType switch
            {
                DetectionType.SystemRestore when def.InputType == InputType.Toggle => SettingDefinitionConverter.ConvertSystemRestore(def),
                DetectionType.SystemTrayIcons when def.ComboBox is not null => SettingDefinitionConverter.ConvertSystemTray(def),
                DetectionType.DnsServer when def.ComboBox is not null => SettingDefinitionConverter.ConvertDnsServer(def),
                _ => null,
            };
            if (setting is null)
                continue;

            // Reset routes to the new engine only when a WindowsDefault state exists (the resolver derives the reset
            // target from it). Effects-based detectors with no WindowsDefault role stay on the old apply - skip them.
            var wd = setting.States.FirstOrDefault(s => s.HasRole(RoleKind.WindowsDefault));
            if (wd is null)
                continue;

            // OLD reset = the WindowsDefault direction's effect intent. A toggle (no ComboBox) applies isEnabled =
            // (WindowsDefault label == "Enabled"); a selection applies the option whose DisplayName is the WindowsDefault
            // label (none today - custom-detector selections carry no WindowsDefault - but handled for completeness).
            IEnumerable<string> oldEffects;
            if (def.ComboBox is null)
            {
                oldEffects = OldEffectWrites(def, isEnabled: wd.Label == "Enabled");
            }
            else
            {
                var opt = def.ComboBox.Options.FirstOrDefault(o => o.DisplayName == wd.Label);
                oldEffects = opt is null ? Enumerable.Empty<string>() : OldSelectionEffectWrites(def, opt);
            }

            var oldWrites = oldEffects.OrderBy(s => s).ToList();
            var newWrites = NewWrites(ApplyPlanBuilder.Build(setting, wd.Label, build: null, reset: true))
                .OrderBy(s => s).ToList();

            bool match = oldWrites.SequenceEqual(newWrites);
            rows.Add(new EquivalenceRow(
                $"{def.Id} [reset->{wd.Label}]",
                string.Join(" | ", oldWrites),
                string.Join(" | ", newWrites),
                match));
        }

        return rows;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per (powercfg selection, acIndex, dcIndex) pair - the
    /// ASYMMETRIC AC/DC apply the symmetric <see cref="RunPowerCfgSelectionApply"/> does not cover. OLD mirrors the
    /// live PowerCfgApplier AC/DC path: GetValueFromIndex(acIndex) (the option's PowerCfgValue) -> AC value index,
    /// GetValueFromIndex(dcIndex) -> DC value index. NEW is the BuildPowerCfgSelectionAcDc plan. Both normalised.
    /// Callers pre-filter with <see cref="IsPlainPowerCfgSelectionForApply"/>.</summary>
    public static IReadOnlyList<EquivalenceRow> RunPowerCfgSelectionAcDcApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsPlainPowerCfgSelectionForApply(def))
                continue;

            var setting = SettingDefinitionConverter.ConvertPowerCfg(def);
            var pcs = def.PowerCfgSettings![0];
            var options = def.ComboBox!.Options;

            for (int ac = 0; ac < options.Count; ac++)
            for (int dc = 0; dc < options.Count; dc++)
            {
                int acVal = System.Convert.ToInt32(options[ac].ValueMappings!["PowerCfgValue"]);
                int dcVal = System.Convert.ToInt32(options[dc].ValueMappings!["PowerCfgValue"]);

                var oldWrites = new[]
                {
                    $"POWERWRITEAC sub={pcs.SubgroupGuid} setting={pcs.SettingGuid} = {acVal}",
                    $"POWERWRITEDC sub={pcs.SubgroupGuid} setting={pcs.SettingGuid} = {dcVal}",
                }.OrderBy(s => s).ToList();

                var newWrites = NewWrites(ApplyPlanBuilder.BuildPowerCfgSelectionAcDc(setting, ac, dc))
                    .OrderBy(s => s).ToList();

                bool match = oldWrites.SequenceEqual(newWrites);
                rows.Add(new EquivalenceRow(
                    $"{def.Id} [AC={options[ac].DisplayName}, DC={options[dc].DisplayName}]",
                    string.Join(" | ", oldWrites),
                    string.Join(" | ", newWrites),
                    match));
            }
        }

        return rows;
    }

    /// <summary>Builds one <see cref="EquivalenceRow"/> per (registry selection, option) modelling the config-import
    /// CUSTOM-state re-apply. OLD is the live executor's custom-state branch (per RegistrySetting whose ValueName is in
    /// the dict: ApplySetting(rs, specificValue != null, specificValue); ValueNames absent from the dict are skipped),
    /// NEW is BuildRegistryCustomState over the SAME dict. The dict is the option's ValueMappings - a representative
    /// per-ValueName custom dict (real CustomStateValues are the same shape: raw values keyed by ValueName). Scoped to
    /// PLAIN registry selections with NO per-option scripts (the resolver's routed population - a script selection also
    /// runs a script the registry-only builder does not, so it stays on the old apply).</summary>
    public static IReadOnlyList<EquivalenceRow> RunRegistryCustomStateApply(IEnumerable<SettingDefinition> defs)
    {
        var rows = new List<EquivalenceRow>();

        foreach (var def in defs)
        {
            if (!IsPlainRegistrySelectionForApply(def) || def.PowerShellScripts.Count > 0)
                continue;

            var setting = SettingDefinitionConverter.ConvertSelection(def);
            var options = def.ComboBox!.Options;

            foreach (var opt in options)
            {
                var customValues = new Dictionary<string, object>();
                foreach (var kv in opt.ValueMappings ?? EmptyValues)
                    customValues[kv.Key] = kv.Value!;   // a null captured value is a DELETE (Absent), preserved here

                var oldWrites = def.RegistrySettings
                    .Where(rs => customValues.ContainsKey(rs.ValueName ?? "KeyExists"))
                    .SelectMany(rs =>
                    {
                        object? specificValue = customValues[rs.ValueName ?? "KeyExists"];
                        return OldApplyWrite(rs, isEnabled: specificValue != null, specificValue);
                    })
                    .OrderBy(x => x).ToList();

                var newWrites = NewWrites(ApplyPlanBuilder.BuildRegistryCustomState(setting, customValues))
                    .OrderBy(x => x).ToList();

                bool match = oldWrites.SequenceEqual(newWrites);
                rows.Add(new EquivalenceRow(
                    $"{def.Id} [custom:{opt.DisplayName}]",
                    string.Join(" | ", oldWrites),
                    string.Join(" | ", newWrites),
                    match));
            }
        }

        return rows;
    }

    private static readonly Dictionary<string, object?> EmptyValues = new();

    /// <summary>The old live RESET apply's write intent for one plain registry setting, mirroring
    /// <c>WindowsRegistryService.ApplySetting(setting, isEnabled, specificValue: null, useDefaultValue: ...)</c>
    /// for the reset-to-default direction WITHOUT executing.
    ///
    /// The old reset routes the setting to its Windows-default direction:
    ///   - default-ON  -> a NORMAL enabled write (useDefaultValue is irrelevant when isEnabled), so this is exactly
    ///                    <see cref="OldApplyWrite"/>(rs, isEnabled: true).
    ///   - default-OFF -> the disabled write with <c>useDefaultValue: true</c>. In the real ApplySetting that flag
    ///                    is consulted ONLY in the plain-value branch (valueToSet = GetParentDisableValue(DisabledValue)
    ///                    instead of GetWriteValue(DisabledValue)) and in the matching lock-after dance; every other
    ///                    branch (per-NIC/monitor, ValueName==null key existence, composite, bit, byte) returns earlier
    ///                    and is byte-for-byte identical to a normal disable. So for a non-plain target the reset write
    ///                    equals <see cref="OldApplyWrite"/>(rs, isEnabled: false); only the plain-value branch diverges.</summary>
    private static IEnumerable<string> OldResetWrite(RegistrySetting rs, bool defaultEnabled)
    {
        // default-ON reset == normal enabled write (useDefaultValue does not affect the enabled path).
        if (defaultEnabled)
            return OldApplyWrite(rs, isEnabled: true, specificValue: null);

        // default-OFF reset: only the PLAIN-value target diverges (GetParentDisableValue). Every non-plain target
        // is unaffected by useDefaultValue, so its reset write equals a normal disable - reuse OldApplyWrite.
        bool isPlainValueTarget =
            !rs.ApplyPerNetworkInterface && !rs.ApplyPerMonitor
            && rs.ValueName != null
            && rs.CompositeStringKey == null
            && !(rs.BitMask.HasValue && rs.BinaryByteIndex.HasValue)
            && !(rs.ModifyByteOnly && rs.BinaryByteIndex.HasValue);

        return isPlainValueTarget
            ? OldResetPlainWrite(rs)
            : OldApplyWrite(rs, isEnabled: false, specificValue: null);
    }

    /// <summary>The plain-value branch of the old reset apply (default-OFF): identical to OldApplyWrite's plain
    /// branch for isEnabled:false EXCEPT the written value is GetParentDisableValue(DisabledValue) - DisabledValue[1]
    /// when DisabledValue has 2+ entries, else its first non-null - mirroring ApplySetting's useDefaultValue path.
    /// A null result DELETEs. The LockKeyAccess unlock-before / lock-after dance is identical (the reset is a disable,
    /// so the lock-after condition !isEnabled holds whenever LockKeyAccess is set, just like OldApplyWrite).</summary>
    private static IEnumerable<string> OldResetPlainWrite(RegistrySetting rs)
    {
        var valueToSet = GetParentDisableValue(rs.DisabledValue);
        if (rs.LockKeyAccess)
            yield return $"UNLOCK {rs.KeyPath}";
        yield return valueToSet == null
            ? $"DELETE {rs.KeyPath}\\{rs.ValueName}"
            : $"SET {rs.KeyPath}\\{rs.ValueName} = {Format(valueToSet)} ({rs.ValueType})";
        // Reset is the disabled direction (isEnabled:false), so the lock-after fires whenever the key is lockable -
        // matching OldApplyWrite's (!isEnabled || ...) condition.
        if (rs.LockKeyAccess)
            yield return $"LOCK {rs.KeyPath}";
    }

    /// <summary>GetParentDisableValue, mirrored from WindowsRegistryService: a DisabledValue with 2+ entries resets
    /// to its SECOND entry (DisabledValue[1]); otherwise its first non-null entry (the normal disabled write).</summary>
    private static object? GetParentDisableValue(object?[]? disabledValues) =>
        disabledValues is { Length: > 1 } ? disabledValues[1] : GetWriteValue(disabledValues);

    /// <summary>The old live apply's write intent for one plain registry setting, mirroring the relevant
    /// branches of <c>WindowsRegistryService.ApplySetting(setting, isEnabled, specificValue)</c> WITHOUT
    /// executing. <paramref name="specificValue"/> overrides the Enabled/DisabledValue (selection apply path).</summary>
    private static IEnumerable<string> OldApplyWrite(RegistrySetting rs, bool isEnabled, object? specificValue)
    {
        // Per-NIC / per-monitor: old apply enumerates the parent key's sub-keys and applies the same write to
        // each (checked FIRST in ApplySetting). The underlying write is the plain enabled/disabled value (these
        // settings are plain DWord; null -> delete). Enumeration is deferred; emit the per-sub-key intent.
        if (rs.ApplyPerNetworkInterface || rs.ApplyPerMonitor)
        {
            var scope = rs.ApplyPerNetworkInterface ? "PERNIC" : "PERMONITOR";
            var perSubValue = specificValue ?? GetWriteValue(isEnabled ? rs.EnabledValue : rs.DisabledValue);
            yield return perSubValue == null
                ? $"{scope} DELETE {rs.KeyPath}\\*\\{rs.ValueName}"
                : $"{scope} SET {rs.KeyPath}\\*\\{rs.ValueName} = {Format(perSubValue)} ({rs.ValueType})";
            yield break;
        }

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
        // A LockKeyAccess key is unlocked before the write and re-locked after, but only when the written value is
        // the disabled state (Start = 4) — mirrors WindowsRegistryService.ApplySetting's unlock-before/lock-after dance.
        var valueToSet = specificValue ?? GetWriteValue(isEnabled ? rs.EnabledValue : rs.DisabledValue);
        if (rs.LockKeyAccess)
            yield return $"UNLOCK {rs.KeyPath}";
        yield return valueToSet == null
            ? $"DELETE {rs.KeyPath}\\{rs.ValueName}"
            : $"SET {rs.KeyPath}\\{rs.ValueName} = {Format(valueToSet)} ({rs.ValueType})";
        if (rs.LockKeyAccess && (!isEnabled || (valueToSet is int v && v == 4)))
            yield return $"LOCK {rs.KeyPath}";
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
                case RegistryUnlockKeyOp u:
                    yield return $"UNLOCK {u.Path}";
                    break;
                case RegistryLockKeyOp l:
                    yield return $"LOCK {l.Path}";
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
                case RegistryPerSubkeyWriteOp pw:
                    yield return $"{ScopeLabel(pw.Target)} SET {pw.ParentPath}\\*\\{pw.Target.ValueName} = {Format(pw.Value)} ({pw.Target.Type})";
                    break;
                case RegistryPerSubkeyDeleteOp pd:
                    yield return $"{ScopeLabel(pd.Target)} DELETE {pd.ParentPath}\\*\\{pd.Target.ValueName}";
                    break;
                case PowerCfgSetOp p:
                    yield return PowerCfgIntent(p);
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

    /// <summary>The old apply's per-option script intent for a selection, mirroring the selection branch of
    /// <c>SettingOperationExecutor</c>: the option's Script field selects which shared script body runs
    /// (None/unset -> none); the option's ScriptVariables are substituted into the body; an empty body runs
    /// nothing. Selections carry no .reg/native effects in the catalog.</summary>
    private static IEnumerable<string> OldSelectionEffectWrites(SettingDefinition def, ComboBoxOption opt)
    {
        if (opt.Script is not { } scriptOption || scriptOption == ScriptOption.None)
            yield break;

        foreach (var ps in def.PowerShellScripts)
        {
            var script = scriptOption == ScriptOption.Enabled ? ps.EnabledScript : ps.DisabledScript;
            if (opt.ScriptVariables is { } vars && !string.IsNullOrEmpty(script))
                foreach (var kvp in vars)
                    script = script!.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            if (!string.IsNullOrEmpty(script))
                yield return EffectIntent(new ScriptEffect(script!, ps.RunContext));
        }
    }

    /// <summary>The per-sub-key scope label (PERNIC / PERMONITOR), derived from the target's flags so the old and
    /// new sides render identically.</summary>
    private static string ScopeLabel(RegTarget target) => target.PerNetworkInterface ? "PERNIC" : "PERMONITOR";

    /// <summary>One powercfg value-index write's apply intent (POWERWRITEAC / POWERWRITEDC), normalised identically
    /// for the old and new sides.</summary>
    private static string PowerCfgIntent(PowerCfgSetOp p) =>
        $"POWERWRITE{p.Context} sub={p.Target.SubgroupGuid} setting={p.Target.SettingGuid} = {p.Value}";

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

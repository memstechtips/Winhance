using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog.Migration;

/// <summary>Throwaway migration tool: translates an old toggle SettingDefinition into the new Setting model
/// so the equivalence harness can compare old vs new. Deleted once the migration is complete.</summary>
public static class SettingDefinitionConverter
{
    public static Setting ConvertToggle(SettingDefinition def)
    {
        // A mirror = the same ValueName written under multiple KeyPaths -> one target, many paths.
        var groups = def.RegistrySettings.GroupBy(r => r.ValueName ?? "KeyExists").ToList();
        var targets = BuildTargets(groups);

        var enabledSet = new Dictionary<string, StateValue>();
        var disabledSet = new Dictionary<string, StateValue>();
        foreach (var g in groups)
        {
            var first = g.First();
            if (first.ValueName == null)
            {
                // ValueName == null toggles encode state as key existence, not a stored value. Mirror the
                // old detection's shape rule: inverted (rare) when DisabledValue carries the null sentinel
                // and EnabledValue does not; otherwise standard, where Enabled means the key is present.
                bool inverted = first.EnabledValue?.Contains(null) != true
                                && first.DisabledValue?.Contains(null) == true;
                enabledSet[g.Key] = inverted ? StateValue.Absent : StateValue.Exists;
                disabledSet[g.Key] = inverted ? StateValue.Exists : StateValue.Absent;
                continue;
            }
            enabledSet[g.Key] = ToStateValue(first.EnabledValue) ?? StateValue.Exists;
            disabledSet[g.Key] = ToStateValue(first.DisabledValue) ?? StateValue.Absent;
        }

        var rec = SettingDefinitionToggleState.GetRecommendedToggleState(def);
        var def_ = SettingDefinitionToggleState.GetDefaultToggleState(def);

        var enabled = new SettingState { Label = "Enabled", Set = enabledSet, Roles = RolesFor(true, rec, def_), Effects = BuildToggleEffects(def, isEnabled: true) };
        var disabled = new SettingState { Label = "Disabled", Set = disabledSet, Roles = RolesFor(false, rec, def_), IsFallback = true, Effects = BuildToggleEffects(def, isEnabled: false) };

        return new Setting
        {
            Id = def.Id,
            Name = def.Name,
            Description = def.Description,
            GroupName = def.GroupName,
            Icon = def.Icon,
            Targets = targets.Cast<Target>().ToList(),
            States = new[] { enabled, disabled },
        };
    }

    /// <summary>Translates an old registry SELECTION (ComboBox) SettingDefinition into the new Setting model.
    /// One state per ComboBox option (Label = option DisplayName); each option's ValueMappings become the
    /// state's per-target accept Set; IsRecommended/IsDefault become roles; the IsDefault option becomes the
    /// catch-all fallback when the definition opts in via ResolveUnmatchedToDefault.</summary>
    public static Setting ConvertSelection(SettingDefinition def)
    {
        var groups = def.RegistrySettings.GroupBy(r => r.ValueName ?? "KeyExists").ToList();
        var targets = BuildTargets(groups);

        // Old ResolveRawValuesToIndex substitutes a target's DefaultValue when the live read is absent, so a
        // mapping value equal to that DefaultValue must also accept absence. Index DefaultValue by group key.
        var defaultByKey = groups.ToDictionary(g => g.Key, g => g.First().DefaultValue);

        // Old ResolveRawValuesToIndex resolves an all-targets-absent read to the IsDefault option (its "all
        // backing absent" path) even when ResolveUnmatchedToDefault is off. For a single-target selection
        // that collapses to "the one value is absent", so let that option's value also accept absence. Scoped
        // to single-target to avoid a multi-key option matching a partially-absent read (which old would not).
        bool singleTarget = groups.Count == 1;

        var options = def.ComboBox!.Options;
        var states = new List<SettingState>(options.Count);
        foreach (var opt in options)
        {
            var set = new Dictionary<string, StateValue>();
            if (opt.ValueMappings is { } vm)
            {
                bool absorbAbsent = opt.IsDefault && singleTarget && !def.ResolveUnmatchedToDefault;
                foreach (var (key, expected) in vm)
                {
                    var sv = ToSelectionStateValue(expected, defaultByKey.TryGetValue(key, out var dv) ? dv : null);
                    if (absorbAbsent)
                        sv = sv.OrAbsent();
                    set[key] = sv;
                }
            }

            var roles = new List<StateRole>();
            if (opt.IsRecommended) roles.Add(new StateRole(RoleKind.Recommended));
            if (opt.IsDefault) roles.Add(new StateRole(RoleKind.WindowsDefault));

            states.Add(new SettingState
            {
                Label = opt.DisplayName,
                Set = set,
                Roles = roles,
                Effects = BuildSelectionOptionEffects(def, opt),
                // ResolveUnmatchedToDefault: an unrecognised live state resolves to the IsDefault option.
                IsFallback = def.ResolveUnmatchedToDefault && opt.IsDefault,
            });
        }

        return new Setting
        {
            Id = def.Id,
            Name = def.Name,
            Description = def.Description,
            GroupName = def.GroupName,
            Icon = def.Icon,
            Targets = targets.Cast<Target>().ToList(),
            States = states,
        };
    }

    /// <summary>Translates an old scheduled-task toggle SettingDefinition into the new Setting model: a single
    /// <see cref="TaskTarget"/> with Enabled (task enabled) / Disabled (task disabled) states. Roles come from
    /// the task's RecommendedState/DefaultState. An absent task is an availability concern, handled by the
    /// caller (the old app marks the setting unavailable), not a detected state here.</summary>
    public static Setting ConvertScheduledTaskToggle(SettingDefinition def)
    {
        var task = def.ScheduledTaskSettings[0];
        var target = new TaskTarget("Task", task.TaskPath);

        var enabled = new SettingState
        {
            Label = "Enabled",
            Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(true) },
            Roles = RolesFor(true, task.RecommendedState, task.DefaultState),
        };
        var disabled = new SettingState
        {
            Label = "Disabled",
            Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(false) },
            Roles = RolesFor(false, task.RecommendedState, task.DefaultState),
            IsFallback = true,   // a present task that is not enabled is Disabled (mirrors the old detection)
        };

        return new Setting
        {
            Id = def.Id,
            Name = def.Name,
            Description = def.Description,
            GroupName = def.GroupName,
            Icon = def.Icon,
            Targets = new List<Target> { target },
            States = new[] { enabled, disabled },
        };
    }

    /// <summary>Translates the system-tray-icons selection (DetectionType.SystemTrayIcons) into a Setting that
    /// detects via <see cref="SystemTrayDetector"/>: the "show all" / "hide all" labels are the options whose
    /// Script is Enabled / Disabled (the old code keys off the same Script field). A null detection (no subkeys,
    /// no IsPromoted values, or a mix) is Custom.</summary>
    public static Setting ConvertSystemTray(SettingDefinition def)
    {
        var options = def.ComboBox!.Options;
        string showAll = options.First(o => o.Script == ScriptOption.Enabled).DisplayName;
        string hideAll = options.First(o => o.Script == ScriptOption.Disabled).DisplayName;

        return new Setting
        {
            Id = def.Id,
            Name = def.Name,
            Description = def.Description,
            GroupName = def.GroupName,
            Icon = def.Icon,
            States = options.Select(o => new SettingState { Label = o.DisplayName }).ToList(),
            Detector = new SystemTrayDetector(showAll, hideAll),
        };
    }

    /// <summary>Translates the system-restore toggle (DetectionType.SystemRestore) into a Setting that detects
    /// via <see cref="SystemRestoreDetector"/>: System Restore on for C: = Enabled, off = Disabled (the old
    /// toggle reads the same IsEnabledForC bool). Roles come from the toggle's recommended/default state.</summary>
    public static Setting ConvertSystemRestore(SettingDefinition def)
    {
        var rec = SettingDefinitionToggleState.GetRecommendedToggleState(def);
        var def_ = SettingDefinitionToggleState.GetDefaultToggleState(def);

        var enabled = new SettingState { Label = "Enabled", Roles = RolesFor(true, rec, def_) };
        var disabled = new SettingState { Label = "Disabled", Roles = RolesFor(false, rec, def_) };

        return new Setting
        {
            Id = def.Id,
            Name = def.Name,
            Description = def.Description,
            GroupName = def.GroupName,
            Icon = def.Icon,
            States = new[] { enabled, disabled },
            Detector = new SystemRestoreDetector("Enabled", "Disabled"),
        };
    }

    /// <summary>Translates the DNS-server selection (DetectionType.DnsServer) into a Setting that detects via
    /// <see cref="DnsServerDetector"/>. The automatic label is the first option's DisplayName (the old code
    /// returns index 0 for DHCP / no adapter / no primary). The primary-IP -> label map is built FIRST-WINS so a
    /// duplicate primary resolves to the earliest option, matching the old first-match loop.</summary>
    public static Setting ConvertDnsServer(SettingDefinition def)
    {
        var options = def.ComboBox!.Options;
        string automaticLabel = options[0].DisplayName;

        var primaryIpToLabel = new Dictionary<string, string>();
        foreach (var opt in options)
        {
            if (opt.ScriptVariables is { } vars
                && vars.TryGetValue("primary", out var primary)
                && !primaryIpToLabel.ContainsKey(primary))
            {
                primaryIpToLabel[primary] = opt.DisplayName;
            }
        }

        return new Setting
        {
            Id = def.Id,
            Name = def.Name,
            Description = def.Description,
            GroupName = def.GroupName,
            Icon = def.Icon,
            States = options.Select(o => new SettingState { Label = o.DisplayName }).ToList(),
            Detector = new DnsServerDetector(automaticLabel, primaryIpToLabel),
        };
    }

    /// <summary>Folds registry settings into targets: a mirror (same ValueName under several KeyPaths) is one
    /// target with many paths. ValueName == null groups under the "KeyExists" key (key-existence target).</summary>
    private static List<RegTarget> BuildTargets(List<IGrouping<string, RegistrySetting>> groups) =>
        groups.Select(g =>
        {
            var first = g.First();
            return new RegTarget(
                g.Key,
                g.Select(r => r.KeyPath).ToArray(),
                first.ValueName,
                first.ValueType)
            {
                ByteIndex = first.BinaryByteIndex,
                BitMask = first.BitMask,
                ByteOnly = first.ModifyByteOnly,
                CompositeStringKey = first.CompositeStringKey,
                PerNetworkInterface = first.ApplyPerNetworkInterface,
                PerMonitor = first.ApplyPerMonitor,
                IsGroupPolicy = first.IsGroupPolicy,
                LockKeyAccess = first.LockKeyAccess,
            };
        }).ToList();

    /// <summary>Maps one ComboBox ValueMapping scalar to the accept-value for a selection state. A null
    /// mapping means the value is absent; a concrete value matches that value when present, and also accepts
    /// absence when it equals the target's DefaultValue (old detection reads absent as the DefaultValue).</summary>
    private static StateValue ToSelectionStateValue(object? expected, object? defaultValue)
    {
        if (expected is null)
            return StateValue.Absent;
        var sv = StateValue.Of(expected);
        if (defaultValue is not null && CatalogValueComparer.AreEqual(defaultValue, expected))
            sv = sv.OrAbsent();
        return sv;
    }

    /// <summary>Maps a toggle's apply-only mechanisms to the per-state Effects the old apply runs for that state.
    /// PowerShell scripts and .reg imports only run when their body is non-empty (old guards with IsNullOrEmpty);
    /// native power always runs. Order (script -> regcontent -> native) mirrors the old apply's effect order.</summary>
    private static IReadOnlyList<Effect> BuildToggleEffects(SettingDefinition def, bool isEnabled)
    {
        var effects = new List<Effect>();

        foreach (var ps in def.PowerShellScripts)
        {
            var script = isEnabled ? ps.EnabledScript : ps.DisabledScript;
            if (!string.IsNullOrEmpty(script))
                effects.Add(new ScriptEffect(script!, ps.RunContext));
        }

        foreach (var rc in def.RegContents)
        {
            var content = isEnabled ? rc.EnabledContent : rc.DisabledContent;
            if (!string.IsNullOrEmpty(content))
                effects.Add(new RegContentEffect(content!));
        }

        foreach (var np in def.NativePowerApiSettings)
            effects.Add(new NativePowerEffect(np.InformationLevel, isEnabled ? np.EnabledValue : np.DisabledValue));

        return effects;
    }

    /// <summary>Maps a selection OPTION's apply-only script to the effect that option's state runs. The option's
    /// Script field selects which shared script body runs (mirrors the old SettingOperationExecutor selection path):
    /// Enabled -> EnabledScript, Disabled -> DisabledScript, None -> no script. The option's ScriptVariables are
    /// substituted into the body, and an empty body runs nothing. Selections carry no .reg/native effects in the
    /// catalog, so only scripts are mapped here. (An option with an UNSET Script is treated as None here; the old
    /// code instead fell through to its enable/disable default. Every catalog selection option sets Script
    /// explicitly, so this is unreachable - revisit if a script-bearing selection ever leaves an option Script unset.)</summary>
    private static IReadOnlyList<Effect> BuildSelectionOptionEffects(SettingDefinition def, ComboBoxOption opt)
    {
        var effects = new List<Effect>();

        if (opt.Script is not { } scriptOption || scriptOption == ScriptOption.None)
            return effects;

        foreach (var ps in def.PowerShellScripts)
        {
            var script = scriptOption == ScriptOption.Enabled ? ps.EnabledScript : ps.DisabledScript;
            script = SubstituteScriptVariables(script, opt.ScriptVariables);
            if (!string.IsNullOrEmpty(script))
                effects.Add(new ScriptEffect(script!, ps.RunContext));
        }

        return effects;
    }

    /// <summary>Substitutes a selection option's <c>{{key}}</c> placeholders into a script body, matching the old
    /// SettingOperationExecutor selection-script substitution. Returns the body unchanged when there are no
    /// variables or the body is empty.</summary>
    private static string? SubstituteScriptVariables(string? script, IReadOnlyDictionary<string, string>? variables)
    {
        if (string.IsNullOrEmpty(script) || variables is null)
            return script;
        foreach (var kvp in variables)
            script = script!.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        return script;
    }

    private static IReadOnlyList<StateRole> RolesFor(bool isEnabledState, bool? recommendedIsEnabled, bool? defaultIsEnabled)
    {
        var roles = new List<StateRole>();
        if (recommendedIsEnabled is bool r && r == isEnabledState) roles.Add(new StateRole(RoleKind.Recommended));
        if (defaultIsEnabled is bool d && d == isEnabledState) roles.Add(new StateRole(RoleKind.WindowsDefault));
        return roles;
    }

    private static StateValue? ToStateValue(object?[]? arr)
    {
        if (arr is null) return null;
        var nonNull = arr.Where(v => v != null).ToArray();
        bool hasNull = arr.Any(v => v == null);
        if (nonNull.Length == 0) return StateValue.Absent;            // [null]
        if (nonNull.Length == 1) return hasNull ? StateValue.Of(nonNull[0]!).OrAbsent() : StateValue.Of(nonNull[0]!);
        return StateValue.OneOf(nonNull);                              // 2+ concrete values
    }
}

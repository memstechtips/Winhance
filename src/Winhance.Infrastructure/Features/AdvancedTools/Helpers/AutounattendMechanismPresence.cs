using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers;

/// <summary>
/// Presence predicates the autounattend pipeline uses to gate section headers, skip already-handled
/// settings, and warn on unreachable payloads. Each predicate has a NEW catalog <see cref="Setting"/> overload
/// (read from Targets/Effects) and an OLD <see cref="SettingDefinition"/> overload (a verbatim extraction of the
/// pre-Slice-E1b inline logic in FeatureRegistryScriptSection / AutounattendScriptBuilder). Production reads the
/// catalog overload when the setting is catalog-paired (every production setting is) and falls back to the def
/// overload for an unpaired setting, mirroring the emit's own catalog/def routing so a header is never emitted
/// without content nor a content-bearing feature skipped. The two are proven equal over the whole population by
/// MechanismPresenceEquivalenceTests (which also asserts zero unpaired), so the fallback is dead in production; the
/// def overloads and the fallback are removed with SettingDefinition in Plan 4.
/// </summary>
internal static class AutounattendMechanismPresence
{
    // ---- catalog (new) -----------------------------------------------------------------------------------

    /// <summary>True when the setting writes a registry value in the given hive (HKCU when isHkcu, else HKLM).
    /// Catalog home of the old RegistrySettings KeyPath hive check: a detectable RegTarget's Paths (a mirror = one
    /// target, many paths) OR an apply-only RegistryWriteEffect's Path. An Action's registry writes are modelled as
    /// RegistryWriteEffect (setting-level Effects, never a detectable Target), so both must be checked to match the
    /// old RegistrySettings. The powercfg EnablementKey is a nested RegTarget, NOT a top-level Target, so it is
    /// correctly excluded (matching the old RegistrySettings, which never included the enablement key).</summary>
    public static bool HasRegistryInHive(Setting setting, bool isHkcu) =>
        setting.Targets.OfType<RegTarget>().Any(rt => rt.Paths.Any(p => IsHkcu(p) == isHkcu))
        || AllEffects(setting).OfType<RegistryWriteEffect>().Any(rwe => IsHkcu(rwe.Path) == isHkcu);

    /// <summary>True when the setting controls a scheduled task (catalog home of ScheduledTaskSettings).</summary>
    public static bool HasScheduledTask(Setting setting) =>
        setting.Targets.OfType<TaskTarget>().Any();

    /// <summary>True when the setting runs a PowerShell script in the given hive (User -> HKCU, System -> HKLM).
    /// Catalog home of the old PowerShellScripts RunContext check: ScriptEffects, which live per-state on
    /// SettingState.Effects (toggle/selection) and at setting level on Setting.Effects (Action). The converter
    /// only emits a ScriptEffect for a non-empty script body, so a script setting whose bodies are all empty (a
    /// no-op that emits nothing anyway) reads absent here - strictly more correct than the old count-based check.</summary>
    public static bool HasScriptInHive(Setting setting, bool isHkcu) =>
        AllEffects(setting).OfType<ScriptEffect>()
            .Any(se => (se.Run == RunContext.User) == isHkcu);

    /// <summary>True when the setting runs a PowerShell script in EITHER hive (catalog home of the diagnostic's
    /// hive-agnostic PowerShellScripts presence). Body-based, like <see cref="HasScriptInHive(Setting, bool)"/>.</summary>
    public static bool HasScript(Setting setting) =>
        AllEffects(setting).OfType<ScriptEffect>().Any();

    /// <summary>True when the setting writes any registry value (catalog home of RegistrySettings): a detectable
    /// RegTarget OR an apply-only RegistryWriteEffect (an Action's registry writes). The nested powercfg
    /// EnablementKey is excluded (it is not a top-level Target).</summary>
    public static bool HasRegistry(Setting setting) =>
        setting.Targets.OfType<RegTarget>().Any()
        || AllEffects(setting).OfType<RegistryWriteEffect>().Any();

    /// <summary>True when the setting is powercfg-backed (catalog home of PowerCfgSettings).</summary>
    public static bool HasPowerCfg(Setting setting) =>
        setting.Targets.OfType<PowerCfgTarget>().Any();

    /// <summary>True when the setting imports .reg content (catalog home of RegContents).</summary>
    public static bool HasRegContent(Setting setting) =>
        AllEffects(setting).OfType<RegContentEffect>().Any();

    /// <summary>True when the setting performs a native power-API write (catalog home of NativePowerApiSettings).</summary>
    public static bool HasNativePower(Setting setting) =>
        AllEffects(setting).OfType<NativePowerEffect>().Any();

    /// <summary>Every apply-only Effect a setting carries: its per-state effects plus its setting-level effects.</summary>
    private static IEnumerable<Effect> AllEffects(Setting setting) =>
        setting.States.SelectMany(st => st.Effects).Concat(setting.Effects);

    private static bool IsHkcu(string keyPath) =>
        keyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);

    // ---- SettingDefinition (old, equivalence oracle - deleted in Plan 4) ---------------------------------

    /// <summary>Old form of <see cref="HasRegistryInHive(Setting, bool)"/> - verbatim from the pre-E1b
    /// FeatureRegistryScriptSection hive pre-filter.</summary>
    public static bool HasRegistryInHive(SettingDefinition def, bool isHkcu) =>
        def.RegistrySettings.Any(rs => IsHkcu(rs.KeyPath) == isHkcu);

    /// <summary>Old form of <see cref="HasScheduledTask(Setting)"/>.</summary>
    public static bool HasScheduledTask(SettingDefinition def) =>
        def.ScheduledTaskSettings?.Count > 0;

    /// <summary>Old form of <see cref="HasScriptInHive(Setting, bool)"/> - verbatim from the pre-E1b
    /// FeatureRegistryScriptSection hive pre-filter (RunContext.User -> HKCU).</summary>
    public static bool HasScriptInHive(SettingDefinition def, bool isHkcu) =>
        def.PowerShellScripts?.Any(ps => (ps.RunContext == RunContext.User) == isHkcu) == true;

    /// <summary>Old form of <see cref="HasScript(Setting)"/> - the diagnostic's hive-agnostic PowerShellScripts
    /// presence.</summary>
    public static bool HasScript(SettingDefinition def) =>
        def.PowerShellScripts?.Count > 0;

    /// <summary>Old form of <see cref="HasRegistry(Setting)"/>.</summary>
    public static bool HasRegistry(SettingDefinition def) =>
        def.RegistrySettings?.Any() == true;

    /// <summary>Old form of <see cref="HasPowerCfg(Setting)"/>.</summary>
    public static bool HasPowerCfg(SettingDefinition def) =>
        def.PowerCfgSettings?.Any() == true;

    /// <summary>Old form of <see cref="HasRegContent(Setting)"/>.</summary>
    public static bool HasRegContent(SettingDefinition def) =>
        def.RegContents?.Count > 0;

    /// <summary>Old form of <see cref="HasNativePower(Setting)"/>.</summary>
    public static bool HasNativePower(SettingDefinition def) =>
        def.NativePowerApiSettings?.Count > 0;
}

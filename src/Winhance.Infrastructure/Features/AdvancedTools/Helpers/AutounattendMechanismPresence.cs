using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers;

/// <summary>
/// Presence predicates the autounattend pipeline uses to gate section headers, skip already-handled
/// settings, and warn on unreachable payloads. Each predicate reads from the setting's Targets/Effects.
/// </summary>
internal static class AutounattendMechanismPresence
{
    // ---- catalog -----------------------------------------------------------------------------------

    /// <summary>True when the setting writes a registry value in the given hive (HKCU when isHkcu, else HKLM):
    /// a detectable RegTarget's Paths (a mirror = one target, many paths) OR an apply-only RegistryWriteEffect's
    /// Path. An Action's registry writes are modelled as RegistryWriteEffect (setting-level Effects, never a
    /// detectable Target), so both must be checked. The powercfg EnablementKey is a nested RegTarget, NOT a
    /// top-level Target, so it is correctly excluded.</summary>
    public static bool HasRegistryInHive(Setting setting, bool isHkcu) =>
        setting.Targets.OfType<RegTarget>().Any(rt => rt.Paths.Any(p => IsHkcu(p) == isHkcu))
        || AllEffects(setting).OfType<RegistryWriteEffect>().Any(rwe => IsHkcu(rwe.Path) == isHkcu);

    /// <summary>True when the setting controls a scheduled task.</summary>
    public static bool HasScheduledTask(Setting setting) =>
        setting.Targets.OfType<TaskTarget>().Any();

    /// <summary>True when the setting runs a PowerShell script in the given hive (User -> HKCU, System -> HKLM):
    /// ScriptEffects, which live per-state on SettingState.Effects (toggle/selection) and at setting level on
    /// Setting.Effects (Action). The converter only emits a ScriptEffect for a non-empty script body, so a
    /// script setting whose bodies are all empty (a no-op that emits nothing anyway) reads absent here.</summary>
    public static bool HasScriptInHive(Setting setting, bool isHkcu) =>
        AllEffects(setting).OfType<ScriptEffect>()
            .Any(se => (se.Run == RunContext.User) == isHkcu);

    /// <summary>True when the setting runs a PowerShell script in EITHER hive. Body-based, like
    /// <see cref="HasScriptInHive(Setting, bool)"/>.</summary>
    public static bool HasScript(Setting setting) =>
        AllEffects(setting).OfType<ScriptEffect>().Any();

    /// <summary>True when the setting writes any registry value: a detectable
    /// RegTarget OR an apply-only RegistryWriteEffect (an Action's registry writes). The nested powercfg
    /// EnablementKey is excluded (it is not a top-level Target).</summary>
    public static bool HasRegistry(Setting setting) =>
        setting.Targets.OfType<RegTarget>().Any()
        || AllEffects(setting).OfType<RegistryWriteEffect>().Any();

    /// <summary>True when the setting is powercfg-backed.</summary>
    public static bool HasPowerCfg(Setting setting) =>
        setting.Targets.OfType<PowerCfgTarget>().Any();

    /// <summary>True when the setting imports .reg content.</summary>
    public static bool HasRegContent(Setting setting) =>
        AllEffects(setting).OfType<RegContentEffect>().Any();

    /// <summary>True when the setting performs a native power-API write.</summary>
    public static bool HasNativePower(Setting setting) =>
        AllEffects(setting).OfType<NativePowerEffect>().Any();

    /// <summary>Every apply-only Effect a setting carries: its per-state effects plus its setting-level effects.</summary>
    private static IEnumerable<Effect> AllEffects(Setting setting) =>
        setting.States.SelectMany(st => st.Effects).Concat(setting.Effects);

    private static bool IsHkcu(string keyPath) =>
        keyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);

}

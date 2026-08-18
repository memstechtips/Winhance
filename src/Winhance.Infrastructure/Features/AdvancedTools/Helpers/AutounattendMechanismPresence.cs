using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers;

internal static class AutounattendMechanismPresence
{
    // A detectable RegTarget's Paths (a mirror = one target, many paths) OR an apply-only RegistryWriteEffect's Path:
    // an Action's registry writes are modelled as Effects, never Targets, so both must be checked. The powercfg
    // EnablementKey is a nested RegTarget, not a top-level Target, so it is correctly excluded.
    public static bool HasRegistryInHive(Setting setting, bool isHkcu) =>
        setting.Targets.OfType<RegTarget>().Any(rt => rt.Paths.Any(p => IsHkcu(p) == isHkcu))
        || AllEffects(setting).OfType<RegistryWriteEffect>().Any(rwe => IsHkcu(rwe.Path) == isHkcu);

    public static bool HasScheduledTask(Setting setting) =>
        setting.Targets.OfType<TaskTarget>().Any();

    // ScriptEffects live per state (toggle/selection) and at setting level (Action). A script setting whose bodies
    // are all empty reads absent - the converter only emits a ScriptEffect for a non-empty body.
    public static bool HasScriptInHive(Setting setting, bool isHkcu) =>
        AllEffects(setting).OfType<ScriptEffect>()
            .Any(se => (se.Run == RunContext.User) == isHkcu);

    public static bool HasScript(Setting setting) =>
        AllEffects(setting).OfType<ScriptEffect>().Any();

    public static bool HasRegistry(Setting setting) =>
        setting.Targets.OfType<RegTarget>().Any()
        || AllEffects(setting).OfType<RegistryWriteEffect>().Any();

    public static bool HasPowerCfg(Setting setting) =>
        setting.Targets.OfType<PowerCfgTarget>().Any();

    public static bool HasRegContent(Setting setting) =>
        AllEffects(setting).OfType<RegContentEffect>().Any();

    public static bool HasNativePower(Setting setting) =>
        AllEffects(setting).OfType<NativePowerEffect>().Any();

    private static IEnumerable<Effect> AllEffects(Setting setting) =>
        setting.States.SelectMany(st => st.Effects).Concat(setting.Effects);

    private static bool IsHkcu(string keyPath) =>
        keyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);

}

using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers;

internal static class AutounattendMechanismPresence
{
    public static bool HasScheduledTask(Setting setting) =>
        setting.Targets.OfType<TaskTarget>().Any();

    // ScriptEffects live per state (toggle/selection) and at setting level (Action). A script setting whose bodies
    // are all empty reads absent - the converter only emits a ScriptEffect for a non-empty body.
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
}

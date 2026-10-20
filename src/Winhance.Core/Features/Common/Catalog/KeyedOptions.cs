using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Catalog;

public static class KeyedOptions
{
    public static string? KeyOf(Setting setting, SettingState state) =>
        setting.Targets.OfType<RegTarget>().FirstOrDefault(t => t is { ReadOnly: false, ApplyOnly: false, From: OptionValue.Key }) is { } target
        && state.Set.TryGetValue(target.Key, out var value)
        && value.WritePayload is string key
            ? key
            : null;

    // An empty set is valid: a power plan's whole apply is the provider's effect.
    public static IReadOnlyDictionary<string, StateValue>? SetFor(Setting setting, string key, IOptionProvider provider)
    {
        if (!provider.Accepts(setting, key))
            return null;

        var set = new Dictionary<string, StateValue>(StringComparer.Ordinal);
        foreach (var target in setting.Targets.OfType<RegTarget>().Where(t => !t.ReadOnly))
        {
            if ((target.From == OptionValue.Key ? key : provider.ValueFor(target.From, key)) is { } value)
                set[target.Key] = StateValue.Of(value);
        }
        return set;
    }
}

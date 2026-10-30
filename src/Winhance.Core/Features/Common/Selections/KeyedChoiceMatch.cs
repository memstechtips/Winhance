using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Selections;

// Review mode and the profile switch must agree on this, or one calls a setting done while the other re-applies it.
// The key alone is not enough: Windows can hold the same power plan under its own GUID, and a save renames the
// file it carries, so only the option's provider or the bytes can say two keys are one choice.
public static class KeyedChoiceMatch
{
    public static async Task<bool> IsSameAsync(
        IOptionProviderRegistry options, IFileStore files, Setting setting, ChoiceValue.Keyed saved, ChoiceValue.Keyed live, string? carriedBase64) =>
        options.For(setting.Options!.Source).SameOption(setting, new DynamicOption(saved.Label, saved.Key), new DynamicOption(live.Label, live.Key))
        || (carriedBase64 is { Length: > 0 } && await files.IsSameFileAsync(live.Key, carriedBase64).ConfigureAwait(false));
}

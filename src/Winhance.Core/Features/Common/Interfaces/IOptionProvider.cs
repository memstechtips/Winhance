using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IOptionProvider
{
    IReadOnlyList<OptionSource> Sources { get; }

    IReadOnlyList<DynamicOption> Options(Setting setting, IDetectionContext context);

    string? CurrentKey(Setting setting, IDetectionContext context);

    bool Accepts(Setting setting, string key);

    // Null leaves the machine's value standing (a culture with no LCID has no Locale to write).
    string? ValueFor(OptionValue value, string key);

    Effect? EffectFor(Setting setting, string key);

    // Two keys can name one option: Windows may install a power plan under a GUID of its own choosing.
    bool SameOption(Setting setting, DynamicOption saved, DynamicOption live) =>
        string.Equals(saved.Value, live.Value, StringComparison.OrdinalIgnoreCase);
}

using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Selections;

public sealed record CarriedFile(string SettingId, string Destination, string Base64);

public sealed record SelectionSet(
    IReadOnlyList<SettingChoice> Settings,
    IReadOnlyList<AppChoice> WindowsApps,
    IReadOnlyList<AppChoice> ExternalApps)
{
    public IReadOnlyList<CarriedFile> Files { get; init; } = Array.Empty<CarriedFile>();

    public static readonly SelectionSet Empty =
        new(Array.Empty<SettingChoice>(), Array.Empty<AppChoice>(), Array.Empty<AppChoice>());

    public ChoiceValue? Value(string settingId)
    {
        var id = SettingIdAliases.Normalize(settingId);
        return Settings.FirstOrDefault(choice => SettingIdAliases.Normalize(choice.SettingId) == id)?.Value;
    }

    public SettingState? StateOf(string settingId) =>
        SettingCatalog.Find(settingId) is { } setting ? ChosenState(setting, Value(settingId)) : null;

    // Falls back to the seed state so a set that never reached the Autounattend page still renders a full file.
    public static SettingState? ChosenState(Setting setting, ChoiceValue? value)
    {
        var chosen = value switch
        {
            ChoiceValue.Toggle toggle =>
                setting.States.FirstOrDefault(s => s.Label == TwoState.Label(ControlKind.Toggle, toggle.On)),
            ChoiceValue.CheckBox box =>
                setting.States.FirstOrDefault(s => s.Label == TwoState.Label(ControlKind.CheckBox, box.Checked)),
            ChoiceValue.Option option when option.Index >= 0 && option.Index < setting.States.Count =>
                setting.States[option.Index],
            _ => null,
        };

        return chosen ?? setting.SeedState;
    }
}

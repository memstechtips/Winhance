using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Common.Catalog;

public sealed record Display
{
    public required LocKey Name { get; init; }
    public required LocKey Description { get; init; }
    public LocKey? GroupName { get; init; }
    public Icon? Icon { get; init; }
    public string? AddedInVersion { get; init; }         // drives the NEW badge
    public bool IsSubjectivePreference { get; init; }    // Preference badge instead of Recommended/Default

    public IReadOnlyDictionary<string, LocKey>? CrossGroupChildSettings { get; init; }

    public bool CompactChildren { get; init; }

    public OptionTiles Tiles { get; init; }
}

public enum OptionTiles
{
    None,
    Pictures,
    Colors,
}

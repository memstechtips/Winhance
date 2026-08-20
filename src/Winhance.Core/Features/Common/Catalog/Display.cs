namespace Winhance.Core.Features.Common.Catalog;

// Name/Description/GroupName are the English source text and the fallback for the Setting_{Id}_* localization keys.
public sealed record Display
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? GroupName { get; init; }
    public Icon? Icon { get; init; }
    public string? AddedInVersion { get; init; }         // drives the NEW badge
    public bool IsSubjectivePreference { get; init; }    // Preference badge instead of Recommended/Default

    public IReadOnlyDictionary<string, string>? CrossGroupChildSettings { get; init; }
}

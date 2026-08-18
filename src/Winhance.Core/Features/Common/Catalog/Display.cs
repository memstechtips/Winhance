namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Everything the user sees for a setting. Name/Description/GroupName are the English source text and
/// the fallback for the Setting_{Id}_* localization keys. Id (the contract) is NOT here - it is never shown.</summary>
public sealed record Display
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? GroupName { get; init; }
    public Icon? Icon { get; init; }
    public string? AddedInVersion { get; init; }         // drives the NEW badge
    public bool IsSubjectivePreference { get; init; }    // Preference badge instead of Recommended/Default

    /// <summary>Child setting id -> localization key for the cross-group info banner (the master's "also
    /// controls these settings in other groups" list). Exactly one setting ships it:
    /// privacy-ads-promotional-master. Null = none (never an empty dictionary).</summary>
    public IReadOnlyDictionary<string, string>? CrossGroupChildSettings { get; init; }
}

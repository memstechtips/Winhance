namespace Winhance.UI.Features.Common.Models;

public enum ProfileKind
{
    Focus,
    Everyday,
    BeforeWinhance,
    User,
}

// Id is the file name for a saved profile; a built-in's carries a ':' so no file name can take it.
public sealed record SettingsProfile(string Id, string Name, ProfileKind Kind, string? FilePath, string? Description = null);

// NotApplied names the settings that were skipped or still read differently once the apply finished.
public sealed record ProfileSwitchResult(
    bool Success, IReadOnlyList<string> NotApplied, IReadOnlyList<string> NeedsRestart);

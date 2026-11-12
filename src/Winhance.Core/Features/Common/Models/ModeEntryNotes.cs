namespace Winhance.Core.Features.Common.Models;

// What a mode-entry dialog says above its own choices. A null section is not shown; an explanation brings its
// "don't show again" box with it.
public sealed record ModeEntryNotes(string? Reason, string? Explanation, string? Warning);

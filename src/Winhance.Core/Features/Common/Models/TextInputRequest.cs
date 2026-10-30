namespace Winhance.Core.Features.Common.Models;

public sealed record TextInputRequest
{
    public string Title { get; init; } = string.Empty;

    public string? Message { get; init; }

    public string Text { get; init; } = string.Empty;

    public string? Placeholder { get; init; }

    // Empty means the localized OK.
    public string ConfirmButtonText { get; init; } = string.Empty;

    // The confirm button stays disabled while this says no.
    public Func<string, bool> IsValid { get; init; } = text => text.Trim().Length > 0;
}

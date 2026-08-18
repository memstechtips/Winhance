namespace Winhance.Core.Features.Common.Models;

public sealed record ConfirmationRequest
{
    public string Message { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? CheckboxText { get; init; }

    public IReadOnlyList<string>? Items { get; init; } = null;

    public bool CheckboxInitiallyChecked { get; init; } = true;

    public string ConfirmButtonText { get; init; } = "OK";

    public string CancelButtonText { get; init; } = "Cancel";

    // When set, a third button appears and Enter defaults to Cancel (the safe choice) instead of the primary button.
    public string? SecondaryButtonText { get; init; }
}

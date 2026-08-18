namespace Winhance.Core.Features.Common.Models;

public sealed record ConfirmationRequest
{
    public string Message { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? CheckboxText { get; init; }

    public IReadOnlyList<string>? Items { get; init; } = null;

    public bool CheckboxInitiallyChecked { get; init; } = true;

    // Empty means DialogService supplies the localized OK / Cancel.
    public string ConfirmButtonText { get; init; } = string.Empty;

    public string CancelButtonText { get; init; } = string.Empty;

    // When set, a third button appears and Enter defaults to Cancel (the safe choice) instead of the primary button.
    public string? SecondaryButtonText { get; init; }
}

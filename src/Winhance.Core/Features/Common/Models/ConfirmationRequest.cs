namespace Winhance.Core.Features.Common.Models;

public sealed record ConfirmationRequest
{
    public string Message { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? CheckboxText { get; init; }

    // A smaller line under the checkbox.
    public string? CheckboxDescription { get; init; }

    public IReadOnlyList<string>? Items { get; init; } = null;

    // When set, the list shows these rows with their icons instead of the plain Items.
    public IReadOnlyList<ConfirmationItem>? ItemDetails { get; init; }

    // One legend line per icon type present in ItemDetails; null hides it.
    public string? InstabilityLegend { get; init; }

    public string? NoReinstallLegend { get; init; }

    public bool CheckboxInitiallyChecked { get; init; } = true;

    // Empty means DialogService supplies the localized OK / Cancel.
    public string ConfirmButtonText { get; init; } = string.Empty;

    public string CancelButtonText { get; init; } = string.Empty;

    // When set, a third button appears and Enter defaults to Cancel (the safe choice) instead of the primary button.
    public string? SecondaryButtonText { get; init; }
}

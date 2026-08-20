namespace Winhance.Core.Features.Common.Models;

public sealed record ConfirmationResponse
{
    // The PRIMARY button only - a secondary-button pick does not count as Confirmed, so two-button callers are unaffected.
    public bool Confirmed { get; init; }

    public bool SecondaryChosen { get; init; }

    public bool CheckboxChecked { get; init; }
}

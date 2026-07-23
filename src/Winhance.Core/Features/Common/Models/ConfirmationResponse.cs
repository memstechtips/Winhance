namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Represents the user's response to a confirmation request.
/// This generic model can be used across all features that require user confirmation.
/// </summary>
public sealed record ConfirmationResponse
{
    /// <summary>
    /// Gets whether the user confirmed the operation (the PRIMARY button only - a secondary-button
    /// pick does not count as Confirmed, so existing two-button callers are unaffected).
    /// </summary>
    public bool Confirmed { get; init; }

    /// <summary>
    /// Gets whether the user chose the optional secondary button. Only ever true when the
    /// ConfirmationRequest supplied SecondaryButtonText.
    /// </summary>
    public bool SecondaryChosen { get; init; }

    /// <summary>
    /// Gets whether the optional checkbox was checked.
    /// Only relevant if the ConfirmationRequest had CheckboxText.
    /// </summary>
    public bool CheckboxChecked { get; init; }
}

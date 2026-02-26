namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Represents the user's response to a confirmation request.
/// This generic model can be used across all features that require user confirmation.
/// </summary>
public record ConfirmationResponse
{
    /// <summary>
    /// Gets whether the user confirmed the operation.
    /// </summary>
    public bool Confirmed { get; init; }

    /// <summary>
    /// Gets whether the optional checkbox was checked.
    /// Only relevant if the ConfirmationRequest had CheckboxText.
    /// </summary>
    public bool CheckboxChecked { get; init; }
}

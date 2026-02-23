namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents a request for user confirmation with optional context data.
    /// This generic model can be used across all features that require user confirmation.
    /// </summary>
    public record ConfirmationRequest
    {
        /// <summary>
        /// Gets the confirmation message to display to the user.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Gets the title of the confirmation dialog.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the optional checkbox text. If null, no checkbox is shown.
        /// </summary>
        public string? CheckboxText { get; init; }
    }
}

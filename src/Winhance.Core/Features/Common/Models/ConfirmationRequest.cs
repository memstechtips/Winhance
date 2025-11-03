using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents a request for user confirmation with optional context data.
    /// This generic model can be used across all features that require user confirmation.
    /// </summary>
    public class ConfirmationRequest
    {
        /// <summary>
        /// Gets or sets the confirmation message to display to the user.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title of the confirmation dialog.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional checkbox text. If null, no checkbox is shown.
        /// </summary>
        public string? CheckboxText { get; set; }

        /// <summary>
        /// Gets or sets additional context data that may be needed for the operation.
        /// </summary>
        public Dictionary<string, object>? Context { get; set; }
    }
}
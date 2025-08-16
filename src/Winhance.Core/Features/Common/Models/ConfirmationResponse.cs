using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents the user's response to a confirmation request.
    /// This generic model can be used across all features that require user confirmation.
    /// </summary>
    public class ConfirmationResponse
    {
        /// <summary>
        /// Gets or sets whether the user confirmed the operation.
        /// </summary>
        public bool Confirmed { get; set; }

        /// <summary>
        /// Gets or sets whether the optional checkbox was checked.
        /// Only relevant if the ConfirmationRequest had CheckboxText.
        /// </summary>
        public bool CheckboxChecked { get; set; }

        /// <summary>
        /// Gets or sets additional context data that may be passed back from the UI.
        /// </summary>
        public Dictionary<string, object>? Context { get; set; }
    }
}
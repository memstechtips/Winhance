using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents the result of a refresh operation for installation statuses.
    /// </summary>
    public class RefreshResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the refresh operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message if the refresh operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the collection of app IDs that were successfully refreshed.
        /// </summary>
        public IEnumerable<string> RefreshedAppIds { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the collection of app IDs that failed to refresh.
        /// </summary>
        public IEnumerable<string> FailedAppIds { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the number of successfully refreshed apps.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the number of failed refreshed apps.
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Gets or sets the collection of errors.
        /// </summary>
        public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
    }
}

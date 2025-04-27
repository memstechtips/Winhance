using System;

namespace Winhance.WPF.Features.Common.Messages
{
    /// <summary>
    /// Message to notify the view to reset the expansion state of collapsible sections.
    /// </summary>
    public class ResetExpansionStateMessage
    {
        /// <summary>
        /// Gets the timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.Now;
    }
}

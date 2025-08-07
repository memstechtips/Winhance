using System;

namespace Winhance.Core.Features.Common.Events.UI
{
    /// <summary>
    /// Domain event to request theme icon update in the view
    /// </summary>
    public class UpdateThemeIconEvent : IDomainEvent
    {
        /// <summary>
        /// Gets the timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets a unique identifier for the event instance
        /// </summary>
        public Guid EventId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateThemeIconEvent"/> class
        /// </summary>
        public UpdateThemeIconEvent()
        {
            Timestamp = DateTime.UtcNow;
            EventId = Guid.NewGuid();
        }
    }
}

using System;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Events.UI
{
    /// <summary>
    /// Event for window state changes
    /// </summary>
    public class WindowStateEvent : IDomainEvent
    {
        /// <summary>
        /// Gets the event ID
        /// </summary>
        public Guid EventId { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets the timestamp when the event was created
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>
        /// Gets the window state
        /// </summary>
        public WindowState WindowState { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowStateEvent"/> class
        /// </summary>
        /// <param name="windowState">The window state</param>
        public WindowStateEvent(WindowState windowState)
        {
            WindowState = windowState;
        }
    }
}

using System;

namespace Winhance.Core.Features.Common.Events.UI
{
    /// <summary>
    /// Event for task progress updates
    /// </summary>
    public class TaskProgressEvent : IDomainEvent
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
        /// Gets or sets the progress value (0-100)
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// Gets or sets the status text
        /// </summary>
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the progress is indeterminate
        /// </summary>
        public bool IsIndeterminate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a task is running
        /// </summary>
        public bool IsTaskRunning { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the task can be canceled
        /// </summary>
        public bool CanCancel { get; set; }
    }
}

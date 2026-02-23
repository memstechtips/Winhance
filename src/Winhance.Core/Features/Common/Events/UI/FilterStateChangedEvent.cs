using System;

namespace Winhance.Core.Features.Common.Events.UI
{
    /// <summary>
    /// Domain event published when the Windows version filter state changes.
    /// Replaces the direct MainWindowViewModel.FilterStateChanged CLR event
    /// to decouple child ViewModels from the parent ViewModel.
    /// </summary>
    public class FilterStateChangedEvent : IDomainEvent
    {
        public DateTime Timestamp { get; }
        public Guid EventId { get; }

        /// <summary>
        /// Whether the Windows version filter is currently enabled.
        /// </summary>
        public bool IsFilterEnabled { get; }

        public FilterStateChangedEvent(bool isFilterEnabled)
        {
            Timestamp = DateTime.UtcNow;
            EventId = Guid.NewGuid();
            IsFilterEnabled = isFilterEnabled;
        }
    }
}

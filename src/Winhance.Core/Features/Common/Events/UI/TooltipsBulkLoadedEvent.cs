using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Events.UI
{
    /// <summary>
    /// Domain event published when tooltip data has been bulk loaded for multiple settings.
    /// This is typically used during application startup to initialize all tooltip data at once.
    /// </summary>
    public class TooltipsBulkLoadedEvent : IDomainEvent
    {
        public DateTime Timestamp { get; }
        public Guid EventId { get; }
        
        /// <summary>
        /// Dictionary mapping setting IDs to their tooltip data.
        /// </summary>
        public Dictionary<string, SettingTooltipData> TooltipDataCollection { get; }

        /// <summary>
        /// Initializes a new instance of the TooltipsBulkLoadedEvent.
        /// </summary>
        /// <param name="tooltipDataCollection">The collection of tooltip data indexed by setting ID</param>
        public TooltipsBulkLoadedEvent(Dictionary<string, SettingTooltipData> tooltipDataCollection)
        {
            Timestamp = DateTime.UtcNow;
            EventId = Guid.NewGuid();
            TooltipDataCollection = tooltipDataCollection ?? throw new ArgumentNullException(nameof(tooltipDataCollection));
        }
    }
}
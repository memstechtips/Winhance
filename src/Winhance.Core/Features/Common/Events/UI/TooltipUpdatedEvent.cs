using System;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Events.UI
{
    /// <summary>
    /// Domain event published when tooltip data has been updated for a specific setting.
    /// This allows UI components to reactively update tooltip displays without tight coupling.
    /// </summary>
    public class TooltipUpdatedEvent : IDomainEvent
    {
        public DateTime Timestamp { get; }
        public Guid EventId { get; }
        
        /// <summary>
        /// The ID of the setting whose tooltip data was updated.
        /// </summary>
        public string SettingId { get; }
        
        /// <summary>
        /// The updated tooltip data for the setting.
        /// </summary>
        public SettingTooltipData TooltipData { get; }

        /// <summary>
        /// Initializes a new instance of the TooltipUpdatedEvent.
        /// </summary>
        /// <param name="settingId">The setting ID whose tooltip was updated</param>
        /// <param name="tooltipData">The updated tooltip data</param>
        public TooltipUpdatedEvent(string settingId, SettingTooltipData tooltipData)
        {
            Timestamp = DateTime.UtcNow;
            EventId = Guid.NewGuid();
            SettingId = settingId ?? throw new ArgumentNullException(nameof(settingId));
            TooltipData = tooltipData ?? throw new ArgumentNullException(nameof(tooltipData));
        }
    }
}
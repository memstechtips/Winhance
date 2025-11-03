using System;

namespace Winhance.Core.Features.Common.Events
{
    public class PowerPlanChangedEvent : IDomainEvent
    {
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public Guid EventId { get; } = Guid.NewGuid();
        public string PreviousPlanGuid { get; set; } = string.Empty;
        public string NewPlanGuid { get; set; } = string.Empty;
        public string NewPlanName { get; set; } = string.Empty;
        public int NewPlanIndex { get; set; } = -1;
    }
}
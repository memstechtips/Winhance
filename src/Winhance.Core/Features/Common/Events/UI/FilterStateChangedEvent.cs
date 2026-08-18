namespace Winhance.Core.Features.Common.Events.UI;

public class FilterStateChangedEvent : IDomainEvent
{
    public DateTime Timestamp { get; }
    public Guid EventId { get; }

    public bool IsFilterEnabled { get; }

    public FilterStateChangedEvent(bool isFilterEnabled)
    {
        Timestamp = DateTime.UtcNow;
        EventId = Guid.NewGuid();
        IsFilterEnabled = isFilterEnabled;
    }
}

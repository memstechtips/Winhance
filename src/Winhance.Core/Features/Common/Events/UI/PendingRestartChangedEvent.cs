namespace Winhance.Core.Features.Common.Events;

public class PendingRestartChangedEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();

    public bool IsPending { get; init; }
}

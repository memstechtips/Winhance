namespace Winhance.Core.Features.Common.Events;

public interface IDomainEvent
{
    DateTime Timestamp { get; }

    Guid EventId { get; }
}

namespace Winhance.Core.Features.Common.Events;

/// <summary>
/// Raised when the set of settings waiting on an Explorer restart becomes non-empty or is cleared.
/// The pending-restart bar observes this to show or hide itself.
/// </summary>
public class PendingRestartChangedEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();

    /// <summary>True while at least one applied setting is still waiting on an Explorer restart.</summary>
    public bool IsPending { get; init; }
}

using System;

namespace Winhance.Core.Features.Common.Events;

/// <summary>
/// Marker interface for all domain events
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Gets a unique identifier for the event instance
    /// </summary>
    Guid EventId { get; }
}

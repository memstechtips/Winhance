using System;

namespace Winhance.Core.Features.Common.Events;

/// <summary>
/// Raised when the app leaves Builder mode for Normal. Builder authors toggle/selection
/// state into the shared settings ViewModels without applying it to the system, so on
/// exit the loaded settings must be reloaded from live system state to stay truthful.
/// </summary>
public class BuilderModeExitedEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}

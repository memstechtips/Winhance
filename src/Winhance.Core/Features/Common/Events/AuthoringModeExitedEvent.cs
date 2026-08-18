namespace Winhance.Core.Features.Common.Events;

// An authoring mode moves toggles on the shared ViewModels without touching the machine, so on exit those
// positions are fiction and the settings must be reloaded from live state. Named for the capability
// (ModeCapabilities.AuthorsIntent), not for Builder, so a second authoring mode is covered by declaring its capabilities.
public class AuthoringModeExitedEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}

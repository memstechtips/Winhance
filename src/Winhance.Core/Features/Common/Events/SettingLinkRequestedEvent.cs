namespace Winhance.Core.Features.Common.Events;

// Goes over the event bus rather than up the visual tree: the chip is several levels inside a card, panel and
// section, and threading an event through all of that would tie the matrix view to the page. The host page
// decides what "go there" means - only it knows which of its sections holds the setting.
public class SettingLinkRequestedEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();

    public required string SettingId { get; init; }

    // What the search box is filtered by once there.
    public required string SettingName { get; init; }
}

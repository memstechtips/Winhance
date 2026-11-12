namespace Winhance.Core.Features.Common.Events.UI;

// The main window owns the frame and the sidebar, so a page that links to another asks it over the bus.
public class PageRequestedEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();

    public required string PageTag { get; init; }

    // Picks the tab on a page of several features, such as Windows Apps on Software & Apps.
    public string? FeatureId { get; init; }
}

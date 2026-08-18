namespace Winhance.Core.Features.Common.Events;

/// <summary>
/// Raised when the user clicks a setting's name inside a Technical Details requirement chip, asking
/// to be taken to that setting.
///
/// It goes over the event bus rather than up the visual tree: the chip is several levels inside a
/// settings card, inside a panel, inside a section, and threading an event through all of that would
/// tie the matrix view to the page hosting it. The host page decides what "go there" means -- it is
/// the only thing that knows which of its sections holds the setting, and only it can navigate.
/// </summary>
public class SettingLinkRequestedEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();

    /// <summary>Catalog id of the setting to go to.</summary>
    public required string SettingId { get; init; }

    /// <summary>Its display name, which is what the search box is filtered by once there.</summary>
    public required string SettingName { get; init; }
}

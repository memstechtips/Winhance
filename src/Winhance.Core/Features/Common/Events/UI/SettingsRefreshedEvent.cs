using System;

namespace Winhance.Core.Features.Common.Events.UI;

/// <summary>
/// Domain event published when a section's settings have been reloaded
/// (e.g. after a filter change or language change).
/// Pages subscribe to re-apply view-level state such as badge visibility.
/// </summary>
public class SettingsRefreshedEvent : IDomainEvent
{
    public DateTime Timestamp { get; }
    public Guid EventId { get; }

    /// <summary>
    /// The display name of the section whose settings were refreshed.
    /// </summary>
    public string SectionDisplayName { get; }

    public SettingsRefreshedEvent(string sectionDisplayName)
    {
        Timestamp = DateTime.UtcNow;
        EventId = Guid.NewGuid();
        SectionDisplayName = sectionDisplayName;
    }
}

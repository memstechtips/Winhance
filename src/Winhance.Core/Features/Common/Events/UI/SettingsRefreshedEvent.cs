namespace Winhance.Core.Features.Common.Events.UI;

public class SettingsRefreshedEvent : IDomainEvent
{
    public DateTime Timestamp { get; }
    public Guid EventId { get; }

    public string SectionDisplayName { get; }

    public SettingsRefreshedEvent(string sectionDisplayName)
    {
        Timestamp = DateTime.UtcNow;
        EventId = Guid.NewGuid();
        SectionDisplayName = sectionDisplayName;
    }
}

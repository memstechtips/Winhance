namespace Winhance.Core.Features.Common.Events;

// Published after the seed choices are recorded, so feature ViewModels that were already built re-apply
// the authored overlay; ones built later get it from SettingViewModelFactory on creation.
public class BuilderSeededEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}

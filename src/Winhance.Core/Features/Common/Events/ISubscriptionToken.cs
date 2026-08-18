namespace Winhance.Core.Features.Common.Events;

public interface ISubscriptionToken : IDisposable
{
    Guid SubscriptionId { get; }

    Type EventType { get; }
}

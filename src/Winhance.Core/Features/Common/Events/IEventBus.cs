namespace Winhance.Core.Features.Common.Events;

public interface IEventBus
{
    // Synchronous handlers run inline; async handlers are fired-and-observed (errors logged, not awaited by the caller).
    void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;

    ISubscriptionToken Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;

    // Preferred over an async void handler: the returned Task is observed for errors.
    ISubscriptionToken SubscribeAsync<TEvent>(Func<TEvent, Task> handler) where TEvent : IDomainEvent;

    void Unsubscribe(ISubscriptionToken token);
}

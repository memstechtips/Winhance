using System;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Events
{
    /// <summary>
    /// Interface for the event bus that handles publishing and subscribing to domain events
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Publishes a domain event to all subscribers.
        /// Synchronous handlers are invoked inline; async handlers are fired-and-observed
        /// (errors are logged but not awaited by the caller).
        /// </summary>
        /// <typeparam name="TEvent">The type of event to publish</typeparam>
        /// <param name="domainEvent">The event to publish</param>
        void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;

        /// <summary>
        /// Subscribes a synchronous handler to a specific domain event type
        /// </summary>
        /// <typeparam name="TEvent">The type of event to subscribe to</typeparam>
        /// <param name="handler">The handler to invoke when the event is published</param>
        /// <returns>A subscription token that can be used to unsubscribe</returns>
        ISubscriptionToken Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;

        /// <summary>
        /// Subscribes an async handler to a specific domain event type.
        /// Preferred over using async void with <see cref="Subscribe{TEvent}"/> because
        /// the returned Task is properly observed for errors.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to subscribe to</typeparam>
        /// <param name="handler">The async handler to invoke when the event is published</param>
        /// <returns>A subscription token that can be used to unsubscribe</returns>
        ISubscriptionToken SubscribeAsync<TEvent>(Func<TEvent, Task> handler) where TEvent : IDomainEvent;

        /// <summary>
        /// Unsubscribes from a specific domain event type using the subscription token
        /// </summary>
        /// <param name="token">The subscription token returned from Subscribe</param>
        void Unsubscribe(ISubscriptionToken token);
    }
}

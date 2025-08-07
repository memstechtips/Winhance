using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Events
{
    /// <summary>
    /// Implementation of the event bus that handles publishing and subscribing to domain events
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly ILogService _logService;
        private readonly Dictionary<Type, List<Subscription>> _subscriptions = new();
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class
        /// </summary>
        /// <param name="logService">The log service</param>
        public EventBus(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc />
        public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            if (domainEvent == null)
                throw new ArgumentNullException(nameof(domainEvent));

            var eventType = typeof(TEvent);
            
            List<Subscription> subscriptions;
            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(eventType, out subscriptions))
                    return; // No subscribers
                
                // Create a copy to avoid modification during enumeration
                subscriptions = subscriptions.ToList();
            }

            foreach (var subscription in subscriptions)
            {
                try
                {
                    // Cast the handler to the correct type and invoke it
                    ((Action<TEvent>)subscription.Handler)(domainEvent);
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error handling event {eventType.Name}: {ex.Message}");
                }
            }
        }

        /// <inheritdoc />
        public ISubscriptionToken Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(TEvent);
            var subscription = new Subscription(eventType, handler);

            lock (_lock)
            {
                if (!_subscriptions.TryGetValue(eventType, out var subscriptions))
                {
                    subscriptions = new List<Subscription>();
                    _subscriptions[eventType] = subscriptions;
                }

                subscriptions.Add(subscription);
            }

            return new SubscriptionToken(subscription.Id, eventType, token => Unsubscribe(token));
        }

        /// <inheritdoc />
        public void Unsubscribe(ISubscriptionToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            lock (_lock)
            {
                if (_subscriptions.TryGetValue(token.EventType, out var subscriptions))
                {
                    subscriptions.RemoveAll(s => s.Id == token.SubscriptionId);
                    
                    // Remove the event type if there are no more subscriptions
                    if (subscriptions.Count == 0)
                        _subscriptions.Remove(token.EventType);
                }
            }
        }

        /// <summary>
        /// Represents a subscription to an event
        /// </summary>
        private class Subscription
        {
            /// <summary>
            /// Gets the unique identifier for this subscription
            /// </summary>
            public Guid Id { get; }
            
            /// <summary>
            /// Gets the type of event this subscription is for
            /// </summary>
            public Type EventType { get; }
            
            /// <summary>
            /// Gets the handler for this subscription
            /// </summary>
            public Delegate Handler { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="Subscription"/> class
            /// </summary>
            /// <param name="eventType">The type of event</param>
            /// <param name="handler">The handler</param>
            public Subscription(Type eventType, Delegate handler)
            {
                Id = Guid.NewGuid();
                EventType = eventType;
                Handler = handler;
            }
        }

        /// <summary>
        /// Implementation of <see cref="ISubscriptionToken"/> that unsubscribes when disposed
        /// </summary>
        private class SubscriptionToken : ISubscriptionToken
        {
            private readonly Action<ISubscriptionToken> _unsubscribeAction;
            private bool _isDisposed;

            /// <inheritdoc />
            public Guid SubscriptionId { get; }
            
            /// <inheritdoc />
            public Type EventType { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="SubscriptionToken"/> class
            /// </summary>
            /// <param name="subscriptionId">The subscription ID</param>
            /// <param name="eventType">The event type</param>
            /// <param name="unsubscribeAction">The action to unsubscribe</param>
            public SubscriptionToken(Guid subscriptionId, Type eventType, Action<ISubscriptionToken> unsubscribeAction)
            {
                SubscriptionId = subscriptionId;
                EventType = eventType;
                _unsubscribeAction = unsubscribeAction ?? throw new ArgumentNullException(nameof(unsubscribeAction));
            }

            /// <inheritdoc />
            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _unsubscribeAction(this);
                    _isDisposed = true;
                }
            }
        }
    }
}

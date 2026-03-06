using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Events;

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

        List<Subscription>? subscriptions;
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
                if (subscription.IsAsync)
                {
                    // Fire the async handler and observe the Task for errors
                    var task = ((Func<TEvent, Task>)subscription.Handler)(domainEvent);
                    task.ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            _logService.Log(LogLevel.Error,
                                $"Error in async handler for event {eventType.Name}: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
                else
                {
                    ((Action<TEvent>)subscription.Handler)(domainEvent);
                }
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

        return AddSubscription(typeof(TEvent), handler, isAsync: false);
    }

    /// <inheritdoc />
    public ISubscriptionToken SubscribeAsync<TEvent>(Func<TEvent, Task> handler) where TEvent : IDomainEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        return AddSubscription(typeof(TEvent), handler, isAsync: true);
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

    private ISubscriptionToken AddSubscription(Type eventType, Delegate handler, bool isAsync)
    {
        var subscription = new Subscription(eventType, handler, isAsync);

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

    /// <summary>
    /// Represents a subscription to an event
    /// </summary>
    private class Subscription
    {
        public Guid Id { get; }
        public Type EventType { get; }
        public Delegate Handler { get; }
        public bool IsAsync { get; }

        public Subscription(Type eventType, Delegate handler, bool isAsync)
        {
            Id = Guid.NewGuid();
            EventType = eventType;
            Handler = handler;
            IsAsync = isAsync;
        }
    }

    /// <summary>
    /// Implementation of <see cref="ISubscriptionToken"/> that unsubscribes when disposed
    /// </summary>
    private class SubscriptionToken : ISubscriptionToken
    {
        private readonly Action<ISubscriptionToken> _unsubscribeAction;
        private int _isDisposed; // 0 = not disposed, 1 = disposed

        public Guid SubscriptionId { get; }
        public Type EventType { get; }

        public SubscriptionToken(Guid subscriptionId, Type eventType, Action<ISubscriptionToken> unsubscribeAction)
        {
            SubscriptionId = subscriptionId;
            EventType = eventType;
            _unsubscribeAction = unsubscribeAction ?? throw new ArgumentNullException(nameof(unsubscribeAction));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                _unsubscribeAction(this);
            }
        }
    }
}

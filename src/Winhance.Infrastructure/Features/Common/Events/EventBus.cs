using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Events;

public class EventBus : IEventBus
{
    private readonly ILogService _logService;
    private readonly Dictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly object _lock = new();

    public EventBus(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
    {
        if (domainEvent == null)
            throw new ArgumentNullException(nameof(domainEvent));

        var eventType = typeof(TEvent);

        List<Subscription>? subscriptions;
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventType, out subscriptions))
                return;

            // Create a copy to avoid modification during enumeration
            subscriptions = subscriptions.ToList();
        }

        foreach (var subscription in subscriptions)
        {
            try
            {
                if (subscription.IsAsync)
                {
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

    public ISubscriptionToken Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        return AddSubscription(typeof(TEvent), handler, isAsync: false);
    }

    public ISubscriptionToken SubscribeAsync<TEvent>(Func<TEvent, Task> handler) where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        return AddSubscription(typeof(TEvent), handler, isAsync: true);
    }

    public void Unsubscribe(ISubscriptionToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        lock (_lock)
        {
            if (_subscriptions.TryGetValue(token.EventType, out var subscriptions))
            {
                subscriptions.RemoveAll(s => s.Id == token.SubscriptionId);

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

    private class SubscriptionToken : ISubscriptionToken
    {
        private readonly Action<ISubscriptionToken> _unsubscribeAction;
        private int _isDisposed;

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

using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Events
{
    /// <summary>
    /// Base class for components that handle domain events
    /// </summary>
    public abstract class EventHandlerBase : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;
        private readonly List<ISubscriptionToken> _subscriptionTokens = new();
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventHandlerBase"/> class
        /// </summary>
        /// <param name="eventBus">The event bus</param>
        /// <param name="logService">The log service</param>
        protected EventHandlerBase(IEventBus eventBus, ILogService logService)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Subscribes to a domain event
        /// </summary>
        /// <typeparam name="TEvent">The type of event to subscribe to</typeparam>
        /// <param name="handler">The handler to invoke when the event is published</param>
        protected void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            try
            {
                var token = _eventBus.Subscribe(handler);
                _subscriptionTokens.Add(token);
                _logService.Log(LogLevel.Debug, $"Subscribed to {typeof(TEvent).Name} events");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error subscribing to {typeof(TEvent).Name} events: {ex.Message}");
            }
        }

        /// <summary>
        /// Publishes a domain event
        /// </summary>
        /// <typeparam name="TEvent">The type of event to publish</typeparam>
        /// <param name="domainEvent">The event to publish</param>
        protected void PublishEvent<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            if (domainEvent == null)
                throw new ArgumentNullException(nameof(domainEvent));

            try
            {
                _eventBus.Publish(domainEvent);
                _logService.Log(LogLevel.Debug, $"Published {typeof(TEvent).Name} event");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error publishing {typeof(TEvent).Name} event: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes all subscription tokens
        /// </summary>
        public virtual void Dispose()
        {
            if (_isDisposed)
                return;

            foreach (var token in _subscriptionTokens)
            {
                try
                {
                    token.Dispose();
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error disposing subscription token: {ex.Message}");
                }
            }

            _subscriptionTokens.Clear();
            _isDisposed = true;
        }
    }
}

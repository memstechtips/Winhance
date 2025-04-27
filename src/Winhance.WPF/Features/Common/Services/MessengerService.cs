using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Messaging;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Implementation of IMessengerService that uses CommunityToolkit.Mvvm.Messenger
    /// </summary>
    public class MessengerService : IMessengerService
    {
        private readonly IMessenger _messenger;
        private readonly Dictionary<object, List<IDisposable>> _recipientTokens;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessengerService"/> class.
        /// </summary>
        public MessengerService()
        {
            _messenger = WeakReferenceMessenger.Default;
            _recipientTokens = new Dictionary<object, List<IDisposable>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessengerService"/> class with a specific messenger.
        /// </summary>
        /// <param name="messenger">The messenger to use.</param>
        public MessengerService(IMessenger messenger)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _recipientTokens = new Dictionary<object, List<IDisposable>>();
        }

        /// <inheritdoc />
        void IMessengerService.Send<TMessage>(TMessage message)
        {
            // Only MessageBase objects can be sent with the messenger
            if (message is MessageBase msgBase)
            {
                _messenger.Send(msgBase);
            }
        }

        /// <inheritdoc />
        void IMessengerService.Register<TMessage>(object recipient, Action<TMessage> action)
        {
            // Only reference types can be registered with the messenger
            if (typeof(TMessage).IsClass && typeof(TMessage).IsAssignableTo(typeof(MessageBase)))
            {
                // We need to use dynamic here to handle the generic type constraints
                dynamic typedRecipient = recipient;
                dynamic typedAction = action;
                RegisterInternal(typedRecipient, typedAction);
            }
        }

        private void RegisterInternal<TMessage>(object recipient, Action<TMessage> action) 
            where TMessage : MessageBase
        {
            // Register the recipient with the messenger
            _messenger.Register<TMessage>(recipient, (r, m) => action(m));
            
            // Create a token that will unregister the recipient when disposed
            var token = new RegistrationToken(() => _messenger.Unregister<TMessage>(recipient));

            // Keep track of the token for later cleanup
            if (!_recipientTokens.TryGetValue(recipient, out var tokens))
            {
                tokens = new List<IDisposable>();
                _recipientTokens[recipient] = tokens;
            }

            tokens.Add(token);
        }

        /// <summary>
        /// A token that unregisters a recipient when disposed
        /// </summary>
        private class RegistrationToken : IDisposable
        {
            private readonly Action _unregisterAction;
            private bool _isDisposed;

            public RegistrationToken(Action unregisterAction)
            {
                _unregisterAction = unregisterAction;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _unregisterAction();
                    _isDisposed = true;
                }
            }
        }

        /// <inheritdoc />
        void IMessengerService.Unregister(object recipient)
        {
            if (_recipientTokens.TryGetValue(recipient, out var tokens))
            {
                foreach (var token in tokens)
                {
                    token.Dispose();
                }

                _recipientTokens.Remove(recipient);
            }

            _messenger.UnregisterAll(recipient);
        }
    }
}

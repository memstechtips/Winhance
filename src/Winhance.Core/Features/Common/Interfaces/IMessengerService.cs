using System;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Provides functionality for messaging between components.
    /// </summary>
    public interface IMessengerService
    {
        /// <summary>
        /// Sends a message of the specified type.
        /// </summary>
        /// <typeparam name="TMessage">The type of the message to send.</typeparam>
        /// <param name="message">The message to send.</param>
        void Send<TMessage>(TMessage message);

        /// <summary>
        /// Registers a recipient for messages of the specified type.
        /// </summary>
        /// <typeparam name="TMessage">The type of message to register for.</typeparam>
        /// <param name="recipient">The recipient object.</param>
        /// <param name="action">The action to perform when a message is received.</param>
        void Register<TMessage>(object recipient, Action<TMessage> action);

        /// <summary>
        /// Unregisters a recipient from receiving messages.
        /// </summary>
        /// <param name="recipient">The recipient to unregister.</param>
        void Unregister(object recipient);
    }
}
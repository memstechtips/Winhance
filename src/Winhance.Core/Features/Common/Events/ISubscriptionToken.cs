using System;

namespace Winhance.Core.Features.Common.Events
{
    /// <summary>
    /// Represents a subscription to an event that can be disposed to unsubscribe
    /// </summary>
    public interface ISubscriptionToken : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier for this subscription
        /// </summary>
        Guid SubscriptionId { get; }

        /// <summary>
        /// Gets the type of event this subscription is for
        /// </summary>
        Type EventType { get; }
    }
}

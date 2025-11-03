using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Events.Features
{
    /// <summary>
    /// Event raised when a feature has completed composition (loading settings and initializing UI state).
    /// This event triggers tooltip initialization for the feature's settings.
    /// </summary>
    public class FeatureComposedEvent : IDomainEvent
    {
        /// <summary>
        /// Gets the timestamp when the event occurred.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets a unique identifier for the event instance.
        /// </summary>
        public Guid EventId { get; }

        /// <summary>
        /// The unique identifier of the feature that was composed.
        /// </summary>
        public string ModuleId { get; }

        /// <summary>
        /// The settings that were loaded and composed for this feature.
        /// </summary>
        public IEnumerable<SettingDefinition> Settings { get; }

        /// <summary>
        /// Initializes a new instance of the FeatureComposedEvent.
        /// </summary>
        /// <param name="moduleId">The unique identifier of the feature</param>
        /// <param name="settings">The settings that were composed</param>
        public FeatureComposedEvent(string moduleId, IEnumerable<SettingDefinition> settings)
        {
            ModuleId = moduleId ?? throw new System.ArgumentNullException(nameof(moduleId));
            Settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
            Timestamp = DateTime.UtcNow;
            EventId = Guid.NewGuid();
        }
    }
}

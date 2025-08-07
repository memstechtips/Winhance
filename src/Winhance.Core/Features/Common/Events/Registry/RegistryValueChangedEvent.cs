using System;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Events.Registry
{
    /// <summary>
    /// Domain event raised when a registry value changes
    /// </summary>
    public class RegistryValueChangedEvent : IDomainEvent
    {
        /// <summary>
        /// Gets the timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// Gets a unique identifier for the event instance
        /// </summary>
        public Guid EventId { get; }
        
        /// <summary>
        /// Gets the registry setting that was changed
        /// </summary>
        public RegistrySetting RegistrySetting { get; }

        /// <summary>
        /// Gets the old value before the change
        /// </summary>
        public object? OldValue { get; }

        /// <summary>
        /// Gets the new value after the change
        /// </summary>
        public object? NewValue { get; }

        /// <summary>
        /// Gets the path identifier for the registry value
        /// </summary>
        public string ValuePath { get; }

        /// <summary>
        /// Initializes a new instance of the RegistryValueChangedEvent
        /// </summary>
        /// <param name="registrySetting">The registry setting that changed</param>
        /// <param name="oldValue">The old value</param>
        /// <param name="newValue">The new value</param>
        public RegistryValueChangedEvent(RegistrySetting registrySetting, object? oldValue, object? newValue)
        {
            Timestamp = DateTime.UtcNow;
            EventId = Guid.NewGuid();
            RegistrySetting = registrySetting ?? throw new ArgumentNullException(nameof(registrySetting));
            OldValue = oldValue;
            NewValue = newValue;
            ValuePath = $"{registrySetting.Hive}\\{registrySetting.SubKey}\\{registrySetting.Name}";
        }
    }
}

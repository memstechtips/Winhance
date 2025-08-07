using System;
using Microsoft.Win32;

namespace Winhance.Core.Features.Common.Events.UI
{
    /// <summary>
    /// Event raised when tooltip data has been refreshed
    /// </summary>
    public class TooltipDataRefreshedEvent : IDomainEvent
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
        /// Gets the registry hive that was refreshed
        /// </summary>
        public RegistryHive Hive { get; }
        
        /// <summary>
        /// Gets the registry subkey that was refreshed
        /// </summary>
        public string SubKey { get; }
        
        /// <summary>
        /// Gets the registry value name that was refreshed
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Gets the full registry path that was refreshed
        /// </summary>
        public string RegistryPath { get; }

        /// <summary>
        /// Initializes a new instance of the TooltipDataRefreshedEvent
        /// </summary>
        /// <param name="hive">The registry hive</param>
        /// <param name="subKey">The registry subkey</param>
        /// <param name="name">The registry value name</param>
        public TooltipDataRefreshedEvent(RegistryHive hive, string subKey, string name)
        {
            Timestamp = DateTime.UtcNow;
            EventId = Guid.NewGuid();
            Hive = hive;
            SubKey = subKey ?? throw new ArgumentNullException(nameof(subKey));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            RegistryPath = $"{hive}\\{subKey}\\{name}";
        }
    }
}

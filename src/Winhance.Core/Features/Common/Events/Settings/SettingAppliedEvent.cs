using System;

namespace Winhance.Core.Features.Common.Events.Settings
{
    /// <summary>
    /// Domain event that is published when a setting has been successfully applied.
    /// This event allows other parts of the system to react to setting changes without coupling.
    /// </summary>
    public class SettingAppliedEvent : IDomainEvent
    {
        public DateTime Timestamp { get; }
        public Guid EventId { get; }

        /// <summary>
        /// The ID of the setting that was applied.
        /// </summary>
        public string SettingId { get; }

        /// <summary>
        /// Whether the setting was enabled or disabled.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// The value that was applied (for value-based settings like sliders, combo boxes).
        /// </summary>
        public object? Value { get; }

        public SettingAppliedEvent(string settingId, bool isEnabled, object? value = null)
        {
            Timestamp = DateTime.UtcNow;
            EventId = Guid.NewGuid();
            SettingId = settingId ?? throw new ArgumentNullException(nameof(settingId));
            IsEnabled = isEnabled;
            Value = value;
        }
    }
}

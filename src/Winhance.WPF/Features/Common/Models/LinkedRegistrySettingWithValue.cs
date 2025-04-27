using System;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// Wrapper class for RegistrySetting that includes the current value.
    /// Used for displaying registry settings with their current values in tooltips.
    /// </summary>
    public class LinkedRegistrySettingWithValue
    {
        /// <summary>
        /// Gets the underlying registry setting.
        /// </summary>
        public RegistrySetting Setting { get; }

        /// <summary>
        /// Gets or sets the current value of the registry setting.
        /// </summary>
        public object? CurrentValue { get; set; }

        /// <summary>
        /// Creates a new instance of the LinkedRegistrySettingWithValue class.
        /// </summary>
        /// <param name="setting">The registry setting.</param>
        /// <param name="currentValue">The current value of the registry setting.</param>
        public LinkedRegistrySettingWithValue(RegistrySetting setting, object? currentValue)
        {
            Setting = setting ?? throw new ArgumentNullException(nameof(setting));
            CurrentValue = currentValue;
        }
    }
}

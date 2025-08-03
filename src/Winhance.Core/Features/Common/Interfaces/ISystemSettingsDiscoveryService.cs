using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Customize.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for discovering current system settings state without side effects.
    /// Used during initialization to read current system state and update UI accordingly.
    /// </summary>
    public interface ISystemSettingsDiscoveryService
    {
        /// <summary>
        /// Gets the current state (enabled/disabled) of multiple customization settings.
        /// </summary>
        /// <param name="settings">The settings to check</param>
        /// <returns>Dictionary mapping setting ID to current enabled state</returns>
        Task<Dictionary<string, bool>> GetCurrentSettingsStateAsync(IEnumerable<CustomizationSetting> settings);

        /// <summary>
        /// Gets the current values of multiple customization settings (for ComboBox settings).
        /// </summary>
        /// <param name="settings">The settings to check</param>
        /// <returns>Dictionary mapping setting ID to current value</returns>
        Task<Dictionary<string, object?>> GetCurrentSettingsValuesAsync(IEnumerable<CustomizationSetting> settings);

        /// <summary>
        /// Gets both the state and values of multiple customization settings in a single call.
        /// </summary>
        /// <param name="settings">The settings to check</param>
        /// <returns>Dictionary mapping setting ID to (IsEnabled, CurrentValue) tuple</returns>
        Task<Dictionary<string, (bool IsEnabled, object? CurrentValue)>> GetCurrentSettingsStateAndValuesAsync(IEnumerable<CustomizationSetting> settings);
    }
}

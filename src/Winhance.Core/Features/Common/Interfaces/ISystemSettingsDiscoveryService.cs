using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for discovering current system settings state without side effects.
    /// Used during initialization to read current system state and update UI accordingly.
    /// </summary>
    public interface ISystemSettingsDiscoveryService
    {
        /// <summary>
        /// Gets the current state (enabled/disabled) of multiple application settings.
        /// </summary>
        /// <param name="settings">The settings to check</param>
        /// <returns>Dictionary mapping setting ID to current enabled state</returns>
        Task<Dictionary<string, bool>> GetCurrentSettingsStateAsync(IEnumerable<ApplicationSetting> settings);

        /// <summary>
        /// Gets the current values of multiple application settings (for ComboBox settings).
        /// </summary>
        /// <param name="settings">The settings to check</param>
        /// <returns>Dictionary mapping setting ID to current value</returns>
        Task<Dictionary<string, object?>> GetCurrentSettingsValuesAsync(IEnumerable<ApplicationSetting> settings);

        /// <summary>
        /// Gets both the state and values of multiple application settings in a single call.
        /// </summary>
        /// <param name="settings">The settings to check</param>
        /// <returns>Dictionary mapping setting ID to (IsEnabled, CurrentValue) tuple</returns>
        Task<Dictionary<string, (bool IsEnabled, object? CurrentValue)>> GetCurrentSettingsStateAndValuesAsync(IEnumerable<ApplicationSetting> settings);

        /// <summary>
        /// Gets individual registry values for tooltip display alongside aggregated setting data.
        /// This method reuses the same registry calls made during state discovery to avoid duplicate calls.
        /// </summary>
        /// <param name="settings">The settings to get individual registry values for</param>
        /// <returns>Dictionary mapping setting ID to individual registry values for tooltip display</returns>
        Task<Dictionary<string, Dictionary<RegistrySetting, object?>>> GetIndividualRegistryValuesAsync(IEnumerable<ApplicationSetting> settings);
    }
}

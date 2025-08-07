using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Registry interface for mapping setting IDs to their appropriate domain services.
    /// Provides a centralized way to route settings operations to the correct domain service.
    /// </summary>
    public interface IDomainServiceRegistry
    {
        /// <summary>
        /// Applies a setting using the appropriate domain service.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <param name="enable">Whether to enable or disable the setting.</param>
        /// <param name="value">Optional value for settings that require specific values.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplySettingAsync(string settingId, bool enable, object? value = null);

        /// <summary>
        /// Checks if a setting is enabled using the appropriate domain service.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <returns>True if the setting is enabled, false otherwise.</returns>
        Task<bool> IsSettingEnabledAsync(string settingId);

        /// <summary>
        /// Gets the current value of a setting using the appropriate domain service.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <returns>The current value of the setting, or null if not set.</returns>
        Task<object?> GetSettingValueAsync(string settingId);

        /// <summary>
        /// Gets the enabled state of multiple settings in a batch operation.
        /// </summary>
        /// <param name="settingIds">Collection of setting IDs to check.</param>
        /// <returns>Dictionary mapping setting IDs to their enabled state.</returns>
        Task<Dictionary<string, bool>> GetMultipleSettingsStateAsync(IEnumerable<string> settingIds);

        /// <summary>
        /// Gets the current values of multiple settings in a batch operation.
        /// </summary>
        /// <param name="settingIds">Collection of setting IDs to get values for.</param>
        /// <returns>Dictionary mapping setting IDs to their current values.</returns>
        Task<Dictionary<string, object?>> GetMultipleSettingsValuesAsync(IEnumerable<string> settingIds);

        /// <summary>
        /// Gets the domain service responsible for handling a specific setting ID.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <returns>The domain service that handles this setting.</returns>
        IDomainService GetDomainService(string settingId);
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Base interface for all domain services in the Winhance application.
    /// Provides common functionality that all domain services must implement.
    /// </summary>
    public interface IDomainService
    {
        /// <summary>
        /// Gets all settings managed by this domain service.
        /// </summary>
        /// <returns>Collection of application settings for this domain.</returns>
        Task<IEnumerable<ApplicationSetting>> GetSettingsAsync();

        /// <summary>
        /// Applies a setting with the specified enable state and optional value.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <param name="enable">Whether to enable or disable the setting.</param>
        /// <param name="value">Optional value for settings that require specific values.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplySettingAsync(string settingId, bool enable, object? value = null);

        /// <summary>
        /// Checks if a specific setting is currently enabled in the system.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <returns>True if the setting is enabled, false otherwise.</returns>
        Task<bool> IsSettingEnabledAsync(string settingId);

        /// <summary>
        /// Gets the current value of a specific setting from the system.
        /// </summary>
        /// <param name="settingId">The unique identifier of the setting.</param>
        /// <returns>The current value of the setting, or null if not set.</returns>
        Task<object?> GetSettingValueAsync(string settingId);

        /// <summary>
        /// Gets the domain name that this service handles.
        /// Used for routing settings to the appropriate domain service.
        /// </summary>
        string DomainName { get; }
    }
}

using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Strategy interface for applying different types of settings.
    /// Follows SRP by handling a specific setting application concern.
    /// </summary>
    public interface ISettingApplicationStrategy
    {
        /// <summary>
        /// Determines if this strategy can handle the given setting.
        /// </summary>
        /// <param name="setting">The setting to check.</param>
        /// <returns>True if this strategy can handle the setting.</returns>
        bool CanHandle(ApplicationSetting setting);

        /// <summary>
        /// Applies a binary toggle setting.
        /// </summary>
        /// <param name="setting">The setting to apply.</param>
        /// <param name="enable">Whether to enable or disable the setting.</param>
        Task ApplyBinaryToggleAsync(ApplicationSetting setting, bool enable);

        /// <summary>
        /// Applies a ComboBox setting using the centralized resolver pattern.
        /// </summary>
        /// <param name="setting">The ComboBox setting to apply.</param>
        /// <param name="comboBoxIndex">The selected ComboBox index.</param>
        Task ApplyComboBoxIndexAsync(ApplicationSetting setting, int comboBoxIndex);

        /// <summary>
        /// Applies a numeric up/down setting with a specific value.
        /// </summary>
        /// <param name="setting">The setting to apply.</param>
        /// <param name="value">The numeric value to set.</param>
        Task ApplyNumericUpDownAsync(ApplicationSetting setting, object value);

        /// <summary>
        /// Gets the current status of a setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <param name="settings">The available settings to search through.</param>
        /// <returns>True if the setting is enabled, false otherwise.</returns>
        Task<bool> GetSettingStatusAsync(
            string settingId,
            System.Collections.Generic.IEnumerable<ApplicationSetting> settings
        );

        /// <summary>
        /// Gets the current value of a setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting to get the value for.</param>
        /// <param name="settings">The available settings to search through.</param>
        /// <returns>The current value of the setting, or null if not found.</returns>
        Task<object?> GetSettingValueAsync(
            string settingId,
            System.Collections.Generic.IEnumerable<ApplicationSetting> settings
        );
    }
}

using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for a registry of settings.
    /// </summary>
    public interface ISettingsRegistry
    {
        /// <summary>
        /// Registers a setting in the registry.
        /// </summary>
        /// <param name="setting">The setting to register.</param>
        void RegisterSetting(ISettingItem setting);

        /// <summary>
        /// Gets a setting by its ID.
        /// </summary>
        /// <param name="id">The ID of the setting to get.</param>
        /// <returns>The setting if found, otherwise null.</returns>
        ISettingItem? GetSettingById(string id);

        /// <summary>
        /// Gets all settings in the registry.
        /// </summary>
        /// <returns>A list of all settings.</returns>
        List<ISettingItem> GetAllSettings();

        /// <summary>
        /// Gets all settings of a specific type.
        /// </summary>
        /// <typeparam name="T">The type of settings to get.</typeparam>
        /// <returns>A list of settings of the specified type.</returns>
        List<ISettingItem> GetSettingsByType<T>() where T : ISettingItem;
    }
}

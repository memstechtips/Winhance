using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Defines methods for managing dependencies between settings.
    /// </summary>
    public interface IDependencyManager
    {
        /// <summary>
        /// Determines if a setting can be enabled based on its dependencies.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <param name="allSettings">All available settings that might be dependencies.</param>
        /// <returns>True if the setting can be enabled; otherwise, false.</returns>
        bool CanEnableSetting(string settingId, IEnumerable<ISettingItem> allSettings);
        
        /// <summary>
        /// Handles the disabling of a setting by automatically disabling any dependent settings.
        /// </summary>
        /// <param name="settingId">The ID of the setting that was disabled.</param>
        /// <param name="allSettings">All available settings that might depend on the disabled setting.</param>
        void HandleSettingDisabled(string settingId, IEnumerable<ISettingItem> allSettings);
        
        /// <summary>
        /// Handles the enabling of a setting by automatically enabling any required settings.
        /// </summary>
        /// <param name="settingId">The ID of the setting that is being enabled.</param>
        /// <param name="allSettings">All available settings that might be required by the enabled setting.</param>
        /// <returns>True if all required settings were enabled successfully; otherwise, false.</returns>
        bool HandleSettingEnabled(string settingId, IEnumerable<ISettingItem> allSettings);

        /// <summary>
        /// Gets a list of unsatisfied dependencies for a setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <param name="allSettings">All available settings that might be dependencies.</param>
        /// <returns>A list of settings that are required by the specified setting but are not enabled.</returns>
        List<ISettingItem> GetUnsatisfiedDependencies(string settingId, IEnumerable<ISettingItem> allSettings);

        /// <summary>
        /// Enables all dependencies in the provided list.
        /// </summary>
        /// <param name="dependencies">The dependencies to enable.</param>
        /// <returns>True if all dependencies were enabled successfully; otherwise, false.</returns>
        bool EnableDependencies(IEnumerable<ISettingItem> dependencies);
    }
}

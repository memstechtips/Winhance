using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for managing dependencies between domain settings (ApplicationSetting objects).
    /// This handles business logic dependencies at the domain layer.
    /// </summary>
    public interface IDomainDependencyService
    {
        /// <summary>
        /// Determines if a setting can be enabled based on its domain dependencies.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <param name="allSettings">All available settings that might be dependencies.</param>
        /// <param name="currentSettingsState">Current state of all settings (enabled/disabled).</param>
        /// <returns>True if the setting can be enabled; otherwise, false.</returns>
        bool CanEnableSetting(string settingId, IEnumerable<ApplicationSetting> allSettings, Dictionary<string, bool> currentSettingsState);

        /// <summary>
        /// Gets the list of settings that must be enabled before the specified setting can be enabled.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <param name="allSettings">All available settings that might be dependencies.</param>
        /// <returns>List of setting IDs that must be enabled first.</returns>
        IEnumerable<string> GetRequiredDependencies(string settingId, IEnumerable<ApplicationSetting> allSettings);

        /// <summary>
        /// Gets the list of settings that must be disabled before the specified setting can be enabled.
        /// </summary>
        /// <param name="settingId">The ID of the setting to check.</param>
        /// <param name="allSettings">All available settings that might be dependencies.</param>
        /// <returns>List of setting IDs that must be disabled first.</returns>
        IEnumerable<string> GetConflictingDependencies(string settingId, IEnumerable<ApplicationSetting> allSettings);

        /// <summary>
        /// Gets the list of settings that will be automatically disabled when the specified setting is disabled.
        /// </summary>
        /// <param name="settingId">The ID of the setting being disabled.</param>
        /// <param name="allSettings">All available settings that might depend on this setting.</param>
        /// <returns>List of setting IDs that depend on the specified setting.</returns>
        IEnumerable<string> GetDependentSettings(string settingId, IEnumerable<ApplicationSetting> allSettings);

        /// <summary>
        /// Validates that the current settings state is consistent with all dependencies.
        /// </summary>
        /// <param name="allSettings">All available settings.</param>
        /// <param name="currentSettingsState">Current state of all settings.</param>
        /// <returns>Dictionary of setting IDs and their validation errors (empty if valid).</returns>
        Dictionary<string, string> ValidateSettingsDependencies(IEnumerable<ApplicationSetting> allSettings, Dictionary<string, bool> currentSettingsState);

        /// <summary>
        /// Attempts to automatically resolve dependencies for a setting.
        /// </summary>
        /// <param name="settingId">The ID of the setting to enable.</param>
        /// <param name="allSettings">All available settings.</param>
        /// <param name="currentSettingsState">Current state of all settings.</param>
        /// <returns>Dictionary of setting IDs and their required states to satisfy dependencies.</returns>
        Dictionary<string, bool> GetDependencyResolutionPlan(string settingId, IEnumerable<ApplicationSetting> allSettings, Dictionary<string, bool> currentSettingsState);
    }
}

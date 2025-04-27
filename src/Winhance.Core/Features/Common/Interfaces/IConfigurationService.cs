using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Defines methods for saving and loading application configuration files.
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Saves a configuration file containing the selected items.
        /// </summary>
        /// <typeparam name="T">The type of items to save.</typeparam>
        /// <param name="items">The collection of items to save.</param>
        /// <param name="configType">The type of configuration being saved.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        Task<bool> SaveConfigurationAsync<T>(IEnumerable<T> items, string configType);

        /// <summary>
        /// Loads a configuration file and returns the configuration file.
        /// </summary>
        /// <param name="configType">The type of configuration being loaded.</param>
        /// <returns>A task representing the asynchronous operation. Returns the configuration file if successful, null otherwise.</returns>
        Task<ConfigurationFile> LoadConfigurationAsync(string configType);

        /// <summary>
        /// Saves a unified configuration file containing settings for multiple parts of the application.
        /// </summary>
        /// <param name="unifiedConfig">The unified configuration to save.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        Task<bool> SaveUnifiedConfigurationAsync(UnifiedConfigurationFile unifiedConfig);

        /// <summary>
        /// Loads a unified configuration file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns the unified configuration file if successful, null otherwise.</returns>
        Task<UnifiedConfigurationFile> LoadUnifiedConfigurationAsync();

        /// <summary>
        /// Creates a unified configuration file from individual configuration sections.
        /// </summary>
        /// <param name="sections">Dictionary of section names and their corresponding configuration items.</param>
        /// <param name="includedSections">List of section names to include in the unified configuration.</param>
        /// <returns>A unified configuration file.</returns>
        UnifiedConfigurationFile CreateUnifiedConfiguration(Dictionary<string, IEnumerable<ISettingItem>> sections, IEnumerable<string> includedSections);

        /// <summary>
        /// Extracts a specific section from a unified configuration file.
        /// </summary>
        /// <param name="unifiedConfig">The unified configuration file.</param>
        /// <param name="sectionName">The name of the section to extract.</param>
        /// <returns>A configuration file containing only the specified section.</returns>
        ConfigurationFile ExtractSectionFromUnifiedConfiguration(UnifiedConfigurationFile unifiedConfig, string sectionName);
    }
}

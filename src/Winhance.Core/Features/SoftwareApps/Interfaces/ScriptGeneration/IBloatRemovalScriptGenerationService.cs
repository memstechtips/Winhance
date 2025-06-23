using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration
{
    /// <summary>
    /// Service for generating bloat removal scripts.
    /// Follows the Single Responsibility Principle by focusing only on script generation.
    /// </summary>
    public interface IBloatRemovalScriptGenerationService
    {
        /// <summary>
        /// Creates a batch removal script for multiple applications.
        /// </summary>
        /// <param name="appNames">The application names to generate a removal script for.</param>
        /// <param name="appsWithRegistry">Dictionary mapping app names to registry settings.</param>
        /// <param name="appSubPackages">Optional dictionary of app subpackages to include.</param>
        /// <returns>A removal script object with the generated content.</returns>
        Task<RemovalScript> CreateBatchRemovalScriptAsync(
            List<string> appNames,
            Dictionary<string, List<AppRegistrySetting>> appsWithRegistry,
            Dictionary<string, string[]>? appSubPackages = null
        );

        /// <summary>
        /// Creates a removal script for a single application.
        /// </summary>
        /// <param name="app">The application to generate a removal script for.</param>
        /// <returns>The generated script content.</returns>
        Task<string> CreateSingleAppRemovalScriptContentAsync(AppInfo app);

        /// <summary>
        /// Gets the script content for removing a single application.
        /// </summary>
        /// <param name="app">The application to generate a removal script for.</param>
        /// <returns>The generated script content as a string.</returns>
        Task<string> GetSingleAppRemovalScriptContentAsync(AppInfo app);
    }
}

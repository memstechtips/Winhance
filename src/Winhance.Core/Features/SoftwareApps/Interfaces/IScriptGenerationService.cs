using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    /// <summary>
    /// Service for generating scripts for application removal and management.
    /// </summary>
    public interface IScriptGenerationService
    {
        /// <summary>
        /// Creates a batch removal script for applications.
        /// </summary>
        /// <param name="appNames">The application names to generate a removal script for.</param>
        /// <param name="appsWithRegistry">Dictionary mapping app names to registry settings.</param>
        /// <returns>A removal script object.</returns>
        Task<RemovalScript> CreateBatchRemovalScriptAsync(
            List<string> appNames,
            Dictionary<string, List<AppRegistrySetting>> appsWithRegistry);

        /// <summary>
        /// Creates a batch removal script for a single application.
        /// </summary>
        /// <param name="scriptPath">The path where the script should be saved.</param>
        /// <param name="app">The application to generate a removal script for.</param>
        /// <returns>True if the script was created successfully; otherwise, false.</returns>
        Task<bool> CreateBatchRemovalScriptAsync(string scriptPath, AppInfo app);

        /// <summary>
        /// Updates the bloat removal script for an installed application.
        /// </summary>
        /// <param name="app">The application to update the script for.</param>
        /// <returns>True if the script was updated successfully; otherwise, false.</returns>
        Task<bool> UpdateBloatRemovalScriptForInstalledAppAsync(AppInfo app);

        /// <summary>
        /// Registers a removal task in the Windows Task Scheduler.
        /// </summary>
        /// <param name="script">The script to register.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RegisterRemovalTaskAsync(RemovalScript script);

        /// <summary>
        /// Registers a removal task in the Windows Task Scheduler.
        /// </summary>
        /// <param name="taskName">The name of the task.</param>
        /// <param name="scriptPath">The path to the script.</param>
        /// <returns>True if the task was registered successfully; otherwise, false.</returns>
        Task<bool> RegisterRemovalTaskAsync(string taskName, string scriptPath);

        /// <summary>
        /// Saves a script to a file.
        /// </summary>
        /// <param name="script">The script to save.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveScriptAsync(RemovalScript script);

        /// <summary>
        /// Saves a script to a file.
        /// </summary>
        /// <param name="scriptPath">The path where the script should be saved.</param>
        /// <param name="scriptContent">The content of the script.</param>
        /// <returns>True if the script was saved successfully; otherwise, false.</returns>
        Task<bool> SaveScriptAsync(string scriptPath, string scriptContent);
    }
}
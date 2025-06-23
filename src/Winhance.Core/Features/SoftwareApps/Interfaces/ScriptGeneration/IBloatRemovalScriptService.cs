using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration
{
    /// <summary>
    /// Service for managing and executing the BloatRemoval script.
    /// </summary>
    public interface IBloatRemovalScriptService
    {
        /// <summary>
        /// Adds Windows apps to the BloatRemoval script.
        /// </summary>
        /// <param name="appInfos">The list of app information to add to the script.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the updated script.</returns>
        Task<RemovalScript> AddAppsToScriptAsync(
            List<AppInfo> appInfos,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds Windows capabilities to the BloatRemoval script.
        /// </summary>
        /// <param name="capabilities">The list of capabilities to add to the script.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the updated script.</returns>
        Task<RemovalScript> AddCapabilitiesToScriptAsync(
            List<CapabilityInfo> capabilities,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds Windows optional features to the BloatRemoval script.
        /// </summary>
        /// <param name="features">The list of features to add to the script.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the updated script.</returns>
        Task<RemovalScript> AddFeaturesToScriptAsync(
            List<FeatureInfo> features,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the BloatRemoval script to remove all items added to it.
        /// </summary>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the result of the execution.</returns>
        Task<OperationResult<bool>> ExecuteScriptAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current BloatRemoval script.
        /// </summary>
        /// <returns>A task representing the asynchronous operation with the current script.</returns>
        Task<RemovalScript> GetCurrentScriptAsync();

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

        /// <summary>
        /// Gets the content of a script from a file.
        /// </summary>
        /// <param name="scriptPath">The path to the script file.</param>
        /// <returns>The content of the script.</returns>
        Task<string> GetScriptContentAsync(string scriptPath);
    }
}

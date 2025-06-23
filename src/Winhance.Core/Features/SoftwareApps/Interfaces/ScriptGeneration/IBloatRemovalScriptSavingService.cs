using System;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration
{
    /// <summary>
    /// Service for saving bloat removal scripts to the file system.
    /// </summary>
    public interface IBloatRemovalScriptSavingService
    {
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

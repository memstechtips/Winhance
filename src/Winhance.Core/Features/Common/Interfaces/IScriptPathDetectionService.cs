using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for detecting script paths and providing cached results.
    /// </summary>
    public interface IScriptPathDetectionService
    {
        /// <summary>
        /// Gets the cached script path information.
        /// </summary>
        /// <returns>Immutable script path information.</returns>
        ScriptPathInfo GetScriptPathInfo();

        /// <summary>
        /// Gets the scripts directory path.
        /// </summary>
        /// <returns>The full path to the scripts directory.</returns>
        string GetScriptsDirectory();
    }
}

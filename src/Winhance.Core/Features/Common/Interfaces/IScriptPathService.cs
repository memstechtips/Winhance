using System;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for managing script paths with support for both installed and portable versions.
    /// </summary>
    public interface IScriptPathService
    {
        /// <summary>
        /// Gets the directory where scripts should be stored.
        /// Automatically detects if running from external media and uses appropriate fallback.
        /// </summary>
        /// <returns>The full path to the scripts directory.</returns>
        string GetScriptsDirectory();

        /// <summary>
        /// Gets the full path to a specific script file.
        /// </summary>
        /// <param name="scriptName">The name of the script (without .ps1 extension).</param>
        /// <returns>The full path to the script file.</returns>
        string GetScriptPath(string scriptName);

        /// <summary>
        /// Gets whether the application is running from external/removable media.
        /// </summary>
        /// <returns>True if running from external media, false otherwise.</returns>
        bool IsRunningFromExternalMedia();

        /// <summary>
        /// Gets the directory where the application is installed/running from.
        /// </summary>
        /// <returns>The application directory path.</returns>
        string GetApplicationDirectory();
    }
}

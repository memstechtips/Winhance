using System;

namespace Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration
{
    /// <summary>
    /// Provides methods for modifying package-related script content.
    /// </summary>
    public interface IPackageScriptModifier
    {
        /// <summary>
        /// Removes a package from the script content.
        /// </summary>
        /// <param name="scriptContent">The script content.</param>
        /// <param name="packageName">The name of the package to remove.</param>
        /// <returns>The updated script content.</returns>
        string RemovePackageFromScript(string scriptContent, string packageName);
    }
}
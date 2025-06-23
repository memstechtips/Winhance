using System;

namespace Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration
{
    /// <summary>
    /// Provides methods for modifying registry-related script content.
    /// </summary>
    public interface IRegistryScriptModifier
    {
        /// <summary>
        /// Removes app-specific registry settings from the script content.
        /// </summary>
        /// <param name="scriptContent">The script content.</param>
        /// <param name="appName">The name of the app whose registry settings should be removed.</param>
        /// <returns>The updated script content.</returns>
        string RemoveAppRegistrySettingsFromScript(string scriptContent, string appName);
    }
}
using System;

namespace Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration
{
    /// <summary>
    /// Provides methods for modifying feature-related script content.
    /// </summary>
    public interface IFeatureScriptModifier
    {
        /// <summary>
        /// Removes an optional feature from the script content.
        /// </summary>
        /// <param name="scriptContent">The script content.</param>
        /// <param name="featureName">The name of the optional feature to remove.</param>
        /// <returns>The updated script content.</returns>
        string RemoveOptionalFeatureFromScript(string scriptContent, string featureName);
    }
}
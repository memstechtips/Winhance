using System;

namespace Winhance.Infrastructure.Features.Common.ScriptGeneration
{
    /// <summary>
    /// Provides methods for modifying capability-related script content.
    /// </summary>
    public interface ICapabilityScriptModifier
    {
        /// <summary>
        /// Removes a capability from the script content.
        /// </summary>
        /// <param name="scriptContent">The script content.</param>
        /// <param name="capabilityName">The name of the capability to remove.</param>
        /// <returns>The updated script content.</returns>
        string RemoveCapabilityFromScript(string scriptContent, string capabilityName);
    }
}
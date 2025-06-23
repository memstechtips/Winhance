using System;

namespace Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration
{
    /// <summary>
    /// Provides templates for PowerShell scripts used in the application.
    /// </summary>
    public interface IBloatRemovalScriptTemplateProvider
    {
        /// <summary>
        /// Gets the complete template for the BloatRemoval script.
        /// </summary>
        /// <returns>The full template string for the BloatRemoval script.</returns>
        string GetFullScriptTemplate();
    }
}

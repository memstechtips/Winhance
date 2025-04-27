using System;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Provides templates for PowerShell scripts used in the application.
    /// </summary>
    public interface IScriptTemplateProvider
    {
        /// <summary>
        /// Gets the template for removing a package.
        /// </summary>
        /// <returns>The template string for package removal.</returns>
        string GetPackageRemovalTemplate();

        /// <summary>
        /// Gets the template for removing a capability.
        /// </summary>
        /// <returns>The template string for capability removal.</returns>
        string GetCapabilityRemovalTemplate();

        /// <summary>
        /// Gets the template for removing an optional feature.
        /// </summary>
        /// <returns>The template string for optional feature removal.</returns>
        string GetFeatureRemovalTemplate();

        /// <summary>
        /// Gets the template for a registry setting operation.
        /// </summary>
        /// <param name="isDelete">True if this is a delete operation; false if it's a set operation.</param>
        /// <returns>The template string for registry operations.</returns>
        string GetRegistrySettingTemplate(bool isDelete);

        /// <summary>
        /// Gets the header for a script.
        /// </summary>
        /// <param name="scriptName">The name of the script.</param>
        /// <returns>The header string for the script.</returns>
        string GetScriptHeader(string scriptName);

        /// <summary>
        /// Gets the footer for a script.
        /// </summary>
        /// <returns>The footer string for the script.</returns>
        string GetScriptFooter();
    }
}
using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration
{
    /// <summary>
    /// Service for building script content.
    /// </summary>
    public interface IBloatRemovalScriptBuilderService
    {
        /// <summary>
        /// Builds a script for removing packages.
        /// </summary>
        /// <param name="packageNames">The names of the packages to remove.</param>
        /// <returns>The script content.</returns>
        string BuildPackageRemovalScript(IEnumerable<string> packageNames);

        /// <summary>
        /// Builds a script for removing capabilities.
        /// </summary>
        /// <param name="capabilityNames">The names of the capabilities to remove.</param>
        /// <returns>The script content.</returns>
        string BuildCapabilityRemovalScript(IEnumerable<string> capabilityNames);

        /// <summary>
        /// Builds a script for removing features.
        /// </summary>
        /// <param name="featureNames">The names of the features to remove.</param>
        /// <returns>The script content.</returns>
        string BuildFeatureRemovalScript(IEnumerable<string> featureNames);

        /// <summary>
        /// Builds a script for registry operations.
        /// </summary>
        /// <param name="registrySettings">Dictionary mapping app names to registry settings.</param>
        /// <returns>The script content.</returns>
        string BuildRegistryScript(Dictionary<string, List<AppRegistrySetting>> registrySettings);

        /// <summary>
        /// Builds a complete removal script.
        /// </summary>
        /// <param name="packageNames">The names of the packages to remove.</param>
        /// <param name="capabilityNames">The names of the capabilities to remove.</param>
        /// <param name="featureNames">The names of the features to remove.</param>
        /// <param name="registrySettings">Dictionary mapping app names to registry settings.</param>
        /// <param name="subPackages">Dictionary mapping app names to their subpackages.</param>
        /// <returns>The script content.</returns>
        string BuildCompleteRemovalScript(
            IEnumerable<string> packageNames,
            IEnumerable<string> capabilityNames,
            IEnumerable<string> featureNames,
            Dictionary<string, List<AppRegistrySetting>> registrySettings,
            Dictionary<string, string[]> subPackages);

        /// <summary>
        /// Builds a script for removing a single app.
        /// </summary>
        /// <param name="app">The app to remove.</param>
        /// <returns>The script content.</returns>
        string BuildSingleAppRemovalScript(AppInfo app);
    }
}
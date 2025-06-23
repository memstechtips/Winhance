using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;

/// <summary>
/// Provides methods for updating script content.
/// </summary>
public interface IScriptUpdateService
{
    /// <summary>
    /// Updates an existing BloatRemoval script with new entries.
    /// </summary>
    /// <param name="appNames">The names of the applications to add or remove.</param>
    /// <param name="appsWithRegistry">Dictionary mapping app names to their registry settings.</param>
    /// <param name="appSubPackages">Dictionary mapping app names to their subpackages.</param>
    /// <param name="isInstallOperation">True if this is an install operation (remove from script), false if it's a removal operation (add to script).</param>
    /// <returns>The updated removal script.</returns>
    Task<RemovalScript> UpdateExistingBloatRemovalScriptAsync(
        List<string> appNames,
        Dictionary<string, List<AppRegistrySetting>> appsWithRegistry,
        Dictionary<string, string[]> appSubPackages,
        bool isInstallOperation = false);

    /// <summary>
    /// Updates the capabilities array in the script by adding or removing capabilities.
    /// </summary>
    /// <param name="scriptContent">The script content.</param>
    /// <param name="capabilities">The capabilities to add or remove.</param>
    /// <param name="isInstallOperation">True if this is an install operation (remove from script), false if it's a removal operation (add to script).</param>
    /// <returns>The updated script content.</returns>
    string UpdateCapabilitiesArrayInScript(string scriptContent, List<string> capabilities, bool isInstallOperation = false);

    /// <summary>
    /// Updates the packages array in the script by adding or removing packages.
    /// </summary>
    /// <param name="scriptContent">The script content.</param>
    /// <param name="packages">The packages to add or remove.</param>
    /// <param name="isInstallOperation">True if this is an install operation (remove from script), false if it's a removal operation (add to script).</param>
    /// <returns>The updated script content.</returns>
    string UpdatePackagesArrayInScript(string scriptContent, List<string> packages, bool isInstallOperation = false);

    /// <summary>
    /// Updates the optional features in the script by adding or removing features.
    /// </summary>
    /// <param name="scriptContent">The script content.</param>
    /// <param name="features">The features to add or remove.</param>
    /// <param name="isInstallOperation">True if this is an install operation (remove from script), false if it's a removal operation (add to script).</param>
    /// <returns>The updated script content.</returns>
    string UpdateOptionalFeaturesInScript(string scriptContent, List<string> features, bool isInstallOperation = false);

    /// <summary>
    /// Updates the registry settings in the script by adding or removing settings.
    /// </summary>
    /// <param name="scriptContent">The script content.</param>
    /// <param name="appsWithRegistry">Dictionary mapping app names to their registry settings.</param>
    /// <param name="isInstallOperation">True if this is an install operation (remove from script), false if it's a removal operation (add to script).</param>
    /// <returns>The updated script content.</returns>
    string UpdateRegistrySettingsInScript(string scriptContent, Dictionary<string, List<AppRegistrySetting>> appsWithRegistry, bool isInstallOperation = false);
}

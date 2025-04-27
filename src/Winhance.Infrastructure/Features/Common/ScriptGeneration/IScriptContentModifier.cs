using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.Common.ScriptGeneration;

/// <summary>
/// Provides methods for modifying script content.
/// </summary>
public interface IScriptContentModifier
{
    /// <summary>
    /// Removes a capability from the script content.
    /// </summary>
    /// <param name="scriptContent">The script content.</param>
    /// <param name="capabilityName">The name of the capability to remove.</param>
    /// <returns>The updated script content.</returns>
    string RemoveCapabilityFromScript(string scriptContent, string capabilityName);

    /// <summary>
    /// Removes a package from the script content.
    /// </summary>
    /// <param name="scriptContent">The script content.</param>
    /// <param name="packageName">The name of the package to remove.</param>
    /// <returns>The updated script content.</returns>
    string RemovePackageFromScript(string scriptContent, string packageName);

    /// <summary>
    /// Removes an optional feature from the script content.
    /// </summary>
    /// <param name="scriptContent">The script content.</param>
    /// <param name="featureName">The name of the optional feature to remove.</param>
    /// <returns>The updated script content.</returns>
    string RemoveOptionalFeatureFromScript(string scriptContent, string featureName);

    /// <summary>
    /// Removes app-specific registry settings from the script content.
    /// </summary>
    /// <param name="scriptContent">The script content.</param>
    /// <param name="appName">The name of the app whose registry settings should be removed.</param>
    /// <returns>The updated script content.</returns>
    string RemoveAppRegistrySettingsFromScript(string scriptContent, string appName);
}

using System;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Implementation of IBloatRemovalScriptContentModifier that delegates to specialized modifiers.
    /// </summary>
    public class CompositeBloatRemovalScriptModifier : IBloatRemovalScriptContentModifier
    {
        private readonly IPackageScriptModifier _packageModifier;
        private readonly ICapabilityScriptModifier _capabilityModifier;
        private readonly IFeatureScriptModifier _featureModifier;
        private readonly IRegistryScriptModifier _registryModifier;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeBloatRemovalScriptModifier"/> class.
        /// </summary>
        /// <param name="packageModifier">The package script modifier.</param>
        /// <param name="capabilityModifier">The capability script modifier.</param>
        /// <param name="featureModifier">The feature script modifier.</param>
        /// <param name="registryModifier">The registry script modifier.</param>
        public CompositeBloatRemovalScriptModifier(
            IPackageScriptModifier packageModifier,
            ICapabilityScriptModifier capabilityModifier,
            IFeatureScriptModifier featureModifier,
            IRegistryScriptModifier registryModifier)
        {
            _packageModifier = packageModifier ?? throw new ArgumentNullException(nameof(packageModifier));
            _capabilityModifier = capabilityModifier ?? throw new ArgumentNullException(nameof(capabilityModifier));
            _featureModifier = featureModifier ?? throw new ArgumentNullException(nameof(featureModifier));
            _registryModifier = registryModifier ?? throw new ArgumentNullException(nameof(registryModifier));
        }

        /// <inheritdoc/>
        public string RemoveCapabilityFromScript(string scriptContent, string capabilityName)
        {
            return _capabilityModifier.RemoveCapabilityFromScript(scriptContent, capabilityName);
        }

        /// <inheritdoc/>
        public string RemovePackageFromScript(string scriptContent, string packageName)
        {
            return _packageModifier.RemovePackageFromScript(scriptContent, packageName);
        }

        /// <inheritdoc/>
        public string RemoveOptionalFeatureFromScript(string scriptContent, string featureName)
        {
            return _featureModifier.RemoveOptionalFeatureFromScript(scriptContent, featureName);
        }

        /// <inheritdoc/>
        public string RemoveAppRegistrySettingsFromScript(string scriptContent, string appName)
        {
            return _registryModifier.RemoveAppRegistrySettingsFromScript(scriptContent, appName);
        }
    }
}
using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Factory for creating script objects.
    /// </summary>
    public interface IScriptFactory
    {
        /// <summary>
        /// Creates a batch removal script.
        /// </summary>
        /// <param name="appNames">The names of the applications to remove.</param>
        /// <param name="appsWithRegistry">Dictionary mapping app names to registry settings.</param>
        /// <param name="appSubPackages">Dictionary mapping app names to their subpackages.</param>
        /// <returns>A removal script object.</returns>
        RemovalScript CreateBatchRemovalScript(
            List<string> appNames,
            Dictionary<string, List<AppRegistrySetting>> appsWithRegistry,
            Dictionary<string, string[]> appSubPackages = null);

        /// <summary>
        /// Creates a single app removal script.
        /// </summary>
        /// <param name="app">The app to remove.</param>
        /// <returns>A removal script object.</returns>
        RemovalScript CreateSingleAppRemovalScript(AppInfo app);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Factory for creating PowerShell script objects.
    /// </summary>
    public class BloatRemovalScriptFactory : IBloatRemovalScriptFactory
    {
        private readonly IBloatRemovalScriptBuilderService _bloatRemovalScriptBuilderService;
        private readonly ILogService _logService;
        private readonly IAppDiscoveryService _appDiscoveryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="BloatRemovalScriptFactory"/> class.
        /// </summary>
        /// <param name="bloatRemovalScriptBuilderService">The script builder service.</param>
        /// <param name="logService">The logging service.</param>
        /// <param name="appDiscoveryService">The app discovery service.</param>
        public BloatRemovalScriptFactory(
            IBloatRemovalScriptBuilderService bloatRemovalScriptBuilderService,
            ILogService logService,
            IAppDiscoveryService appDiscoveryService
        )
        {
            _bloatRemovalScriptBuilderService =
                bloatRemovalScriptBuilderService
                ?? throw new ArgumentNullException(nameof(bloatRemovalScriptBuilderService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _appDiscoveryService =
                appDiscoveryService ?? throw new ArgumentNullException(nameof(appDiscoveryService));
        }

        /// <inheritdoc/>
        public RemovalScript CreateBatchRemovalScript(
            List<string> appNames,
            Dictionary<string, List<AppRegistrySetting>> appsWithRegistry,
            Dictionary<string, string[]> appSubPackages = null
        )
        {
            try
            {
                _logService.LogInformation(
                    $"Creating batch removal script for {appNames.Count} apps"
                );

                // Categorize apps into packages, capabilities, and features
                var (packages, capabilities, features) = CategorizeApps(appNames);

                // Build the script content
                string scriptContent = _bloatRemovalScriptBuilderService.BuildCompleteRemovalScript(
                    packages,
                    capabilities,
                    features,
                    appsWithRegistry,
                    appSubPackages
                );

                // Return the RemovalScript object
                return new RemovalScript
                {
                    Name = "BloatRemoval",
                    Content = scriptContent,
                    TargetScheduledTaskName = "Winhance\\BloatRemoval",
                    RunOnStartup = true,
                };
            }
            catch (Exception ex)
            {
                _logService.LogError("Error creating batch removal script", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public RemovalScript CreateSingleAppRemovalScript(AppInfo app)
        {
            try
            {
                _logService.LogInformation(
                    $"Creating single app removal script for {app.PackageName}"
                );

                // Build the script content
                string scriptContent =
                    _bloatRemovalScriptBuilderService.BuildSingleAppRemovalScript(app);

                // Return the RemovalScript object
                return new RemovalScript
                {
                    Name = $"Remove_{app.PackageName.Replace(".", "_")}",
                    Content = scriptContent,
                    TargetScheduledTaskName =
                        $"Winhance\\Remove_{app.PackageName.Replace(".", "_")}",
                    RunOnStartup = false,
                };
            }
            catch (Exception ex)
            {
                _logService.LogError(
                    $"Error creating single app removal script for {app.PackageName}",
                    ex
                );
                throw;
            }
        }

        /// <summary>
        /// Categorizes apps into packages, capabilities, and features.
        /// </summary>
        /// <param name="appNames">The app names to categorize.</param>
        /// <returns>A tuple containing lists of packages, capabilities, and features.</returns>
        private (List<string>, List<string>, List<string>) CategorizeApps(List<string> appNames)
        {
            var packages = new List<string>();
            var capabilities = new List<string>();
            var features = new List<string>();

            // Get all standard apps to check their types
            var allApps = _appDiscoveryService.GetStandardAppsAsync().GetAwaiter().GetResult();
            var appInfoDict = allApps.ToDictionary(a => a.PackageName, a => a);

            foreach (var appName in appNames)
            {
                if (appInfoDict.TryGetValue(appName, out var appInfo))
                {
                    switch (appInfo.Type)
                    {
                        case AppType.StandardApp:
                            packages.Add(appName);
                            break;
                        case AppType.Capability:
                            capabilities.Add(appName);
                            break;
                        case AppType.OptionalFeature:
                            features.Add(appName);
                            break;
                        default:
                            // Default to package if type is unknown
                            packages.Add(appName);
                            break;
                    }
                }
                else
                {
                    // If app is not found in the dictionary, assume it's a package
                    packages.Add(appName);
                }
            }

            return (packages, capabilities, features);
        }
    }
}

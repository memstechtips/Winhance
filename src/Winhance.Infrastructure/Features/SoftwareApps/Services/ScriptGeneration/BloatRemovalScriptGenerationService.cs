using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Service for generating bloat removal scripts.
    /// Follows the Single Responsibility Principle by focusing only on script generation.
    /// </summary>
    public class BloatRemovalScriptGenerationService : IBloatRemovalScriptGenerationService
    {
        private readonly ILogService _logService;
        private readonly IAppDiscoveryService _appDiscoveryService;
        private readonly IBloatRemovalScriptBuilderService _bloatRemovalScriptBuilderService;
        private readonly IBloatRemovalScriptFactory _bloatRemovalScriptFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="BloatRemovalScriptGenerationService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="appDiscoveryService">The app discovery service.</param>
        /// <param name="bloatRemovalScriptBuilderService">The script builder service.</param>
        /// <param name="bloatRemovalScriptFactory">The script factory.</param>
        public BloatRemovalScriptGenerationService(
            ILogService logService,
            IAppDiscoveryService appDiscoveryService,
            IBloatRemovalScriptBuilderService bloatRemovalScriptBuilderService,
            IBloatRemovalScriptFactory bloatRemovalScriptFactory
        )
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _appDiscoveryService = appDiscoveryService ?? throw new ArgumentNullException(nameof(appDiscoveryService));
            _bloatRemovalScriptBuilderService = bloatRemovalScriptBuilderService ?? throw new ArgumentNullException(nameof(bloatRemovalScriptBuilderService));
            _bloatRemovalScriptFactory = bloatRemovalScriptFactory ?? throw new ArgumentNullException(nameof(bloatRemovalScriptFactory));
        }

        /// <inheritdoc/>
        public async Task<RemovalScript> CreateBatchRemovalScriptAsync(
            List<string> appNames,
            Dictionary<string, List<AppRegistrySetting>> appsWithRegistry,
            Dictionary<string, string[]>? appSubPackages = null
        )
        {
            try
            {
                _logService.LogInformation(
                    $"Creating batch removal script for {appNames.Count} apps"
                );

                // If no subpackages dictionary was provided, create one by looking up subpackages
                if (appSubPackages == null)
                {
                    // Get all standard apps to check for subpackages
                    var allRemovableApps = (await _appDiscoveryService.GetStandardAppsAsync()).ToList();

                    // Create a dictionary to store subpackages for each app
                    appSubPackages = new Dictionary<string, string[]>();

                    // Find subpackages for each app in the list
                    foreach (var appName in appNames)
                    {
                        var appInfo = allRemovableApps.FirstOrDefault(a => a.PackageName == appName);

                        // Explicitly handle Copilot and Xbox packages to ensure subpackages are added
                        bool isCopilotOrXbox =
                            appName.Contains("Copilot", StringComparison.OrdinalIgnoreCase)
                            || appName.Contains("Xbox", StringComparison.OrdinalIgnoreCase);

                        if (
                            appInfo?.SubPackages != null
                            && (appInfo.SubPackages.Length > 0 || isCopilotOrXbox)
                        )
                        {
                            appSubPackages[appName] = appInfo.SubPackages ?? new string[0];
                        }

                        // If the app has registry settings but they're not in the appsWithRegistry dictionary,
                        // add them now
                        if (appInfo?.RegistrySettings != null && appInfo.RegistrySettings.Length > 0)
                        {
                            if (!appsWithRegistry.ContainsKey(appName))
                            {
                                appsWithRegistry[appName] = appInfo.RegistrySettings.ToList();
                            }
                        }
                    }
                }

                // Create a new script using the script factory
                return _bloatRemovalScriptFactory.CreateBatchRemovalScript(
                    appNames,
                    appsWithRegistry,
                    appSubPackages
                );
            }
            catch (Exception ex)
            {
                _logService.LogError("Error creating batch removal script", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<string> CreateSingleAppRemovalScriptContentAsync(AppInfo app)
        {
            try
            {
                _logService.LogInformation(
                    $"Creating removal script content for {app.PackageName}"
                );

                // Use the script builder service to create the script content
                string content = _bloatRemovalScriptBuilderService.BuildSingleAppRemovalScript(app);

                _logService.LogSuccess(
                    $"Created removal script content for {app.PackageName}"
                );
                return content;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error creating removal script content for {app.PackageName}", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetSingleAppRemovalScriptContentAsync(AppInfo app)
        {
            try
            {
                _logService.LogInformation(
                    $"Getting removal script content for {app.PackageName}"
                );

                // Use the script builder service to create the script content
                string content = _bloatRemovalScriptBuilderService.BuildSingleAppRemovalScript(app);

                _logService.LogSuccess(
                    $"Retrieved removal script content for {app.PackageName}"
                );
                return content;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error getting removal script content for {app.PackageName}", ex);
                throw;
            }
        }
    }
}

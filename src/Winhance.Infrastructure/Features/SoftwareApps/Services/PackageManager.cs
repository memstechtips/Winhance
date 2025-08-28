using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.UI.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Manages packages and applications on the system.
    /// Acts as a facade for more specialized services.
    /// </summary>
    public class PackageManager : IPackageManager
    {
        private readonly IAppRemovalService _appRemovalService;
        private readonly AppRemovalServiceAdapter _appRemovalServiceAdapter;
        private readonly ICapabilityRemovalService _capabilityRemovalService;
        private readonly IFeatureRemovalService _featureRemovalService;

        /// <inheritdoc/>
        public ILogService LogService { get; }

        /// <inheritdoc/>
        public IAppService AppDiscoveryService { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// This property is maintained for backward compatibility.
        /// It returns an adapter that converts IAppRemovalService to IInstallationService&lt;AppInfo&gt;.
        /// New code should use dependency injection to get IAppRemovalService directly.
        /// </remarks>
        public IInstallationService<AppInfo> AppRemovalService => _appRemovalServiceAdapter;

        /// <inheritdoc/>
        public ISpecialAppHandlerService SpecialAppHandlerService { get; }

        /// <inheritdoc/>
        public IBloatRemovalScriptService BloatRemovalScriptService { get; }

        /// <inheritdoc/>
        public ISystemServices SystemServices { get; }

        /// <inheritdoc/>
        public IWinhanceNotificationService NotificationService { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageManager"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="appDiscoveryService">The app discovery service.</param>
        /// <param name="installationService">The installation service.</param>
        /// <param name="specialAppHandlerService">The special app handler service.</param>
        public PackageManager(
            ILogService logService,
            IAppService appDiscoveryService,
            IAppRemovalService appRemovalService,
            ICapabilityRemovalService capabilityRemovalService,
            IFeatureRemovalService featureRemovalService,
            ISpecialAppHandlerService specialAppHandlerService,
            IBloatRemovalScriptService bloatRemovalScriptService,
            ISystemServices systemServices,
            IWinhanceNotificationService notificationService
        )
        {
            LogService = logService;
            AppDiscoveryService = appDiscoveryService;
            _appRemovalService = appRemovalService;
            _capabilityRemovalService = capabilityRemovalService;
            _featureRemovalService = featureRemovalService;
            SpecialAppHandlerService = specialAppHandlerService;
            BloatRemovalScriptService = bloatRemovalScriptService;
            SystemServices = systemServices;
            NotificationService = notificationService;
            _appRemovalServiceAdapter = new AppRemovalServiceAdapter(appRemovalService);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<AppInfo>> GetInstallableAppsAsync()
        {
            return await AppDiscoveryService.GetInstallableAppsAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<AppInfo>> GetStandardAppsAsync()
        {
            return await AppDiscoveryService.GetStandardAppsAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<CapabilityInfo>> GetCapabilitiesAsync()
        {
            return await AppDiscoveryService.GetCapabilitiesAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<FeatureInfo>> GetOptionalFeaturesAsync()
        {
            return await AppDiscoveryService.GetOptionalFeaturesAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveAppAsync(string packageName, bool isCapability)
        {
            // Get all standard apps to check the app type
            var allRemovableApps = (await AppDiscoveryService.GetStandardAppsAsync()).ToList();
            var appInfo = allRemovableApps.FirstOrDefault(a => a.PackageName == packageName);

            // If not found in standard apps and isCapability is true, create a CapabilityInfo directly
            if (appInfo == null && isCapability)
            {
                LogService.LogInformation(
                    $"App not found in standard apps but isCapability is true. Treating {packageName} as a capability."
                );
                return await _capabilityRemovalService.RemoveCapabilityAsync(
                    new CapabilityInfo { Name = packageName, PackageName = packageName }
                );
            }
            else if (appInfo == null)
            {
                LogService.LogWarning($"App not found: {packageName}");
                return false;
            }

            // First check if this is a special app that requires special handling
            if (
                appInfo.RequiresSpecialHandling && !string.IsNullOrEmpty(appInfo.SpecialHandlerType)
            )
            {
                LogService.LogInformation(
                    $"Using special handler for app: {packageName}, handler type: {appInfo.SpecialHandlerType}"
                );

                bool success = false;
                switch (appInfo.SpecialHandlerType)
                {
                    case "Edge":
                        success = await SpecialAppHandlerService.RemoveEdgeAsync();
                        break;
                    case "OneDrive":
                        success = await SpecialAppHandlerService.RemoveOneDriveAsync();
                        break;
                    case "OneNote":
                        success = await SpecialAppHandlerService.RemoveOneNoteAsync();
                        break;
                    default:
                        success = await SpecialAppHandlerService.RemoveSpecialAppAsync(
                            appInfo.SpecialHandlerType
                        );
                        break;
                }

                if (success)
                {
                    LogService.LogSuccess($"Successfully removed special app: {packageName}");
                }
                else
                {
                    LogService.LogError($"Failed to remove special app: {packageName}");
                }

                return success; // Exit early, don't continue with standard removal process
            }

            // If not a special app, proceed with normal removal based on app type
            bool result = false;
            switch (appInfo.Type)
            {
                case AppType.OptionalFeature:
                    result = await _featureRemovalService.RemoveFeatureAsync(
                        new FeatureInfo { Name = packageName }
                    );
                    break;
                case AppType.Capability:
                    result = await _capabilityRemovalService.RemoveCapabilityAsync(
                        new CapabilityInfo { Name = packageName, PackageName = packageName }
                    );
                    break;

                case AppType.StandardApp:
                default:
                    var appResult = await _appRemovalService.RemoveAppAsync(appInfo);
                    result = appResult.Success && appResult.Result;
                    break;
            }

            // Only create and register BloatRemoval script for non-special apps
            if (!appInfo.RequiresSpecialHandling)
            {
                // Prepare data for the correct CreateBatchRemovalScriptAsync overload
                var appNamesList = new List<string> { packageName };
                var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();

                if (appInfo?.RegistrySettings != null && appInfo.RegistrySettings.Length > 0)
                {
                    appsWithRegistry[packageName] = appInfo.RegistrySettings.ToList();
                }

                try
                {
                    // Call the overload that returns RemovalScript
                    var removalScript =
                        await BloatRemovalScriptService.CreateBatchRemovalScriptAsync(
                            appNamesList,
                            appsWithRegistry
                        );

                    // Save the RemovalScript object
                    await BloatRemovalScriptService.SaveScriptAsync(removalScript);

                    // Register the RemovalScript object
                    await BloatRemovalScriptService.RegisterRemovalTaskAsync(removalScript);
                }
                catch (Exception ex)
                {
                    LogService.LogError(
                        $"Failed to create or register removal script for {packageName}",
                        ex
                    );
                    // Don't change result value, as the app removal itself might have succeeded
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<bool> IsAppInstalledAsync(
            string packageName,
            CancellationToken cancellationToken = default
        )
        {
            var status = await AppDiscoveryService.GetBatchInstallStatusAsync(
                new[] { packageName }
            );
            return status.TryGetValue(packageName, out var isInstalled) && isInstalled;
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveEdgeAsync()
        {
            return await SpecialAppHandlerService.RemoveEdgeAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveOneDriveAsync()
        {
            return await SpecialAppHandlerService.RemoveOneDriveAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveOneNoteAsync()
        {
            return await SpecialAppHandlerService.RemoveOneNoteAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveSpecialAppAsync(string appHandlerType)
        {
            return await SpecialAppHandlerService.RemoveSpecialAppAsync(appHandlerType);
        }

        /// <inheritdoc/>
        public async Task<List<(string Name, bool Success, string? Error)>> RemoveAppsInBatchAsync(
            List<(string PackageName, bool IsCapability, string? SpecialHandlerType)> apps
        )
        {
            var results = new List<(string Name, bool Success, string? Error)>();
            var standardApps = new List<AppInfo>();
            var capabilities = new List<CapabilityInfo>();
            var features = new List<FeatureInfo>();
            var specialHandlers = new Dictionary<string, List<string>>();

            // Get all standard apps to check for optional features
            var allRemovableApps = (await AppDiscoveryService.GetStandardAppsAsync()).ToList();

            // Categorize apps by type
            foreach (var app in apps)
            {
                if (app.SpecialHandlerType != null)
                {
                    if (!specialHandlers.ContainsKey(app.SpecialHandlerType))
                    {
                        specialHandlers[app.SpecialHandlerType] = new List<string>();
                    }
                    specialHandlers[app.SpecialHandlerType].Add(app.PackageName);
                }
                else
                {
                    // Check app type
                    var appInfo = allRemovableApps.FirstOrDefault(a =>
                        a.PackageName.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase)
                    );

                    if (appInfo != null)
                    {
                        switch (appInfo.Type)
                        {
                            case AppType.OptionalFeature:
                                features.Add(new FeatureInfo { Name = app.PackageName });
                                break;
                            case AppType.Capability:
                                capabilities.Add(
                                    new CapabilityInfo
                                    {
                                        Name = app.PackageName,
                                        PackageName = app.PackageName,
                                    }
                                );
                                break;
                            case AppType.StandardApp:
                            default:
                                standardApps.Add(appInfo);
                                break;
                        }
                    }
                    else
                    {
                        // If we couldn't determine the app type from the app info, use the IsCapability flag
                        if (app.IsCapability)
                        {
                            LogService.LogInformation(
                                $"App not found in standard apps but IsCapability is true. Treating {app.PackageName} as a capability."
                            );
                            capabilities.Add(
                                new CapabilityInfo
                                {
                                    Name = app.PackageName,
                                    PackageName = app.PackageName,
                                }
                            );
                        }
                        else
                        {
                            standardApps.Add(new AppInfo { PackageName = app.PackageName });
                        }
                    }
                }
            }

            // Process standard apps
            if (standardApps.Any())
            {
                foreach (var app in standardApps)
                {
                    try
                    {
                        await _appRemovalService.RemoveAppAsync(app); // Pass AppInfo object
                        results.Add((app.PackageName, true, null));
                    }
                    catch (Exception ex)
                    {
                        results.Add((app.PackageName, false, ex.Message));
                    }
                }
            }

            // Process capabilities
            if (capabilities.Any())
            {
                foreach (var capability in capabilities)
                {
                    try
                    {
                        await _capabilityRemovalService.RemoveCapabilityAsync(capability); // Pass CapabilityInfo object
                        results.Add((capability.Name, true, null));
                    }
                    catch (Exception ex)
                    {
                        results.Add((capability.Name, false, ex.Message));
                    }
                }
            }

            // Process optional features
            if (features.Any())
            {
                foreach (var feature in features)
                {
                    try
                    {
                        await _featureRemovalService.RemoveFeatureAsync(feature); // Pass FeatureInfo object
                        results.Add((feature.Name, true, null));
                    }
                    catch (Exception ex)
                    {
                        results.Add((feature.Name, false, ex.Message));
                    }
                }
            }

            // Process special handlers
            foreach (var handler in specialHandlers)
            {
                switch (handler.Key)
                {
                    case "Edge":
                        foreach (var app in handler.Value)
                        {
                            var success = await SpecialAppHandlerService.RemoveEdgeAsync();
                            results.Add((app, success, success ? null : "Failed to remove Edge"));
                        }
                        break;
                    case "OneDrive":
                        foreach (var app in handler.Value)
                        {
                            var success = await SpecialAppHandlerService.RemoveOneDriveAsync();
                            results.Add(
                                (app, success, success ? null : "Failed to remove OneDrive")
                            );
                        }
                        break;
                    case "OneNote":
                        foreach (var app in handler.Value)
                        {
                            var success = await SpecialAppHandlerService.RemoveOneNoteAsync();
                            results.Add(
                                (app, success, success ? null : "Failed to remove OneNote")
                            );
                        }
                        break;
                    default:
                        foreach (var app in handler.Value)
                        {
                            var success = await SpecialAppHandlerService.RemoveSpecialAppAsync(
                                handler.Key
                            );
                            results.Add(
                                (app, success, success ? null : $"Failed to remove {handler.Key}")
                            );
                        }
                        break;
                }
            }

            // Create batch removal script for successful removals (excluding special apps)
            try
            {
                var successfulApps = results.Where(r => r.Success).Select(r => r.Name).ToList();

                // Filter out special apps from the successful apps list
                var nonSpecialSuccessfulAppInfos = allRemovableApps
                    .Where(a =>
                        successfulApps.Contains(a.PackageName)
                        && (
                            !a.RequiresSpecialHandling || string.IsNullOrEmpty(a.SpecialHandlerType)
                        )
                    )
                    .ToList();

                LogService.LogInformation(
                    $"Creating batch removal script for {nonSpecialSuccessfulAppInfos.Count} non-special apps"
                );

                foreach (var app in nonSpecialSuccessfulAppInfos)
                {
                    try
                    {
                        await BloatRemovalScriptService.UpdateBloatRemovalScriptForInstalledAppAsync(
                            app
                        );
                    }
                    catch (Exception ex)
                    {
                        LogService.LogWarning(
                            $"Failed to update removal script for {app.PackageName}: {ex.Message}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Failed to create batch removal script", ex);
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task RegisterRemovalTaskAsync(RemovalScript script)
        {
            // Call the correct overload that takes a RemovalScript object
            await BloatRemovalScriptService.RegisterRemovalTaskAsync(script);
        }
    }
}

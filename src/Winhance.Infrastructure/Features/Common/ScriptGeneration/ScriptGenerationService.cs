using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.Common.ScriptGeneration
{
    /// <summary>
    /// Service for generating and managing removal scripts.
    /// </summary>
    public class ScriptGenerationService : IScriptGenerationService
    {
        private readonly ILogService _logService;
        private readonly IAppDiscoveryService _appDiscoveryService;
        private readonly IAppRemovalService _appRemovalService;
        private readonly IScriptContentModifier _scriptContentModifier;
        private readonly IScriptUpdateService _scriptUpdateService;
        private readonly IScheduledTaskService _scheduledTaskService;
        private readonly IScriptFactory _scriptFactory;
        private readonly IScriptBuilderService _scriptBuilderService;
        private readonly string _scriptsPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptGenerationService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="appDiscoveryService">The app discovery service.</param>
        /// <param name="appRemovalService">The app removal service.</param>
        /// <param name="scriptContentModifier">The script content modifier.</param>
        /// <param name="scriptUpdateService">The script update service.</param>
        /// <param name="scheduledTaskService">The scheduled task service.</param>
        /// <param name="scriptFactory">The script factory.</param>
        /// <param name="scriptBuilderService">The script builder service.</param>
        public ScriptGenerationService(
            ILogService logService,
            IAppDiscoveryService appDiscoveryService,
            IAppRemovalService appRemovalService,
            IScriptContentModifier scriptContentModifier,
            IScriptUpdateService scriptUpdateService,
            IScheduledTaskService scheduledTaskService,
            IScriptFactory scriptFactory,
            IScriptBuilderService scriptBuilderService
        )
        {
            _logService = logService;
            _appDiscoveryService = appDiscoveryService;
            _appRemovalService = appRemovalService;
            _scriptContentModifier = scriptContentModifier;
            _scriptUpdateService = scriptUpdateService;
            _scheduledTaskService = scheduledTaskService;
            _scriptFactory = scriptFactory;
            _scriptBuilderService = scriptBuilderService;

            _scriptsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Winhance",
                "Scripts"
            );
            Directory.CreateDirectory(_scriptsPath);
        }

        /// <inheritdoc/>
        public async Task<RemovalScript> CreateBatchRemovalScriptAsync(
            List<string> appNames,
            Dictionary<string, List<AppRegistrySetting>> appsWithRegistry
        )
        {
            try
            {
                _logService.LogInformation(
                    $"Creating batch removal script for {appNames.Count} apps"
                );

                // Get all standard apps to check for subpackages
                var allRemovableApps = (await _appDiscoveryService.GetStandardAppsAsync()).ToList();

                // Create a dictionary to store subpackages for each app
                var appSubPackages = new Dictionary<string, string[]>();

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

                // Check if the BloatRemoval.ps1 file already exists
                string bloatRemovalScriptPath = Path.Combine(_scriptsPath, "BloatRemoval.ps1");
                if (File.Exists(bloatRemovalScriptPath))
                {
                    _logService.LogInformation(
                        "BloatRemoval.ps1 already exists, updating it with new entries"
                    );
                    return await _scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                        appNames,
                        appsWithRegistry,
                        appSubPackages,
                        false // false = removal operation, so add to script
                    );
                }

                // If the file doesn't exist, create a new one using the script factory
                return _scriptFactory.CreateBatchRemovalScript(
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
        public async Task<bool> CreateBatchRemovalScriptAsync(string scriptPath, AppInfo app)
        {
            try
            {
                _logService.LogInformation(
                    $"Creating removal script for {app.PackageName} at {scriptPath}"
                );

                // Use the script builder service to create the script content
                string content = _scriptBuilderService.BuildSingleAppRemovalScript(app);

                await File.WriteAllTextAsync(scriptPath, content);
                _logService.LogSuccess(
                    $"Created removal script for {app.PackageName} at {scriptPath}"
                );
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error creating removal script for {app.PackageName}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task RegisterRemovalTaskAsync(RemovalScript script)
        {
            try
            {
                if (script == null)
                {
                    _logService.LogError("Cannot register removal task: Script is null");
                    return;
                }

                _logService.LogInformation($"Registering removal task for script: {script.Name}");

                // Ensure the script has been saved
                if (string.IsNullOrEmpty(script.Content))
                {
                    _logService.LogWarning($"Script content is empty for: {script.Name}");
                }

                // Register the scheduled task
                bool success = await _scheduledTaskService.RegisterScheduledTaskAsync(script);

                if (success)
                {
                    _logService.LogSuccess(
                        $"Successfully registered scheduled task for script: {script.Name}"
                    );
                }
                else
                {
                    _logService.LogWarning(
                        $"Failed to register scheduled task for script: {script.Name}, but continuing operation"
                    );
                    // Don't throw an exception here, just log a warning and continue
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(
                    $"Error registering removal task for script: {script.Name}",
                    ex
                );
                // Don't rethrow the exception, just log it and continue
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RegisterRemovalTaskAsync(string taskName, string scriptPath)
        {
            try
            {
                if (string.IsNullOrEmpty(taskName) || string.IsNullOrEmpty(scriptPath))
                {
                    _logService.LogError(
                        $"Invalid parameters for task registration. TaskName: {taskName}, ScriptPath: {scriptPath}"
                    );
                    return false;
                }

                _logService.LogInformation(
                    $"Registering removal task: {taskName} for script: {scriptPath}"
                );

                // Check if the script file exists
                if (!File.Exists(scriptPath))
                {
                    _logService.LogError($"Script file not found at: {scriptPath}");
                    return false;
                }

                // Create a RemovalScript object to pass to the scheduled task service
                var script = new RemovalScript
                {
                    Name = Path.GetFileNameWithoutExtension(scriptPath),
                    Content = await File.ReadAllTextAsync(scriptPath),
                    TargetScheduledTaskName = taskName,
                    RunOnStartup = true,
                };

                // Register the scheduled task
                return await _scheduledTaskService.RegisterScheduledTaskAsync(script);
            }
            catch (Exception ex)
            {
                _logService.LogError(
                    $"Error registering removal task: {taskName} for script: {scriptPath}",
                    ex
                );
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task SaveScriptAsync(RemovalScript script)
        {
            try
            {
                string scriptPath = Path.Combine(_scriptsPath, $"{script.Name}.ps1");
                await File.WriteAllTextAsync(scriptPath, script.Content);
                _logService.LogInformation($"Saved script to {scriptPath}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving script: {script.Name}", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SaveScriptAsync(string scriptPath, string scriptContent)
        {
            try
            {
                await File.WriteAllTextAsync(scriptPath, scriptContent);
                _logService.LogInformation($"Saved script to {scriptPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving script to {scriptPath}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateBloatRemovalScriptForInstalledAppAsync(AppInfo app)
        {
            try
            {
                _logService.LogInformation(
                    $"Updating BloatRemoval script for installed app: {app.PackageName}"
                );

                string bloatRemovalScriptPath = Path.Combine(_scriptsPath, "BloatRemoval.ps1");
                if (!File.Exists(bloatRemovalScriptPath))
                {
                    _logService.LogWarning(
                        $"BloatRemoval.ps1 not found at {bloatRemovalScriptPath}"
                    );
                    return false;
                }

                string scriptContent = await File.ReadAllTextAsync(bloatRemovalScriptPath);
                bool scriptModified = false;

                // Handle different types of apps
                if (
                    app.Type == AppType.OptionalFeature
                    || app.PackageName.Equals("Recall", StringComparison.OrdinalIgnoreCase)
                    || app.Type == AppType.Capability
                )
                {
                    // Handle OptionalFeatures and Capabilities using the ScriptUpdateService
                    // This ensures proper handling of install operations (removing from script)
                    _logService.LogInformation(
                        $"Using ScriptUpdateService to update BloatRemoval script for {app.Type} {app.PackageName}"
                    );

                    // Create a list with just this app
                    var appNames = new List<string> { app.PackageName };
                    var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();
                    var appSubPackages = new Dictionary<string, string[]>();

                    // Get app registry settings if available
                    var allRemovableApps = (
                        await _appDiscoveryService.GetStandardAppsAsync()
                    ).ToList();
                    var appDefinition = allRemovableApps.FirstOrDefault(a =>
                        a.PackageName.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase)
                    );

                    if (
                        appDefinition?.RegistrySettings != null
                        && appDefinition.RegistrySettings.Length > 0
                    )
                    {
                        appsWithRegistry[app.PackageName] = appDefinition.RegistrySettings.ToList();
                    }

                    // Update the script with isInstallOperation = true to remove the entry
                    await _scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                        appNames,
                        appsWithRegistry,
                        appSubPackages,
                        true // true = install operation, so remove from script
                    );

                    return true;
                }
                else
                {
                    // Handle standard package
                    scriptContent = _scriptContentModifier.RemovePackageFromScript(
                        scriptContent,
                        app.PackageName
                    );

                    // Create a list of subpackages to remove
                    List<string> subPackagesToRemove = new List<string>();

                    // Get all standard apps to find the app definition and its subpackages
                    var allRemovableApps = (
                        await _appDiscoveryService.GetStandardAppsAsync()
                    ).ToList();

                    // Find the app definition that matches the current app
                    var appDefinition = allRemovableApps.FirstOrDefault(a =>
                        a.PackageName.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase)
                    );

                    // If we found the app definition and it has subpackages, add them to the removal list
                    if (appDefinition?.SubPackages != null && appDefinition.SubPackages.Length > 0)
                    {
                        _logService.LogInformation(
                            $"Found {appDefinition.SubPackages.Length} subpackages for {app.PackageName} in WindowsAppCatalog"
                        );
                        subPackagesToRemove.AddRange(appDefinition.SubPackages);
                    }

                    // Remove registry settings for this app from the script
                    scriptContent = _scriptContentModifier.RemoveAppRegistrySettingsFromScript(
                        scriptContent,
                        app.PackageName
                    );

                    // Apply registry settings to delete registry keys from the system
                    if (
                        appDefinition?.RegistrySettings != null
                        && appDefinition.RegistrySettings.Length > 0
                    )
                    {
                        _logService.LogInformation(
                            $"Found {appDefinition.RegistrySettings.Length} registry settings for {app.PackageName}"
                        );

                        // Create a list of registry settings that delete the keys
                        var deleteRegistrySettings = new List<AppRegistrySetting>();

                        foreach (var setting in appDefinition.RegistrySettings)
                        {
                            // Create a new registry setting that deletes the key
                            var deleteSetting = new AppRegistrySetting
                            {
                                Path = setting.Path,
                                Name = setting.Name,
                                Value = null, // null value means delete the key
                                ValueKind = setting.ValueKind,
                            };

                            deleteRegistrySettings.Add(deleteSetting);
                        }

                        // Apply the registry settings to delete the keys
                        // TODO: ApplyRegistrySettingsAsync is not on IAppRemovalService.
                        // Need to inject IRegistryService or move this logic. Commenting out for now.
                        _logService.LogInformation(
                            $"Applying {deleteRegistrySettings.Count} registry settings to delete keys for {app.PackageName}"
                        );
                        // var success = await _appRemovalService.ApplyRegistrySettingsAsync(
                        //     deleteRegistrySettings
                        // );
                        var success = false; // Assume failure for now as the call is removed
                        _logService.LogWarning(
                            $"Skipping registry key deletion for {app.PackageName} as ApplyRegistrySettingsAsync is not available on the interface."
                        );

                        if (success) // This block will likely not be hit now
                        {
                            _logService.LogSuccess(
                                $"Successfully deleted registry keys for {app.PackageName}"
                            );
                        }
                        else
                        {
                            _logService.LogWarning(
                                $"Failed to delete some registry keys for {app.PackageName}"
                            );
                        }
                    }

                    // Remove all subpackages from the script
                    foreach (var subPackage in subPackagesToRemove)
                    {
                        _logService.LogInformation(
                            $"Removing subpackage: {subPackage} for app: {app.PackageName}"
                        );
                        scriptContent = _scriptContentModifier.RemovePackageFromScript(
                            scriptContent,
                            subPackage
                        );
                    }

                    scriptModified = true;
                }

                // Save the updated script if it was modified
                if (scriptModified)
                {
                    await File.WriteAllTextAsync(bloatRemovalScriptPath, scriptContent);
                    _logService.LogSuccess(
                        $"Successfully updated BloatRemoval script for app: {app.PackageName}"
                    );
                }
                else
                {
                    _logService.LogInformation(
                        $"No changes needed to BloatRemoval script for app: {app.PackageName}"
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError(
                    $"Error updating BloatRemoval script for app: {app.PackageName}",
                    ex
                );
                return false;
            }
        }
    }
}

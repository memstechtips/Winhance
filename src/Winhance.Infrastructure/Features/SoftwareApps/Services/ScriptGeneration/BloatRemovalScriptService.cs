using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Implementation of IBloatRemovalScriptService that manages and executes the BloatRemoval script.
    /// </summary>
    public class BloatRemovalScriptService : IBloatRemovalScriptService
    {
        private readonly ILogService _logService;
        private readonly IAppDiscoveryService _appDiscoveryService;

        // Removed IAppRemovalService dependency to break circular dependency
        private readonly IScriptUpdateService _scriptUpdateService;
        private readonly IScheduledTaskService _scheduledTaskService;
        private readonly ISystemServices _systemServices;
        private readonly IBloatRemovalScriptContentModifier _bloatRemovalScriptContentModifier;
        private readonly IBloatRemovalScriptGenerationService _bloatRemovalScriptGenerationService;
        private readonly IBloatRemovalScriptSavingService _bloatRemovalScriptSavingService;
        private readonly string _scriptsPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="BloatRemovalScriptService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="appDiscoveryService">The app discovery service.</param>
        // Removed appRemovalService parameter to break circular dependency
        /// <param name="scriptUpdateService">The script update service.</param>
        /// <param name="scheduledTaskService">The scheduled task service.</param>
        /// <param name="systemServices">The system services.</param>
        /// <param name="bloatRemovalScriptContentModifier">The bloat removal script content modifier.</param>
        /// <param name="bloatRemovalScriptGenerationService">The bloat removal script generation service.</param>
        /// <param name="bloatRemovalScriptSavingService">The bloat removal script saving service.</param>
        public BloatRemovalScriptService(
            ILogService logService,
            IAppDiscoveryService appDiscoveryService,
            // Removed appRemovalService parameter to break circular dependency
            IScriptUpdateService scriptUpdateService,
            IScheduledTaskService scheduledTaskService,
            ISystemServices systemServices,
            IBloatRemovalScriptContentModifier bloatRemovalScriptContentModifier,
            IBloatRemovalScriptGenerationService bloatRemovalScriptGenerationService,
            IBloatRemovalScriptSavingService bloatRemovalScriptSavingService
        )
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _appDiscoveryService =
                appDiscoveryService ?? throw new ArgumentNullException(nameof(appDiscoveryService));
            // Removed _appRemovalService assignment to break circular dependency
            _scriptUpdateService =
                scriptUpdateService ?? throw new ArgumentNullException(nameof(scriptUpdateService));
            _scheduledTaskService =
                scheduledTaskService
                ?? throw new ArgumentNullException(nameof(scheduledTaskService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _bloatRemovalScriptContentModifier =
                bloatRemovalScriptContentModifier
                ?? throw new ArgumentNullException(nameof(bloatRemovalScriptContentModifier));
            _bloatRemovalScriptGenerationService =
                bloatRemovalScriptGenerationService
                ?? throw new ArgumentNullException(nameof(bloatRemovalScriptGenerationService));
            _bloatRemovalScriptSavingService =
                bloatRemovalScriptSavingService
                ?? throw new ArgumentNullException(nameof(bloatRemovalScriptSavingService));

            _scriptsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Winhance",
                "Scripts"
            );
            Directory.CreateDirectory(_scriptsPath);
        }

        /// <inheritdoc/>
        public async Task<RemovalScript> AddAppsToScriptAsync(
            List<AppInfo> appInfos,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (appInfos == null || !appInfos.Any())
            {
                _logService.LogWarning("No apps provided to add to the BloatRemoval script");
                return await GetCurrentScriptAsync();
            }

            try
            {
                string itemType = "apps";
                var appNames = appInfos.Select(a => a.PackageName).ToList();

                // Extract registry settings
                var appsWithRegistry = ExtractRegistrySettings(appInfos);

                // Extract subpackages
                var appSubPackages = ExtractSubPackages(appInfos);

                return await UpdateBloatRemovalScriptAsync(
                    appNames,
                    appsWithRegistry,
                    appSubPackages,
                    itemType,
                    progress,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding apps to BloatRemoval script: {ex.Message}", ex);
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = $"Error adding apps to BloatRemoval script: {ex.Message}",
                        LogLevel = LogLevel.Error,
                    }
                );
                throw;
            }
        }

        /// <summary>
        /// Extracts registry settings from a list of app information.
        /// </summary>
        /// <param name="appInfos">The list of app information.</param>
        /// <returns>A dictionary mapping app names to registry settings.</returns>
        private Dictionary<string, List<AppRegistrySetting>> ExtractRegistrySettings(
            List<AppInfo> appInfos
        )
        {
            var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();
            foreach (
                var app in appInfos.Where(a =>
                    a.RegistrySettings != null && a.RegistrySettings.Length > 0
                )
            )
            {
                appsWithRegistry[app.PackageName] = app.RegistrySettings.ToList();
            }
            return appsWithRegistry;
        }

        /// <summary>
        /// Extracts subpackages from a list of app information.
        /// </summary>
        /// <param name="appInfos">The list of app information.</param>
        /// <returns>A dictionary mapping app names to subpackages.</returns>
        private Dictionary<string, string[]> ExtractSubPackages(List<AppInfo> appInfos)
        {
            var appSubPackages = new Dictionary<string, string[]>();
            foreach (
                var app in appInfos.Where(a => a.SubPackages != null && a.SubPackages.Length > 0)
            )
            {
                appSubPackages[app.PackageName] = app.SubPackages;
            }
            return appSubPackages;
        }

        /// <inheritdoc/>
        public async Task<RemovalScript> AddCapabilitiesToScriptAsync(
            List<CapabilityInfo> capabilities,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (capabilities == null || !capabilities.Any())
            {
                _logService.LogWarning(
                    "No capabilities provided to add to the BloatRemoval script"
                );
                return await GetCurrentScriptAsync();
            }

            try
            {
                string itemType = "capabilities";
                var packageNames = capabilities.Select(c => c.PackageName).ToList();

                return await UpdateBloatRemovalScriptAsync(
                    packageNames,
                    new Dictionary<string, List<AppRegistrySetting>>(),
                    new Dictionary<string, string[]>(),
                    itemType,
                    progress,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logService.LogError(
                    $"Error adding capabilities to BloatRemoval script: {ex.Message}",
                    ex
                );
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText =
                            $"Error adding capabilities to BloatRemoval script: {ex.Message}",
                        LogLevel = LogLevel.Error,
                    }
                );
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<RemovalScript> AddFeaturesToScriptAsync(
            List<FeatureInfo> features,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (features == null || !features.Any())
            {
                _logService.LogWarning("No features provided to add to the BloatRemoval script");
                return await GetCurrentScriptAsync();
            }

            try
            {
                string itemType = "features";
                var packageNames = features.Select(f => f.PackageName).ToList();

                return await UpdateBloatRemovalScriptAsync(
                    packageNames,
                    new Dictionary<string, List<AppRegistrySetting>>(),
                    new Dictionary<string, string[]>(),
                    itemType,
                    progress,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logService.LogError(
                    $"Error adding features to BloatRemoval script: {ex.Message}",
                    ex
                );
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = $"Error adding features to BloatRemoval script: {ex.Message}",
                        LogLevel = LogLevel.Error,
                    }
                );
                throw;
            }
        }

        /// <summary>
        /// Updates the BloatRemoval script with the specified items.
        /// </summary>
        /// <param name="packageNames">The package names to add to the script.</param>
        /// <param name="registrySettings">The registry settings to add to the script.</param>
        /// <param name="subPackages">The subpackages to add to the script.</param>
        /// <param name="itemType">The type of items being added (apps, capabilities, features).</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the updated script.</returns>
        private async Task<RemovalScript> UpdateBloatRemovalScriptAsync(
            List<string> packageNames,
            Dictionary<string, List<AppRegistrySetting>> registrySettings,
            Dictionary<string, string[]> subPackages,
            string itemType,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            _logService.LogInformation(
                $"Adding {packageNames.Count} {itemType} to BloatRemoval script"
            );
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText =
                        $"Adding {packageNames.Count} {itemType} to BloatRemoval script...",
                }
            );

            // Update the script
            var script = await _scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                packageNames,
                registrySettings,
                subPackages,
                false
            ); // false = removal operation

            // Register the scheduled task
            await RegisterBloatRemovalTaskAsync(script);

            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText =
                        $"Successfully added {packageNames.Count} {itemType} to BloatRemoval script",
                    LogLevel = LogLevel.Success,
                }
            );

            return script;
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> ExecuteScriptAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                _logService.LogInformation("Executing BloatRemoval script");
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = "Executing BloatRemoval script...",
                    }
                );

                // Get the script path
                string scriptPath = GetBloatRemovalScriptPath();

                if (!File.Exists(scriptPath))
                {
                    _logService.LogError($"BloatRemoval script not found at: {scriptPath}");
                    return OperationResult<bool>.Failed(
                        $"BloatRemoval script not found at: {scriptPath}"
                    );
                }

                // Execute the script with elevated privileges
                var result = await ExecutePowerShellScriptWithElevatedPrivilegesAsync(
                    scriptPath,
                    progress,
                    cancellationToken
                );

                return result;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error executing BloatRemoval script: {ex.Message}", ex);
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = $"Error executing BloatRemoval script: {ex.Message}",
                        LogLevel = LogLevel.Error,
                    }
                );
                return OperationResult<bool>.Failed(
                    $"Error executing BloatRemoval script: {ex.Message}",
                    ex
                );
            }
        }

        /// <summary>
        /// Executes a PowerShell script with elevated privileges.
        /// </summary>
        /// <param name="scriptPath">The path to the script to execute.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the result of the execution.</returns>
        private async Task<
            OperationResult<bool>
        > ExecutePowerShellScriptWithElevatedPrivilegesAsync(
            string scriptPath,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            // Create temporary files to capture output and errors
            string outputFile = Path.Combine(
                Path.GetTempPath(),
                $"BloatRemoval_Output_{Guid.NewGuid()}.txt"
            );
            string errorFile = Path.Combine(
                Path.GetTempPath(),
                $"BloatRemoval_Error_{Guid.NewGuid()}.txt"
            );

            // Create a wrapper script that captures output
            string wrapperScriptPath = Path.Combine(
                Path.GetTempPath(),
                $"BloatRemoval_Wrapper_{Guid.NewGuid()}.ps1"
            );
            string wrapperContent =
                $@"
            try {{
                # Execute the BloatRemoval script and capture output
                & '{scriptPath}' *> '{outputFile}' 2> '{errorFile}'
                
                # Write exit code to indicate success
                exit 0
            }} catch {{
                # Write error to error file
                $_ | Out-File -FilePath '{errorFile}' -Append
                exit 1
            }}
            ";

            File.WriteAllText(wrapperScriptPath, wrapperContent);

            try
            {
                // Execute the script with elevated privileges using Process.Start
                _logService.LogInformation(
                    $"Executing PowerShell script via Process.Start: {scriptPath}"
                );

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{wrapperScriptPath}\"",
                    UseShellExecute = true, // Must be true to use Verb
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    Verb = "runas", // Request admin privileges
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);

                if (process == null)
                {
                    _logService.LogError("Failed to start PowerShell process for script execution");
                    return OperationResult<bool>.Failed("Failed to start PowerShell process");
                }

                // Wait for the process to complete
                await process.WaitForExitAsync(cancellationToken);

                // Read output and error files
                string output = File.Exists(outputFile)
                    ? await File.ReadAllTextAsync(outputFile, cancellationToken)
                    : "";
                string error = File.Exists(errorFile)
                    ? await File.ReadAllTextAsync(errorFile, cancellationToken)
                    : "";

                // Log output
                if (!string.IsNullOrWhiteSpace(output))
                {
                    foreach (var line in output.Split(Environment.NewLine))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            _logService.LogInformation($"Script output: {line}");
                        }
                    }
                }

                // Check for errors
                bool hasErrors = !string.IsNullOrWhiteSpace(error) || process.ExitCode != 0;
                if (hasErrors)
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        foreach (var line in error.Split(Environment.NewLine))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                _logService.LogError($"Script error: {line}");
                            }
                        }
                    }

                    _logService.LogError("Script execution failed");
                    progress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = 100,
                            StatusText = "Script execution failed",
                            LogLevel = LogLevel.Error,
                        }
                    );
                    return OperationResult<bool>.Failed("Script execution failed");
                }

                _logService.LogSuccess("Script executed successfully");
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 100,
                        StatusText = "Script executed successfully",
                        LogLevel = LogLevel.Success,
                    }
                );
                return OperationResult<bool>.Succeeded(true);
            }
            finally
            {
                // Clean up temporary files
                try
                {
                    if (File.Exists(wrapperScriptPath))
                        File.Delete(wrapperScriptPath);
                    if (File.Exists(outputFile))
                        File.Delete(outputFile);
                    if (File.Exists(errorFile))
                        File.Delete(errorFile);
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Error cleaning up temporary files: {ex.Message}");
                    // Continue execution, don't fail because of cleanup issues
                }
            }
        }

        /// <inheritdoc/>
        public async Task<RemovalScript> GetCurrentScriptAsync()
        {
            try
            {
                string bloatRemovalScriptPath = Path.Combine(_scriptsPath, "BloatRemoval.ps1");
                if (!File.Exists(bloatRemovalScriptPath))
                {
                    _logService.LogWarning(
                        $"BloatRemoval.ps1 not found at {bloatRemovalScriptPath}"
                    );

                    // Create a basic script using ScriptUpdateService
                    var script = await _scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                        new List<string>(),
                        new Dictionary<string, List<AppRegistrySetting>>(),
                        new Dictionary<string, string[]>(),
                        false
                    );

                    return script;
                }

                // Read the script content directly
                var scriptContent = await File.ReadAllTextAsync(bloatRemovalScriptPath);

                // Create a RemovalScript object with the content
                var removalScript = new RemovalScript
                {
                    Name = "BloatRemoval",
                    Content = scriptContent,
                    // ScriptPath is a calculated property, no need to set it
                    TargetScheduledTaskName = "Winhance\\BloatRemoval",
                    RunOnStartup = true,
                };

                return removalScript;
            }
            catch (Exception ex)
            {
                _logService.LogError(
                    $"Error getting current BloatRemoval script: {ex.Message}",
                    ex
                );
                throw;
            }
        }

        /// <summary>
        /// Gets the path to the BloatRemoval script.
        /// </summary>
        /// <returns>The path to the BloatRemoval script.</returns>
        private string GetBloatRemovalScriptPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Winhance",
                "Scripts",
                "BloatRemoval.ps1"
            );
        }

        /// <summary>
        /// Registers a scheduled task to run the BloatRemoval script at startup.
        /// </summary>
        /// <param name="script">The removal script to register.</param>
        /// <returns>True if the task was registered successfully, false otherwise.</returns>
        private async Task<bool> RegisterBloatRemovalTaskAsync(RemovalScript script)
        {
            try
            {
                if (script == null)
                {
                    _logService.LogError("Cannot register scheduled task: Script is null");
                    return false;
                }

                _logService.LogInformation("Registering scheduled task for BloatRemoval.ps1");

                // Register the task directly using the scheduled task service
                bool success = await _scheduledTaskService.RegisterScheduledTaskAsync(script);

                if (success)
                {
                    _logService.LogSuccess(
                        "Successfully registered scheduled task for BloatRemoval.ps1"
                    );
                }
                else
                {
                    _logService.LogWarning(
                        "Failed to register scheduled task for BloatRemoval.ps1"
                    );
                }

                return success;
            }
            catch (Exception ex)
            {
                _logService.LogError(
                    $"Error registering scheduled task for BloatRemoval.ps1: {ex.Message}",
                    ex
                );
                return false;
            }
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

                // If the file doesn't exist, create a new one using the script generation service
                return await _bloatRemovalScriptGenerationService.CreateBatchRemovalScriptAsync(
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

                // Use the script generation service to create the script content
                string content = await _bloatRemovalScriptGenerationService.GetSingleAppRemovalScriptContentAsync(app);

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
                // Delegate to the BloatRemovalScriptSavingService
                await _bloatRemovalScriptSavingService.SaveScriptAsync(script);
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
                // Delegate to the BloatRemovalScriptSavingService
                return await _bloatRemovalScriptSavingService.SaveScriptAsync(
                    scriptPath,
                    scriptContent
                );
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving script to {scriptPath}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetScriptContentAsync(string scriptPath)
        {
            try
            {
                // Delegate to the BloatRemovalScriptSavingService
                return await _bloatRemovalScriptSavingService.GetScriptContentAsync(scriptPath);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error getting script content from {scriptPath}", ex);
                throw;
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
                
                // Use the saving service to check if the file exists and get its content
                string scriptContent = await _bloatRemovalScriptSavingService.GetScriptContentAsync(bloatRemovalScriptPath);
                
                if (scriptContent == null)
                {
                    _logService.LogWarning(
                        $"BloatRemoval.ps1 not found at {bloatRemovalScriptPath}"
                    );
                    return false;
                }
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
                    // Handle standard package by delegating to the script content modifier
                    // This is still needed as the script generation service doesn't handle script modification
                    scriptContent = _bloatRemovalScriptContentModifier.RemovePackageFromScript(
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
                    scriptContent =
                        _bloatRemovalScriptContentModifier.RemoveAppRegistrySettingsFromScript(
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
                        scriptContent = _bloatRemovalScriptContentModifier.RemovePackageFromScript(
                            scriptContent,
                            subPackage
                        );
                    }

                    scriptModified = true;
                }

                // Save the updated script if it was modified
                if (scriptModified)
                {
                    // Save the modified script back to disk using the saving service
                    await _bloatRemovalScriptSavingService.SaveScriptAsync(bloatRemovalScriptPath, scriptContent);
                    _logService.LogSuccess(
                        $"Updated BloatRemoval script for installed app: {app.PackageName}"
                    );
                    return true;
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

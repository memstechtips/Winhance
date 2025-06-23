using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service that enables Windows optional features.
/// </summary>
public class FeatureInstallationService : BaseInstallationService<FeatureInfo>, IFeatureInstallationService
{
    private readonly FeatureCatalog _featureCatalog;
    private readonly IScriptUpdateService _scriptUpdateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureInstallationService"/> class.
    /// </summary>
    /// <param name="logService">The log service.</param>
    /// <param name="powerShellService">The PowerShell execution service.</param>
    /// <param name="scriptUpdateService">The script update service.</param>
    public FeatureInstallationService(
        ILogService logService,
        IPowerShellExecutionService powerShellService,
        IScriptUpdateService scriptUpdateService)
        : base(logService, powerShellService)
    {
        // Create a default feature catalog
        _featureCatalog = FeatureCatalog.CreateDefault();
        _scriptUpdateService = scriptUpdateService ?? throw new ArgumentNullException(nameof(scriptUpdateService));
    }

    /// <inheritdoc/>
    public Task<OperationResult<bool>> InstallFeatureAsync(
        FeatureInfo featureInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return InstallItemAsync(featureInfo, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<OperationResult<bool>> CanInstallFeatureAsync(FeatureInfo featureInfo)
    {
        return CanInstallItemAsync(featureInfo);
    }

    /// <inheritdoc/>
    protected override async Task<OperationResult<bool>> PerformInstallationAsync(
        FeatureInfo featureInfo,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var result = await InstallFeatureAsync(featureInfo.PackageName, progress, cancellationToken);
        
        // Only update BloatRemoval.ps1 script if installation was successful
        if (result.Success)
        {
            try
            {
                _logService.LogInformation($"Starting BloatRemoval.ps1 script update for {featureInfo.Name}");
                
                // Update the BloatRemoval.ps1 script to remove the installed feature from the removal list
                var appNames = new List<string> { featureInfo.PackageName };
                _logService.LogInformation($"Removing feature name from BloatRemoval.ps1: {featureInfo.PackageName}");
                
                var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();
                var appSubPackages = new Dictionary<string, string[]>();

                // Add registry settings if present
                if (featureInfo.RegistrySettings != null && featureInfo.RegistrySettings.Length > 0)
                {
                    _logService.LogInformation($"Adding {featureInfo.RegistrySettings.Length} registry settings for {featureInfo.Name}");
                    appsWithRegistry.Add(featureInfo.PackageName, new List<AppRegistrySetting>(featureInfo.RegistrySettings));
                }

                _logService.LogInformation($"Updating BloatRemoval.ps1 to remove {featureInfo.Name} from removal list");
                
                // Make sure we're explicitly identifying this as an optional feature
                _logService.LogInformation($"Ensuring {featureInfo.Name} is identified as an optional feature");
                
                var scriptResult = await _scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                    appNames,
                    appsWithRegistry,
                    appSubPackages,
                    true); // true = install operation, so remove from script
                
                // Register the scheduled task to ensure it's updated with the latest script content
                if (scriptResult != null)
                {
                    try
                    {
                        var scheduledTaskService = new ScheduledTaskService(_logService);
                        bool taskRegistered = await scheduledTaskService.RegisterScheduledTaskAsync(scriptResult);
                        
                        if (taskRegistered)
                        {
                            _logService.LogInformation("Successfully registered updated BloatRemoval scheduled task");
                        }
                        else
                        {
                            _logService.LogWarning("Failed to register updated BloatRemoval scheduled task");
                        }
                    }
                    catch (Exception taskEx)
                    {
                        _logService.LogError($"Error registering BloatRemoval scheduled task: {taskEx.Message}", taskEx);
                        // Don't fail the installation if task registration fails
                    }
                }
                
                _logService.LogInformation($"Successfully updated BloatRemoval.ps1 script - {featureInfo.Name} will no longer be removed");
                _logService.LogInformation($"Script update result: {scriptResult?.Name ?? "null"}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error updating BloatRemoval.ps1 script for {featureInfo.Name}", ex);
                _logService.LogError($"Exception details: {ex.Message}");
                _logService.LogError($"Stack trace: {ex.StackTrace}");
                // Don't fail the installation if script update fails
            }
        }
        else
        {
            _logService.LogInformation($"Skipping BloatRemoval.ps1 update because installation of {featureInfo.Name} was not successful");
        }
        
        return result;
    }


    /// <summary>
    /// Installs a Windows optional feature by name.
    /// </summary>
    /// <param name="featureName">The name of the feature to install.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An operation result indicating success or failure with error details.</returns>
    private async Task<OperationResult<bool>> InstallFeatureAsync(
        string featureName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the friendly name of the feature from the catalog
            string friendlyName = GetFriendlyName(featureName);
            
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = $"Enabling {friendlyName}...",
                DetailedMessage = $"Starting to enable optional feature: {featureName}"
            });

            _logService.LogInformation($"Attempting to enable optional feature: {featureName}");
            
            // Create a progress handler that overrides the generic "Operation: Running" text
            var progressHandler = new Progress<TaskProgressDetail>(detail => {
                // If we get a generic "Operation: Running" status, replace it with our more descriptive one
                if (detail.StatusText != null && detail.StatusText.StartsWith("Operation:"))
                {
                    // Keep the percentage but replace the generic text with the friendly name
                    detail.StatusText = $"Enabling {friendlyName}...";
                    if (detail.Progress.HasValue)
                    {
                        detail.StatusText = $"Enabling {friendlyName}... ({detail.Progress:F0}%)";
                    }
                }
                
                // Forward the updated progress to the original progress reporter
                progress?.Report(detail);
            });

            // Define the PowerShell script - Embed featureName, output parseable string
            // Output format: STATUS|Message|RebootRequired (e.g., SUCCESS|Feature enabled|True)
            string script = $@"
                try {{
                    $featureName = '{featureName}' # Embed featureName directly
                    Write-Information ""Checking feature status: $featureName""
                    # Progress reporting needs to be handled by the caller based on script output or duration

                    # Check if the feature exists
                    $feature = Get-WindowsOptionalFeature -Online -FeatureName $featureName -ErrorAction SilentlyContinue

                    if (-not $feature) {{
                        Write-Warning ""Feature not found: $featureName""
                        return ""FAILURE|Feature not found: $featureName""
                    }}

                    # Check if the feature is already enabled
                    if ($feature.State -eq 'Enabled') {{
                        Write-Information ""Feature is already enabled: $featureName""
                        return ""SUCCESS|Feature is already enabled|False""
                    }}

                    Write-Information ""Enabling feature: $featureName""

                    # Enable the feature
                    $result = Enable-WindowsOptionalFeature -Online -FeatureName $featureName -NoRestart

                    # Check if the feature was enabled successfully
                    $feature = Get-WindowsOptionalFeature -Online -FeatureName $featureName

                    if ($feature.State -eq 'Enabled') {{
                        $rebootNeeded = if ($result.RestartNeeded) {{ 'True' }} else {{ 'False' }}
                        return ""SUCCESS|Feature enabled successfully|$rebootNeeded""
                    }} else {{
                        Write-Warning ""Failed to enable feature: $featureName""
                        return ""FAILURE|Failed to enable feature""
                    }}
                }}
                catch {{
                    Write-Error ""Error enabling feature: $($_.Exception.Message)""
                    return ""FAILURE|$($_.Exception.Message)""
                }}
            ";

            // Execute PowerShell using the correct interface method with our custom progress handler
            string resultString = await _powerShellService.ExecuteScriptAsync(
                script,
                progressHandler, // Use our custom progress handler instead of passing progress directly
                cancellationToken);

            // Process the result string
            if (!string.IsNullOrEmpty(resultString))
            {
                var parts = resultString.Split('|');
                if (parts.Length >= 2)
                {
                    string status = parts[0];
                    string message = parts[1];
                    bool rebootRequired = parts.Length > 2 && bool.TryParse(parts[2], out bool req) && req;

                    if (status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        progress?.Report(new TaskProgressDetail
                        {
                            Progress = 100,
                            StatusText = $"Successfully enabled {GetFriendlyName(featureName)}",
                            DetailedMessage = message
                        });
                        _logService.LogSuccess($"Successfully enabled optional feature: {featureName}. {message}");

                        if (rebootRequired)
                        {
                            progress?.Report(new TaskProgressDetail
                            {
                                StatusText = "A system restart is required to complete the installation",
                                DetailedMessage = "Please restart your computer to complete the installation",
                                LogLevel = LogLevel.Warning
                            });
                            _logService.LogWarning($"A system restart is required for {GetFriendlyName(featureName)}");
                        }
                        return OperationResult<bool>.Succeeded(true); // Indicate success
                    }
                    else // FAILURE
                    {
                        progress?.Report(new TaskProgressDetail
                        {
                            Progress = 0, // Indicate failure
                            StatusText = $"Failed to enable {GetFriendlyName(featureName)}",
                            DetailedMessage = message,
                            LogLevel = LogLevel.Error
                        });
                        _logService.LogError($"Failed to enable optional feature: {featureName}. {message}");
                        return OperationResult<bool>.Failed(message); // Indicate failure with message
                    }
                }
                else
                {
                     // Handle unexpected script output format
                    _logService.LogError($"Unexpected script output format for {featureName}: {resultString}");
                     progress?.Report(new TaskProgressDetail { StatusText = "Error processing script result", LogLevel = LogLevel.Error });
                     return OperationResult<bool>.Failed("Unexpected script output format: " + resultString); // Indicate failure with message
                }
            }
            else
            {
                // Handle case where script returned empty string
                _logService.LogError($"Empty result returned when enabling optional feature: {featureName}");
                progress?.Report(new TaskProgressDetail { StatusText = "Script returned no result", LogLevel = LogLevel.Error });
                return OperationResult<bool>.Failed("Script returned no result"); // Indicate failure with message
            }
        }
        catch (OperationCanceledException)
        {
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Operation cancelled when enabling {GetFriendlyName(featureName)}",
                    DetailedMessage = "The operation was cancelled by the user",
                    LogLevel = LogLevel.Warning,
                }
            );

            _logService.LogWarning($"Operation cancelled when enabling optional feature: {featureName}");
            return OperationResult<bool>.Failed("The operation was cancelled by the user"); // Return cancellation result
        }
        catch (Exception ex)
        {
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = $"Error enabling {GetFriendlyName(featureName)}",
                DetailedMessage = $"Exception: {ex.Message}",
                LogLevel = LogLevel.Error
            });
            _logService.LogError($"Error enabling optional feature: {featureName}", ex);
            return OperationResult<bool>.Failed($"Error enabling optional feature: {ex.Message}", ex); // Indicate failure with exception
        }
    }

    /// <summary>
    /// Gets the friendly name of a feature from its package name.
    /// </summary>
    /// <param name="packageName">The package name of the feature.</param>
    /// <returns>The friendly name of the feature, or the package name if not found.</returns>
    private string GetFriendlyName(string packageName)
    {
        // Look up the feature in the catalog by its package name
        var feature = _featureCatalog.Features.FirstOrDefault(f => 
            f.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase));
        
        // Return the friendly name if found, otherwise return the package name
        return feature?.Name ?? packageName;
    }
}

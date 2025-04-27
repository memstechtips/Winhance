using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.ScriptGeneration;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service for removing Windows optional features from the system.
/// </summary>
public class FeatureRemovalService : IFeatureRemovalService
{
    private readonly ILogService _logService;
    private readonly IAppDiscoveryService _appDiscoveryService;
    private readonly IScheduledTaskService _scheduledTaskService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureRemovalService"/> class.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    /// <param name="appDiscoveryService">The app discovery service.</param>
    /// <param name="scheduledTaskService">The scheduled task service.</param>
    public FeatureRemovalService(
        ILogService logService,
        IAppDiscoveryService appDiscoveryService,
        IScheduledTaskService scheduledTaskService)
    {
        _logService = logService;
        _appDiscoveryService = appDiscoveryService;
        _scheduledTaskService = scheduledTaskService;
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveFeatureAsync(
        FeatureInfo featureInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (featureInfo == null)
        {
            throw new ArgumentNullException(nameof(featureInfo));
        }
        // Call the other overload and return its result
        return await RemoveFeatureAsync(featureInfo.PackageName, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveFeatureAsync(
        string featureName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report(new TaskProgressDetail { Progress = 0, StatusText = $"Starting removal of {featureName}..." });
            _logService.LogInformation($"Removing optional feature: {featureName}");
            
            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
            // No need to set execution policy as it's already done in the factory
            
            // First, attempt to disable the feature
            powerShell.AddScript(@"
                param($featureName)
                try {
                    # Check if the feature exists and is enabled
                    $feature = Get-WindowsOptionalFeature -Online | Where-Object { $_.FeatureName -eq $featureName -and $_.State -eq 'Enabled' }
                    
                    if ($feature) {
                        # Get the full feature name
                        $fullFeatureName = $feature.FeatureName
                        
                        # Disable the feature
                        $result = Disable-WindowsOptionalFeature -Online -FeatureName $featureName -NoRestart
                        
                        return @{
                            FullFeatureName = $fullFeatureName
                        }
                    } else {
                        # Feature not found or already disabled
                        return @{
                            Error = ""Feature not found or already disabled""
                            FullFeatureName = $null
                        }
                    }
                }
                catch {
                    return @{
                        Error = $_.Exception.Message
                        FullFeatureName = $null
                    }
                }
            ");
            powerShell.AddParameter("featureName", featureName);
            
            var result = await Task.Run(() => powerShell.Invoke());
            
            // Extract any error or the full feature name from the first command
            string? error = null;
            string fullFeatureName = featureName;
            
            if (result != null && result.Count > 0)
            {
                var resultObj = result[0];
                
                // Get any error
                if (resultObj.Properties.Any(p => p.Name == "Error"))
                {
                    error = resultObj.Properties["Error"]?.Value as string;
                }
                
                // Get the full feature name if available
                if (resultObj.Properties.Any(p => p.Name == "FullFeatureName") && 
                    resultObj.Properties["FullFeatureName"]?.Value != null)
                {
                    fullFeatureName = resultObj.Properties["FullFeatureName"].Value.ToString();
                }
            }
            
            // Now check if the feature is still enabled to determine success
            powerShell.Commands.Clear();
            powerShell.AddScript(@"
                param($featureName)
                $feature = Get-WindowsOptionalFeature -Online | Where-Object { $_.FeatureName -eq $featureName }
                
                if ($feature -ne $null) {
                    return @{
                        IsEnabled = ($feature.State -eq 'Enabled')
                        RebootRequired = $feature.RestartNeeded
                    }
                } else {
                    return @{
                        IsEnabled = $false
                        RebootRequired = $false
                    }
                }
            ");
            powerShell.AddParameter("featureName", featureName);
            
            var statusResult = await Task.Run(() => powerShell.Invoke());
            
            bool isStillEnabled = false;
            bool rebootRequired = false;
            
            if (statusResult != null && statusResult.Count > 0)
            {
                var statusObj = statusResult[0];
                
                // Check if the feature is still enabled
                if (statusObj.Properties.Any(p => p.Name == "IsEnabled") && 
                    statusObj.Properties["IsEnabled"]?.Value != null)
                {
                    isStillEnabled = Convert.ToBoolean(statusObj.Properties["IsEnabled"].Value);
                }
                
                // Check if a reboot is required
                if (statusObj.Properties.Any(p => p.Name == "RebootRequired") && 
                    statusObj.Properties["RebootRequired"]?.Value != null)
                {
                    rebootRequired = Convert.ToBoolean(statusObj.Properties["RebootRequired"].Value);
                }
            }
            
            // Success is determined by whether the feature is no longer enabled
            bool success = !isStillEnabled;
            bool overallSuccess = false;
            
            if (success)
            {
                progress?.Report(new TaskProgressDetail { Progress = 100, StatusText = $"Successfully removed {featureName}" });
                _logService.LogSuccess($"Successfully removed optional feature: {featureName}");
                
                if (rebootRequired)
                {
                    progress?.Report(new TaskProgressDetail { StatusText = "Restart required", LogLevel = LogLevel.Warning });
                    _logService.LogWarning($"A system restart is required to complete the removal of {featureName}");
                }
                
                _logService.LogInformation($"Full feature name: {fullFeatureName}");
                
                // Update BloatRemoval.ps1 script after successful removal
                try
                {
                    var script = await UpdateBloatRemovalScriptAsync(fullFeatureName);
                    _logService.LogInformation($"BloatRemoval.ps1 script updated for feature: {featureName}");
                    
                    // Register the scheduled task to run the script at startup
                    try
                    {
                        bool taskRegistered = await RegisterBloatRemovalTaskAsync(script);
                        if (taskRegistered)
                        {
                            _logService.LogSuccess($"Scheduled task registered for BloatRemoval.ps1");
                        }
                        else
                        {
                            _logService.LogWarning($"Failed to register scheduled task for BloatRemoval.ps1, but continuing operation");
                        }
                    }
                    catch (Exception taskEx)
                    {
                        _logService.LogError($"Error registering scheduled task: {taskEx.Message}");
                        // Don't fail the removal if task registration fails
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error updating BloatRemoval.ps1 script for feature: {featureName}", ex);
                    // Don't fail the removal if script update fails
                }
                
                overallSuccess = true;
            }
            else
            {
                progress?.Report(new TaskProgressDetail { Progress = 0, StatusText = $"Failed to remove {featureName}: {error}", LogLevel = LogLevel.Error });
                _logService.LogError($"Failed to remove optional feature: {featureName}. {error}");
            }
            
            return overallSuccess; // Return success status
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error removing optional feature: {featureName}", ex);
            progress?.Report(new TaskProgressDetail { Progress = 0, StatusText = $"Error removing {featureName}: {ex.Message}", LogLevel = LogLevel.Error });
            return false; // Return false on exception
        }
    }

    /// <inheritdoc/>
    public Task<bool> CanRemoveFeatureAsync(FeatureInfo featureInfo)
    {
        // Basic implementation: Assume all found features can be removed.
        // TODO: Add actual checks if needed (e.g., dependencies, system protection)
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task<List<(string Name, bool Success, string? Error)>> RemoveFeaturesInBatchAsync(
        List<FeatureInfo> features)
    {
        if (features == null)
        {
            throw new ArgumentNullException(nameof(features));
        }

        return await RemoveFeaturesInBatchAsync(features.Select(f => f.PackageName).ToList());
    }

    /// <inheritdoc/>
    public async Task<List<(string Name, bool Success, string? Error)>> RemoveFeaturesInBatchAsync(
        List<string> featureNames)
    {
        var results = new List<(string Name, bool Success, string? Error)>();
        
        try
        {
            _logService.LogInformation($"Removing {featureNames.Count} Windows optional features");
            
            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
            // No need to set execution policy as it's already done in the factory
            
            foreach (var feature in featureNames)
            {
                try
                {
                    _logService.LogInformation($"Removing optional feature: {feature}");
                    
                    // First, attempt to disable the feature
                    powerShell.Commands.Clear();
                    powerShell.AddScript(@"
                        param($featureName)
                        try {
                            # Check if the feature exists and is enabled
                            $feature = Get-WindowsOptionalFeature -Online | Where-Object { $_.FeatureName -eq $featureName -and $_.State -eq 'Enabled' }
                            
                            if ($feature) {
                                # Get the full feature name
                                $fullFeatureName = $feature.FeatureName
                                
                                # Disable the feature
                                $result = Disable-WindowsOptionalFeature -Online -FeatureName $featureName -NoRestart
                                
                                return @{
                                    FullFeatureName = $fullFeatureName
                                }
                            } else {
                                # Feature not found or already disabled
                                return @{
                                    Error = ""Feature not found or already disabled""
                                    FullFeatureName = $null
                                }
                            }
                        }
                        catch {
                            return @{
                                Error = $_.Exception.Message
                                FullFeatureName = $null
                            }
                        }
                    ");
                    powerShell.AddParameter("featureName", feature);
                    
                    var result = await Task.Run(() => powerShell.Invoke());
                    
                    // Extract any error or the full feature name from the first command
                    string? error = null;
                    string featureFullName = feature;
                    
                    if (result != null && result.Count > 0)
                    {
                        var resultObj = result[0];
                        
                        // Get any error
                        if (resultObj.Properties.Any(p => p.Name == "Error"))
                        {
                            error = resultObj.Properties["Error"]?.Value as string;
                        }
                        
                        // Get the full feature name if available
                        if (resultObj.Properties.Any(p => p.Name == "FullFeatureName") && 
                            resultObj.Properties["FullFeatureName"]?.Value != null)
                        {
                            featureFullName = resultObj.Properties["FullFeatureName"].Value.ToString();
                        }
                    }
                    
                    // Now check if the feature is still enabled to determine success
                    powerShell.Commands.Clear();
                    powerShell.AddScript(@"
                        param($featureName)
                        $feature = Get-WindowsOptionalFeature -Online | Where-Object { $_.FeatureName -eq $featureName }
                        
                        if ($feature -ne $null) {
                            return @{
                                IsEnabled = ($feature.State -eq 'Enabled')
                                RebootRequired = $feature.RestartNeeded
                            }
                        } else {
                            return @{
                                IsEnabled = $false
                                RebootRequired = $false
                            }
                        }
                    ");
                    powerShell.AddParameter("featureName", feature);
                    
                    var statusResult = await Task.Run(() => powerShell.Invoke());
                    
                    bool isStillEnabled = false;
                    bool rebootRequired = false;
                    
                    if (statusResult != null && statusResult.Count > 0)
                    {
                        var statusObj = statusResult[0];
                        
                        // Check if the feature is still enabled
                        if (statusObj.Properties.Any(p => p.Name == "IsEnabled") && 
                            statusObj.Properties["IsEnabled"]?.Value != null)
                        {
                            isStillEnabled = Convert.ToBoolean(statusObj.Properties["IsEnabled"].Value);
                        }
                        
                        // Check if a reboot is required
                        if (statusObj.Properties.Any(p => p.Name == "RebootRequired") && 
                            statusObj.Properties["RebootRequired"]?.Value != null)
                        {
                            rebootRequired = Convert.ToBoolean(statusObj.Properties["RebootRequired"].Value);
                        }
                    }
                    
                    // Success is determined by whether the feature is no longer enabled
                    bool success = !isStillEnabled;
                    
                    if (success)
                    {
                        _logService.LogInformation($"Full feature name for batch operation: {featureFullName}");
                        
                        // Update BloatRemoval.ps1 script for batch operations too
                        try
                        {
                            var script = await UpdateBloatRemovalScriptAsync(featureFullName);
                            _logService.LogInformation($"BloatRemoval.ps1 script updated for feature in batch: {feature}");
                            
                            // Register the scheduled task to run the script at startup
                            try
                            {
                                bool taskRegistered = await RegisterBloatRemovalTaskAsync(script);
                                if (taskRegistered)
                                {
                                    _logService.LogSuccess($"Scheduled task registered for BloatRemoval.ps1");
                                }
                                else
                                {
                                    _logService.LogWarning($"Failed to register scheduled task for BloatRemoval.ps1, but continuing operation");
                                }
                            }
                            catch (Exception taskEx)
                            {
                                _logService.LogError($"Error registering scheduled task: {taskEx.Message}");
                                // Don't fail the removal if task registration fails
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError($"Error updating BloatRemoval.ps1 script for feature in batch: {feature}", ex);
                            // Don't fail the removal if script update fails
                        }
                    }
                    
                    results.Add((feature, success, error));
                    
                    if (success)
                    {
                        _logService.LogSuccess($"Successfully removed optional feature: {feature}");
                        if (rebootRequired)
                        {
                            _logService.LogWarning($"A system restart is required to complete the removal of {feature}");
                        }
                    }
                    else
                    {
                        _logService.LogError($"Failed to remove optional feature: {feature}. {error}");
                    }
                }
                catch (Exception ex)
                {
                    results.Add((feature, false, ex.Message));
                    _logService.LogError($"Error removing optional feature: {feature}", ex);
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error removing optional features", ex);
            return featureNames.Select(f => (f, false, $"Error: {ex.Message}")).ToList();
        }
    }

    // SetExecutionPolicy is now handled by PowerShellFactory
    
    /// <summary>
    /// Updates the BloatRemoval.ps1 script to add the removed feature to it.
    /// </summary>
    /// <param name="featureName">The feature name.</param>
    /// <returns>The updated removal script.</returns>
    private async Task<RemovalScript> UpdateBloatRemovalScriptAsync(string featureName)
    {
        try
        {
            // Get the script update service
            var scriptUpdateService = GetScriptUpdateService();
            if (scriptUpdateService == null)
            {
                _logService.LogWarning("Script update service not available");
                throw new InvalidOperationException("Script update service not available");
            }
            
            // Update the script
            var appNames = new List<string> { featureName };
            var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();
            var appSubPackages = new Dictionary<string, string[]>();
            
            _logService.LogInformation($"Updating BloatRemoval.ps1 script for feature: {featureName}");
            
            try
            {
                var script = await scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                    appNames,
                    appsWithRegistry,
                    appSubPackages,
                    false); // false = removal operation, so add to script
                
                _logService.LogInformation($"Successfully updated BloatRemoval.ps1 script for feature: {featureName}");
                return script;
            }
            catch (FileNotFoundException)
            {
                _logService.LogInformation($"BloatRemoval.ps1 script not found. It will be created when needed.");
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error in script update service for feature: {featureName}", ex);
                throw; // Rethrow to be caught by the outer try-catch
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error updating BloatRemoval.ps1 script for feature: {featureName}", ex);
            throw; // Rethrow so the caller can handle it
        }
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
            
            // Register the scheduled task
            bool success = await _scheduledTaskService.RegisterScheduledTaskAsync(script);
            
            if (success)
            {
                _logService.LogSuccess("Successfully registered scheduled task for BloatRemoval.ps1");
            }
            else
            {
                _logService.LogError("Failed to register scheduled task for BloatRemoval.ps1");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error registering scheduled task for BloatRemoval.ps1", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Gets the script update service.
    /// </summary>
    /// <returns>The script update service or null if not available.</returns>
    private IScriptUpdateService? GetScriptUpdateService()
    {
        try
        {
            // Use the existing _appDiscoveryService that was injected into the constructor
            // instead of creating a new instance
            var scriptContentModifier = new ScriptContentModifier(_logService);
            return new ScriptUpdateService(_logService, _appDiscoveryService, scriptContentModifier);
        }
        catch (Exception ex)
        {
            _logService.LogError("Failed to get script update service", ex);
            return null;
        }
    }
}

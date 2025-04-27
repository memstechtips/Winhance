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
/// Service for removing Windows capabilities from the system.
/// </summary>
public class CapabilityRemovalService : ICapabilityRemovalService
{
    private readonly ILogService _logService;
    private readonly IAppDiscoveryService _appDiscoveryService;
    private readonly IScheduledTaskService _scheduledTaskService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilityRemovalService"/> class.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    /// <param name="appDiscoveryService">The app discovery service.</param>
    /// <param name="scheduledTaskService">The scheduled task service.</param>
    public CapabilityRemovalService(
        ILogService logService,
        IAppDiscoveryService appDiscoveryService,
        IScheduledTaskService scheduledTaskService)
    {
        _logService = logService;
        _appDiscoveryService = appDiscoveryService;
        _scheduledTaskService = scheduledTaskService;
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveCapabilityAsync(
        CapabilityInfo capabilityInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (capabilityInfo == null)
        {
            throw new ArgumentNullException(nameof(capabilityInfo));
        }
        // Call the other overload and return its result
        return await RemoveCapabilityAsync(capabilityInfo.PackageName, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveCapabilityAsync(
        string capabilityName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report(new TaskProgressDetail { Progress = 0, StatusText = $"Starting removal of {capabilityName}..." });
            _logService.LogInformation($"Removing capability: {capabilityName}");
            
            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
            // No need to set execution policy as it's already done in the factory
            
            // Step 1: Get a list of all installed capabilities that match the pattern
            List<string> capabilityNames = new List<string>();
            try
            {
                powerShell.Commands.Clear();
                powerShell.AddScript($@"
                    Get-WindowsCapability -Online |
                    Where-Object {{ $_.Name -like '{capabilityName}*' }} |
                    Select-Object -ExpandProperty Name
                ");

                capabilityNames = (await Task.Run(() => powerShell.Invoke<string>())).ToList();
                _logService.LogInformation($"Found {capabilityNames.Count} capabilities matching '{capabilityName}*'");
                
                // Log the found capabilities
                foreach (var capName in capabilityNames)
                {
                    _logService.LogInformation($"Found installed capability: {capName}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error retrieving Windows capabilities for '{capabilityName}': {ex.Message}. Will proceed with empty capability list.");
                // Continue with empty capability list
            }

            // If no capabilities found with exact name, try with a more flexible pattern
            if (capabilityNames == null || !capabilityNames.Any())
            {
                try
                {
                    // Try with a more flexible pattern that might include tildes
                    powerShell.Commands.Clear();
                    powerShell.AddScript($@"
                        Get-WindowsCapability -Online |
                        Where-Object {{ $_.Name -match '{capabilityName}' }} |
                        Select-Object -ExpandProperty Name
                    ");

                    capabilityNames = (await Task.Run(() => powerShell.Invoke<string>())).ToList();
                    _logService.LogInformation($"Found {capabilityNames.Count} capabilities with flexible pattern matching '{capabilityName}'");
                    
                    // Log the found capabilities
                    foreach (var capName in capabilityNames)
                    {
                        _logService.LogInformation($"Found installed capability with flexible pattern: {capName}");
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error retrieving capabilities with flexible pattern: {ex.Message}");
                }
            }

            bool overallSuccess = true;
            
            if (capabilityNames != null && capabilityNames.Any())
            {
                int totalCapabilities = capabilityNames.Count;
                int currentCapability = 0;
                
                foreach (var capName in capabilityNames)
                {
                    currentCapability++;
                    progress?.Report(new TaskProgressDetail {
                        Progress = (currentCapability * 100) / totalCapabilities,
                        StatusText = $"Removing capability {currentCapability} of {totalCapabilities}: {capName}"
                    });
                    
                    _logService.LogInformation($"Removing installed capability: {capName}");

                    // Step 2: Remove the capability
                    bool success = false;
                    try
                    {
                        powerShell.Commands.Clear();
                        powerShell.AddScript($@"
                            Remove-WindowsCapability -Name '{capName}' -Online -ErrorAction SilentlyContinue
                            return $?
                        ");

                        var result = await Task.Run(() => powerShell.Invoke<bool>());
                        success = result.FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Error removing capability '{capName}': {ex.Message}");
                        success = false;
                    }

                    if (success)
                    {
                        _logService.LogSuccess($"Successfully removed capability: {capName}");
                    }
                    else
                    {
                        _logService.LogError($"Failed to remove capability: {capName}");
                        overallSuccess = false;
                    }
                }
                
                // Update BloatRemoval.ps1 script and register scheduled task
                if (overallSuccess)
                {
                    try
                    {
                        // Pass the full capability names (with version numbers) to the script update service
                        var script = await UpdateBloatRemovalScriptAsync(capabilityNames);
                        
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
                    catch (Exception scriptEx)
                    {
                        _logService.LogError($"Error updating BloatRemoval.ps1 script: {scriptEx.Message}");
                        // Don't fail the removal if script update fails
                    }
                }
                
                progress?.Report(new TaskProgressDetail {
                    Progress = 100,
                    StatusText = overallSuccess ? $"Successfully removed all capabilities" : $"Some capabilities could not be removed",
                    DetailedMessage = $"Removed {capabilityNames.Count} capabilities matching {capabilityName}",
                    LogLevel = overallSuccess ? LogLevel.Success : LogLevel.Warning
                });
                
                return overallSuccess;
            }
            else
            {
                _logService.LogInformation($"No installed capabilities found matching: {capabilityName}*");
                progress?.Report(new TaskProgressDetail {
                    Progress = 100,
                    StatusText = $"No installed capabilities found matching: {capabilityName}*",
                    LogLevel = LogLevel.Warning
                });
                
                // Consider it a success if nothing needs to be removed
                return true;
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error removing capability: {capabilityName}", ex);
            progress?.Report(new TaskProgressDetail {
                Progress = 0,
                StatusText = $"Error removing {capabilityName}: {ex.Message}",
                LogLevel = LogLevel.Error
            });
            return false;
        }
    }

    // Add missing CanRemoveCapabilityAsync method
    /// <inheritdoc/>
    public Task<bool> CanRemoveCapabilityAsync(CapabilityInfo capabilityInfo)
    {
        // Basic implementation: Assume all found capabilities can be removed.
        // TODO: Add actual checks if needed (e.g., dependencies)
        return Task.FromResult(true);
    }


    /// <inheritdoc/>
    public async Task<List<(string Name, bool Success, string? Error)>> RemoveCapabilitiesInBatchAsync(
        List<CapabilityInfo> capabilities)
    {
        if (capabilities == null)
        {
            throw new ArgumentNullException(nameof(capabilities));
        }

        return await RemoveCapabilitiesInBatchAsync(capabilities.Select(c => c.PackageName).ToList());
    }

    /// <inheritdoc/>
    public async Task<List<(string Name, bool Success, string? Error)>> RemoveCapabilitiesInBatchAsync(
        List<string> capabilityNames)
    {
        var results = new List<(string Name, bool Success, string? Error)>();
        
        try
        {
            _logService.LogInformation($"Removing {capabilityNames.Count} Windows capabilities");
            
            using var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
            // No need to set execution policy as it's already done in the factory
            
            foreach (var capability in capabilityNames)
            {
                try
                {
                    _logService.LogInformation($"Removing capability: {capability}");
                    
                    // Step 1: Get a list of all installed capabilities that match the pattern
                    List<string> matchingCapabilities = new List<string>();
                    try
                    {
                        powerShell.Commands.Clear();
                        powerShell.AddScript($@"
                            Get-WindowsCapability -Online |
                            Where-Object {{ $_.Name -like '{capability}*' -and $_.State -eq 'Installed' }} |
                            Select-Object -ExpandProperty Name
                        ");

                        matchingCapabilities = (await Task.Run(() => powerShell.Invoke<string>())).ToList();
                        _logService.LogInformation($"Found {matchingCapabilities.Count} capabilities matching '{capability}*' for batch removal");
                        
                        // Log the found capabilities
                        foreach (var capName in matchingCapabilities)
                        {
                            _logService.LogInformation($"Found installed capability for batch removal: {capName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Error retrieving Windows capabilities for batch removal '{capability}': {ex.Message}. Will proceed with empty capability list.");
                        // Continue with empty capability list
                    }

                    bool success = true;
                    string? error = null;
                    
                    if (matchingCapabilities != null && matchingCapabilities.Any())
                    {
                        foreach (var capName in matchingCapabilities)
                        {
                            _logService.LogInformation($"Removing installed capability in batch: {capName}");

                            // Step 2: Remove the capability
                            bool capSuccess = false;
                            try
                            {
                                powerShell.Commands.Clear();
                                powerShell.AddScript($@"
                                    Remove-WindowsCapability -Name '{capName}' -Online -ErrorAction SilentlyContinue
                                    return $?
                                ");

                                var capResult = await Task.Run(() => powerShell.Invoke<bool>());
                                capSuccess = capResult.FirstOrDefault();
                            }
                            catch (Exception ex)
                            {
                                _logService.LogError($"Error removing capability in batch '{capName}': {ex.Message}");
                                capSuccess = false;
                            }

                            if (capSuccess)
                            {
                                _logService.LogSuccess($"Successfully removed capability in batch: {capName}");
                            }
                            else
                            {
                                _logService.LogError($"Failed to remove capability in batch: {capName}");
                                success = false;
                                error = $"Failed to remove one or more capabilities";
                            }
                        }
                    }
                    else
                    {
                        _logService.LogInformation($"No installed capabilities found matching for batch removal: {capability}*");
                        // Consider it a success if nothing needs to be removed
                        success = true;
                    }
                    
                    // If successful, update the BloatRemoval.ps1 script with the full capability names
                    if (success && matchingCapabilities.Any())
                    {
                        try
                        {
                            await UpdateBloatRemovalScriptAsync(matchingCapabilities);
                            _logService.LogInformation($"Updated BloatRemoval.ps1 script with full capability names for batch removal");
                        }
                        catch (Exception scriptEx)
                        {
                            _logService.LogError($"Error updating BloatRemoval.ps1 script for batch capability removal: {scriptEx.Message}");
                            // Don't fail the removal if script update fails
                        }
                    }
                    
                    results.Add((capability, success, error));
                }
                catch (Exception ex)
                {
                    results.Add((capability, false, ex.Message));
                    _logService.LogError($"Error removing capability: {capability}", ex);
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logService.LogError("Error removing capabilities", ex);
            return capabilityNames.Select(c => (c, false, $"Error: {ex.Message}")).ToList();
        }
    }

    // SetExecutionPolicy is now handled by PowerShellFactory
    /// <summary>
    /// Updates the BloatRemoval.ps1 script to add the removed capabilities to it.
    /// </summary>
    /// <param name="capabilityNames">The list of capability names with version numbers.</param>
    /// <returns>The updated removal script.</returns>
    private async Task<RemovalScript> UpdateBloatRemovalScriptAsync(List<string> capabilityNames)
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
            
            // Update the script with the full capability names (including version numbers)
            var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();
            var appSubPackages = new Dictionary<string, string[]>();
            
            _logService.LogInformation($"Updating BloatRemoval.ps1 script for {capabilityNames.Count} capabilities");
            
            try
            {
                var script = await scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                    capabilityNames,
                    appsWithRegistry,
                    appSubPackages,
                    false); // false = removal operation, so add to script
                
                _logService.LogInformation($"Successfully updated BloatRemoval.ps1 script for capabilities");
                return script;
            }
            catch (FileNotFoundException)
            {
                _logService.LogInformation($"BloatRemoval.ps1 script not found. It will be created when needed.");
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error in script update service for capabilities: {ex.Message}", ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error updating BloatRemoval.ps1 script for capabilities: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Updates the BloatRemoval.ps1 script to add the removed capability to it.
    /// </summary>
    /// <param name="capabilityName">The capability name.</param>
    /// <returns>The updated removal script.</returns>
    private async Task<RemovalScript> UpdateBloatRemovalScriptAsync(string capabilityName)
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
            var appNames = new List<string> { capabilityName };
            var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();
            var appSubPackages = new Dictionary<string, string[]>();
            
            _logService.LogInformation($"Updating BloatRemoval.ps1 script for capability: {capabilityName}");
            
            try
            {
                var script = await scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                    appNames,
                    appsWithRegistry,
                    appSubPackages,
                    false); // false = removal operation, so add to script
                
                _logService.LogInformation($"Successfully updated BloatRemoval.ps1 script for capability: {capabilityName}");
                return script;
            }
            catch (FileNotFoundException)
            {
                _logService.LogInformation($"BloatRemoval.ps1 script not found. It will be created when needed.");
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error in script update service for capability: {capabilityName}", ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error updating BloatRemoval.ps1 script for capability: {capabilityName}", ex);
            throw;
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
            // This is a simplified implementation. In a real application, you would use dependency injection.
            // For now, we'll create a new instance directly.
            // Use the injected _appDiscoveryService instead of creating a new instance
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

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
/// Service that enables Windows capabilities.
/// </summary>
public class CapabilityInstallationService : BaseInstallationService<CapabilityInfo>, ICapabilityInstallationService
{
    private readonly CapabilityCatalog _capabilityCatalog;
    private readonly IScriptUpdateService _scriptUpdateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilityInstallationService"/> class.
    /// </summary>
    /// <param name="logService">The log service.</param>
    /// <param name="powerShellService">The PowerShell execution service.</param>
    /// <param name="scriptUpdateService">The script update service.</param>
    public CapabilityInstallationService(
        ILogService logService,
        IPowerShellExecutionService powerShellService,
        IScriptUpdateService scriptUpdateService
    ) : base(logService, powerShellService)
    {
        // Create a default capability catalog
        _capabilityCatalog = CapabilityCatalog.CreateDefault();
        _scriptUpdateService = scriptUpdateService ?? throw new ArgumentNullException(nameof(scriptUpdateService));
    }

    /// <inheritdoc/>
    public Task<OperationResult<bool>> InstallCapabilityAsync(
        CapabilityInfo capabilityInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        return InstallItemAsync(capabilityInfo, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<OperationResult<bool>> CanInstallCapabilityAsync(CapabilityInfo capabilityInfo)
    {
        return CanInstallItemAsync(capabilityInfo);
    }

    /// <inheritdoc/>
    protected override async Task<OperationResult<bool>> PerformInstallationAsync(
        CapabilityInfo capabilityInfo,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var result = await InstallCapabilityAsync(capabilityInfo.PackageName, progress, cancellationToken);
        
        // Only update BloatRemoval.ps1 script if installation was successful
        if (result.Success)
        {
            try
            {
                _logService.LogInformation($"Starting BloatRemoval.ps1 script update for {capabilityInfo.Name}");
                
                // Update the BloatRemoval.ps1 script to remove the installed capability from the removal list
                var appNames = new List<string> { capabilityInfo.PackageName };
                _logService.LogInformation($"Removing capability name from BloatRemoval.ps1: {capabilityInfo.PackageName}");
                
                var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();
                var appSubPackages = new Dictionary<string, string[]>();

                // Add registry settings if present
                if (capabilityInfo.RegistrySettings != null && capabilityInfo.RegistrySettings.Length > 0)
                {
                    _logService.LogInformation($"Adding {capabilityInfo.RegistrySettings.Length} registry settings for {capabilityInfo.Name}");
                    appsWithRegistry.Add(capabilityInfo.PackageName, new List<AppRegistrySetting>(capabilityInfo.RegistrySettings));
                }

                _logService.LogInformation($"Updating BloatRemoval.ps1 to remove {capabilityInfo.Name} from removal list");
                
                // Check if the capability name already includes a version (~~~~)
                string fullCapabilityName = capabilityInfo.PackageName;
                if (!fullCapabilityName.Contains("~~~~"))
                {
                    // We don't have a version in the package name, but we might be able to extract it from installed capabilities
                    _logService.LogInformation($"Package name doesn't include version information: {fullCapabilityName}");
                    _logService.LogInformation($"Using package name as is: {fullCapabilityName}");
                }
                else
                {
                    _logService.LogInformation($"Using full capability name with version: {fullCapabilityName}");
                }
                
                // Always use the package name as provided
                appNames = new List<string> { fullCapabilityName };
                
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
                
                _logService.LogInformation($"Successfully updated BloatRemoval.ps1 script - {capabilityInfo.Name} will no longer be removed");
                _logService.LogInformation($"Script update result: {scriptResult?.Name ?? "null"}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error updating BloatRemoval.ps1 script for {capabilityInfo.Name}", ex);
                _logService.LogError($"Exception details: {ex.Message}");
                _logService.LogError($"Stack trace: {ex.StackTrace}");
                // Don't fail the installation if script update fails
            }
        }
        else
        {
            _logService.LogInformation($"Skipping BloatRemoval.ps1 update because installation of {capabilityInfo.Name} was not successful");
        }
        
        return result;
    }

    /// <summary>
    /// Installs a Windows capability by name.
    /// </summary>
    /// <param name="capabilityName">The name of the capability to install.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An operation result indicating success or failure with error details.</returns>
    private async Task<OperationResult<bool>> InstallCapabilityAsync(
        string capabilityName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Get the friendly name of the capability from the catalog
            string friendlyName = GetFriendlyName(capabilityName);
            
            // Set a more descriptive initial status using the friendly name
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Enabling {friendlyName}...",
                    DetailedMessage = $"Starting to enable capability: {capabilityName}",
                }
            );

            _logService.LogInformation($"Attempting to enable capability: {capabilityName}");
            
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

            // Define the PowerShell script - Embed capabilityName, output parseable string
            // Output format: STATUS|Message|RebootRequired (e.g., SUCCESS|Installed 1 of 1|True)
            string script = $@"
                try {{
                    $capabilityNamePattern = '{capabilityName}*' # Embed pattern directly
                    Write-Information ""Searching for capability: $capabilityNamePattern""
                    # Progress reporting needs to be handled by the caller based on script output or duration

                    # Find matching capabilities
                    $capabilities = Get-WindowsCapability -Online | Where-Object {{ $_.Name -like $capabilityNamePattern -and $_.State -ne 'Installed' }}

                    if ($capabilities.Count -eq 0) {{
                        # Check if it's already installed
                        $alreadyInstalled = Get-WindowsCapability -Online | Where-Object {{ $_.Name -like $capabilityNamePattern -and $_.State -eq 'Installed' }}
                        if ($alreadyInstalled) {{
                             return ""SUCCESS|Capability already installed|False""
                        }} else {{
                             Write-Warning ""No matching capabilities found: $capabilityNamePattern""
                             return ""FAILURE|No matching capabilities found""
                        }}
                    }}

                    $totalCapabilities = $capabilities.Count
                    $rebootRequired = $false
                    $installedCount = 0
                    $errorMessages = @()

                    foreach ($capability in $capabilities) {{
                        Write-Information ""Installing capability: $($capability.Name)""
                        try {{
                            $result = Add-WindowsCapability -Online -Name $capability.Name
                            if ($result.RestartNeeded) {{
                                $rebootRequired = $true
                            }}
                            $installedCount++
                        }}
                        catch {{
                            $errMsg = ""Failed to install capability: $($capability.Name). $($_.Exception.Message)""
                            Write-Error $errMsg
                            $errorMessages += $errMsg
                        }}
                    }}

                    if ($installedCount -gt 0) {{
                        $rebootNeededStr = if ($rebootRequired) {{ 'True' }} else {{ 'False' }}
                        $finalMessage = ""Successfully installed $installedCount of $totalCapabilities capabilities.""
                        if ($errorMessages.Count -gt 0) {{
                            $finalMessage += "" Errors: $($errorMessages -join '; ')""
                        }}
                        return ""SUCCESS|$finalMessage|$rebootNeededStr""
                    }} else {{
                        $finalMessage = ""Failed to install any capabilities.""
                        if ($errorMessages.Count -gt 0) {{
                            $finalMessage += "" Errors: $($errorMessages -join '; ')""
                        }}
                        return ""FAILURE|$finalMessage""
                    }}
                }}
                catch {{
                    Write-Error ""Error enabling capability: $($_.Exception.Message)""
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
                             StatusText = $"Successfully enabled {GetFriendlyName(capabilityName)}",
                             DetailedMessage = message
                         });
                         _logService.LogSuccess($"Successfully enabled capability: {capabilityName}. {message}");

                         if (rebootRequired)
                         {
                             progress?.Report(new TaskProgressDetail
                             {
                                 StatusText = "A system restart is required to complete the installation",
                                 DetailedMessage = "Please restart your computer to complete the installation",
                                 LogLevel = LogLevel.Warning
                             });
                             _logService.LogWarning($"A system restart is required for {GetFriendlyName(capabilityName)}");
                         }
                         return OperationResult<bool>.Succeeded(true); // Indicate success
                     }
                     else // FAILURE
                     {
                         progress?.Report(new TaskProgressDetail
                         {
                             Progress = 0, // Indicate failure
                             StatusText = $"Failed to enable {GetFriendlyName(capabilityName)}",
                             DetailedMessage = message,
                             LogLevel = LogLevel.Error
                         });
                         _logService.LogError($"Failed to enable capability: {capabilityName}. {message}");
                         return OperationResult<bool>.Failed(message); // Indicate failure with message
                     }
                 }
                 else
                 {
                     // Handle unexpected script output format
                     _logService.LogError($"Unexpected script output format for {capabilityName}: {resultString}");
                     progress?.Report(new TaskProgressDetail { StatusText = "Error processing script result", LogLevel = LogLevel.Error });
                     return OperationResult<bool>.Failed("Unexpected script output format: " + resultString); // Indicate failure with message
                 }
            }
            else
            {
                 // Handle case where script returned empty string
                 _logService.LogError($"Empty result returned when enabling capability: {capabilityName}");
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
                    StatusText = $"Operation cancelled when enabling {GetFriendlyName(capabilityName)}",
                    DetailedMessage = "The operation was cancelled by the user",
                    LogLevel = LogLevel.Warning,
                }
            );

            _logService.LogWarning($"Operation cancelled when enabling capability: {capabilityName}");
            return OperationResult<bool>.Failed("The operation was cancelled by the user"); // Return cancellation result
        }
        catch (Exception ex)
        {
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = $"Error enabling {GetFriendlyName(capabilityName)}",
                DetailedMessage = $"Exception: {ex.Message}",
                LogLevel = LogLevel.Error
            });
            _logService.LogError($"Error enabling capability: {capabilityName}", ex);
            return OperationResult<bool>.Failed($"Error enabling capability: {ex.Message}", ex); // Indicate failure with exception
        }
    }

    // Note: CheckInstalled is not part of the ICapabilityInstallationService interface
    // It should likely be moved or removed if not used elsewhere.
    // For now, commenting it out to fix build errors.
    /*
    public bool CheckInstalled(CapabilityInfo capabilityInfo)
    {
        // ... (Implementation needs fixing similar to InstallCapabilityAsync)
    }
    */

    // Note: InstallCapabilitiesAsync is not part of the ICapabilityInstallationService interface
    // It should likely be moved or removed if not used elsewhere.
    // For now, commenting it out to fix build errors.
    /*
    public Task InstallCapabilitiesAsync(IEnumerable<CapabilityInfo> capabilities)
    {
        return InstallCapabilitiesAsync(capabilities, null, default);
    }

    public async Task InstallCapabilitiesAsync(
        IEnumerable<CapabilityInfo> capabilities,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        // ... (Implementation needs fixing similar to InstallCapabilityAsync)
    }
    */

    // Note: RemoveCapabilitiesAsync is not part of the ICapabilityInstallationService interface
    // It should likely be moved or removed if not used elsewhere.
    // For now, commenting it out to fix build errors.
    /*
    public async Task RemoveCapabilitiesAsync(IEnumerable<CapabilityInfo> capabilities)
    {
       // ... (Implementation needs fixing similar to InstallCapabilityAsync)
    }

    private async Task RemoveCapabilityAsync(CapabilityInfo capabilityInfo)
    {
       // ... (Implementation needs fixing similar to InstallCapabilityAsync)
    }
    */

    /// <summary>
    /// Gets the friendly name of a capability from its package name.
    /// </summary>
    /// <param name="packageName">The package name of the capability.</param>
    /// <returns>The friendly name of the capability, or the package name if not found.</returns>
    private string GetFriendlyName(string packageName)
    {
        // Remove any version information from the package name (e.g., "Media.WindowsMediaPlayer~~~~0.0.12.0" -> "Media.WindowsMediaPlayer")
        string basePackageName = packageName.Split('~')[0];
        
        // Look up the capability in the catalog by its package name
        var capability = _capabilityCatalog.Capabilities.FirstOrDefault(c =>
            c.PackageName.Equals(basePackageName, StringComparison.OrdinalIgnoreCase));
        
        // Return the friendly name if found, otherwise return the package name
        return capability?.Name ?? packageName;
    }
}

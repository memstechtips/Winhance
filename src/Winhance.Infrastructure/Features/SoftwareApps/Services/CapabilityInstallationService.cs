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

            // Set a more descriptive initial status using the friendly name with download expectation
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Enabling {friendlyName}... Files are being downloaded via Windows Update, this process could take 15 minutes or more, please wait...",
                    DetailedMessage = $"Starting to enable capability: {capabilityName}. This may require downloading files from Microsoft servers.",
                }
            );

            _logService.LogInformation($"Attempting to enable capability: {capabilityName}");

            // Create an optimized progress handler that reduces reporting frequency for long operations
            DateTime lastProgressReport = DateTime.MinValue;
            var progressHandler = new Progress<TaskProgressDetail>(detail =>
            {
                // Throttle progress updates for responsive UI while maintaining performance
                var now = DateTime.Now;
                bool shouldReport = (now - lastProgressReport).TotalSeconds >= 3; // Report every 3 seconds for fast performance

                // Always report significant progress changes or completion
                if (detail.Progress.HasValue && (detail.Progress >= 100 || detail.Progress == 0))
                    shouldReport = true;

                if (!shouldReport) return;

                lastProgressReport = now;

                // If we get a generic "Operation: Running" status, replace it with our more descriptive one
                if (detail.StatusText != null && detail.StatusText.StartsWith("Operation:"))
                {
                    // Keep the percentage but replace the generic text with informative message
                    if (detail.Progress.HasValue && detail.Progress > 0)
                    {
                        detail.StatusText = $"Enabling {friendlyName}... ({detail.Progress:F0}%) - Files are being downloaded via Windows Update, this process could take 15 minutes or more, please wait...";
                    }
                    else
                    {
                        detail.StatusText = $"Enabling {friendlyName}... Files are being downloaded via Windows Update, this process could take 15 minutes or more, please wait...";
                    }
                }

                // Forward the updated progress to the original progress reporter
                progress?.Report(detail);
            });

            // First check if capability is available offline to optimize performance
            bool isOfflineAvailable = await IsCapabilityAvailableOfflineAsync(capabilityName);
            if (!isOfflineAvailable)
            {
                _logService.LogInformation($"Capability {capabilityName} requires online download, this may take 15+ minutes");
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 5,
                    StatusText = $"Downloading {friendlyName} from Windows Update... This process could take 15 minutes or more, please wait...",
                    DetailedMessage = "This capability requires download from Microsoft servers and may take significant time to complete.",
                    LogLevel = LogLevel.Info
                });
            }

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
                            StatusText = $"Successfully enabled {GetFriendlyName(capabilityName)}! Thank you for waiting.",
                            DetailedMessage = $"{message} The Windows Update download and installation process is now complete."
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
                // Check if this is due to cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    _logService.LogWarning($"Capability installation was cancelled: {capabilityName}");
                    progress?.Report(new TaskProgressDetail
                    {
                        StatusText = $"Installation of {GetFriendlyName(capabilityName)} was cancelled",
                        DetailedMessage = "The installation was cancelled by the user",
                        LogLevel = LogLevel.Warning
                    });
                    return OperationResult<bool>.Failed("The installation was cancelled by the user");
                }
                else
                {
                    _logService.LogError($"Empty result returned when enabling capability: {capabilityName}");
                    progress?.Report(new TaskProgressDetail { StatusText = "Script returned no result", LogLevel = LogLevel.Error });
                    return OperationResult<bool>.Failed("Script returned no result"); // Indicate failure with message
                }
            }
        }
        catch (OperationCanceledException)
        {
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Cancelling {GetFriendlyName(capabilityName)} installation...",
                    DetailedMessage = "Stopping Windows Update and system processes, please wait...",
                    LogLevel = LogLevel.Warning,
                }
            );

            _logService.LogWarning($"Operation cancelled when enabling capability: {capabilityName}");

            // Attempt to stop related Windows processes that may still be running
            await StopCapabilityInstallationProcessesAsync(capabilityName, progress);

            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Operation cancelled when enabling {GetFriendlyName(capabilityName)}",
                    DetailedMessage = "The operation was cancelled and system processes have been stopped",
                    LogLevel = LogLevel.Warning,
                }
            );

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
    /// Checks if a capability is available offline (cached locally) to avoid network delays.
    /// </summary>
    /// <param name="capabilityName">The capability name to check.</param>
    /// <returns>True if the capability is available offline, false if it requires download.</returns>
    private async Task<bool> IsCapabilityAvailableOfflineAsync(string capabilityName)
    {
        try
        {
            // Quick check using DISM to see if capability source is local
            string checkScript = $@"
                try {{
                    $capability = Get-WindowsCapability -Online | Where-Object {{ $_.Name -like '{capabilityName}*' }} | Select-Object -First 1
                    if ($capability) {{
                        # Check if it's already installed (fastest case)
                        if ($capability.State -eq 'Installed') {{
                            return 'ALREADY_INSTALLED'
                        }}
                        # For uninstalled capabilities, assume they need download for safety
                        # In future versions, we could check Windows\WinSxS or other local sources
                        return 'REQUIRES_DOWNLOAD'
                    }} else {{
                        return 'NOT_FOUND'
                    }}
                }} catch {{
                    return 'ERROR'
                }}
            ";

            var result = await _powerShellService.ExecuteScriptAsync(checkScript);

            // If already installed, it's "offline available" in the sense that no download is needed
            if (result?.Trim() == "ALREADY_INSTALLED")
            {
                return true;
            }

            // For now, assume most capabilities require download
            // This could be enhanced with more sophisticated local cache checking
            return false;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not determine offline availability for {capabilityName}: {ex.Message}");
            return false; // Assume download required on error
        }
    }

    /// <summary>
    /// Attempts to stop Windows system processes related to capability installation.
    /// </summary>
    /// <param name="capabilityName">The capability being installed.</param>
    /// <param name="progress">Progress reporter for user feedback.</param>
    /// <returns>Task representing the async operation.</returns>
    private async Task StopCapabilityInstallationProcessesAsync(string capabilityName, IProgress<TaskProgressDetail>? progress)
    {
        try
        {
            _logService.LogInformation($"Attempting to stop Windows processes for cancelled capability installation: {capabilityName}");

            // PowerShell script to stop Windows capability installation processes
            string stopScript = @"
                try {
                    Write-Information 'Stopping Windows capability installation processes...'
                    
                    # Stop Windows Modules Installer processes (TiWorker.exe)
                    $tiWorkerProcesses = Get-Process -Name 'TiWorker' -ErrorAction SilentlyContinue
                    foreach ($process in $tiWorkerProcesses) {
                        try {
                            Write-Information ""Stopping TiWorker process (PID: $($process.Id))""
                            $process.Kill()
                            Write-Information ""Successfully stopped TiWorker process""
                        } catch {
                            Write-Warning ""Failed to stop TiWorker process: $($_.Exception.Message)""
                        }
                    }
                    
                    # Stop DISM processes that might be running
                    $dismProcesses = Get-Process -Name 'Dism*' -ErrorAction SilentlyContinue
                    foreach ($process in $dismProcesses) {
                        try {
                            Write-Information ""Stopping DISM process: $($process.Name) (PID: $($process.Id))""
                            $process.Kill()
                            Write-Information ""Successfully stopped DISM process""
                        } catch {
                            Write-Warning ""Failed to stop DISM process: $($_.Exception.Message)""
                        }
                    }
                    
                    # Stop any PowerShell processes that might be hanging
                    $currentPid = $PID
                    $powershellProcesses = Get-Process -Name 'powershell*' -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $currentPid }
                    foreach ($process in $powershellProcesses) {
                        try {
                            # Check if this PowerShell process is related to capability installation
                            $commandLine = (Get-WmiObject Win32_Process -Filter ""ProcessId = $($process.Id)"" -ErrorAction SilentlyContinue).CommandLine
                            if ($commandLine -and ($commandLine -like '*Add-WindowsCapability*' -or $commandLine -like '*Get-WindowsCapability*')) {
                                Write-Information ""Stopping capability-related PowerShell process (PID: $($process.Id))""
                                $process.Kill()
                                Write-Information ""Successfully stopped PowerShell process""
                            }
                        } catch {
                            Write-Warning ""Failed to stop PowerShell process: $($_.Exception.Message)""
                        }
                    }
                    
                    # Try to cancel any pending Windows Update operations
                    try {
                        Write-Information 'Attempting to stop Windows Update Service temporarily...'
                        Stop-Service -Name 'wuauserv' -Force -ErrorAction SilentlyContinue
                        Start-Sleep -Seconds 2
                        Start-Service -Name 'wuauserv' -ErrorAction SilentlyContinue
                        Write-Information 'Windows Update Service restarted'
                    } catch {
                        Write-Warning ""Failed to restart Windows Update Service: $($_.Exception.Message)""
                    }
                    
                    Write-Information 'Process cleanup completed'
                    return 'SUCCESS|Process cleanup completed'
                } catch {
                    Write-Error ""Error during process cleanup: $($_.Exception.Message)""
                    return ""PARTIAL|Some processes may still be running: $($_.Exception.Message)""
                }
            ";

            // Execute the cleanup script
            var result = await _powerShellService.ExecuteScriptAsync(stopScript);

            if (result?.Contains("SUCCESS") == true)
            {
                _logService.LogInformation("Successfully stopped Windows capability installation processes");
            }
            else
            {
                _logService.LogWarning($"Process cleanup completed with warnings: {result}");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error stopping Windows capability installation processes: {ex.Message}", ex);
            // Don't throw - this is cleanup, we don't want to mask the original cancellation
        }
    }

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

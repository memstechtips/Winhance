using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Exceptions;
using Winhance.Core.Features.SoftwareApps.Helpers;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.ScriptGeneration;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service that installs standard applications.
/// </summary>
public class AppInstallationService : BaseInstallationService<AppInfo>, IAppInstallationService, IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "WinhanceInstaller");
    private readonly HttpClient _httpClient;
    private readonly IScriptUpdateService _scriptUpdateService;
    private IProgress<TaskProgressDetail>? _currentProgress;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppInstallationService"/> class.
    /// </summary>
    /// <param name="logService">The log service.</param>
    /// <param name="powerShellService">The PowerShell execution service.</param>
    /// <param name="scriptUpdateService">The script update service.</param>
    public AppInstallationService(
        ILogService logService,
        IPowerShellExecutionService powerShellService,
        IScriptUpdateService scriptUpdateService
    ) : base(logService, powerShellService)
    {
        _httpClient = new HttpClient();
        _scriptUpdateService = scriptUpdateService;
        Directory.CreateDirectory(_tempDir);
    }

    /// <inheritdoc/>
    public Task<OperationResult<bool>> InstallAppAsync(
        AppInfo appInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        return InstallItemAsync(appInfo, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<OperationResult<bool>> CanInstallAppAsync(AppInfo appInfo)
    {
        return CanInstallItemAsync(appInfo);
    }

    /// <inheritdoc/>
    protected override async Task<OperationResult<bool>> PerformInstallationAsync(
        AppInfo appInfo,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        _currentProgress = progress;

        try
        {
            bool success = false;

            if (appInfo.PackageName.Equals("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                // Special handling for OneDrive
                success = await InstallOneDriveAsync(progress, cancellationToken);
            }
            else if (appInfo.IsCustomInstall)
            {
                success = await InstallCustomAppAsync(appInfo, progress, cancellationToken);
            }
            else
            {
                // Use WinGet for all standard apps, including appx packages
                // Use PackageID if available, otherwise fall back to PackageName
                string packageIdentifier = !string.IsNullOrEmpty(appInfo.PackageID)
                    ? appInfo.PackageID
                    : appInfo.PackageName;

                // Pass the app's display name to use in progress messages
                success = await InstallWithWingetAsync(packageIdentifier, progress, cancellationToken, appInfo.Name);
            }

            // Only update BloatRemoval.ps1 if installation was successful
            if (success)
            {
                try
                {
                    _logService.LogInformation($"Starting BloatRemoval.ps1 script update for {appInfo.Name}");
                    
                    // Update the BloatRemoval.ps1 script to remove the installed app from the removal list
                    var appNames = new List<string> { appInfo.PackageName };
                    _logService.LogInformation($"Removing package name from BloatRemoval.ps1: {appInfo.PackageName}");
                    
                    var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();
                    var appSubPackages = new Dictionary<string, string[]>();

                    // Add any subpackages if present
                    if (appInfo.SubPackages != null && appInfo.SubPackages.Length > 0)
                    {
                        _logService.LogInformation($"Adding {appInfo.SubPackages.Length} subpackages for {appInfo.Name}");
                        appSubPackages.Add(appInfo.PackageName, appInfo.SubPackages);
                    }

                    // Add registry settings if present
                    if (appInfo.RegistrySettings != null && appInfo.RegistrySettings.Length > 0)
                    {
                        _logService.LogInformation($"Adding {appInfo.RegistrySettings.Length} registry settings for {appInfo.Name}");
                        appsWithRegistry.Add(appInfo.PackageName, new List<AppRegistrySetting>(appInfo.RegistrySettings));
                    }

                    _logService.LogInformation($"Updating BloatRemoval.ps1 to remove {appInfo.Name} from removal list");
                    var result = await _scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                        appNames,
                        appsWithRegistry,
                        appSubPackages,
                        true); // true = install operation, so remove from script
                    
                    _logService.LogInformation($"Successfully updated BloatRemoval.ps1 script - {appInfo.Name} will no longer be removed");
                    _logService.LogInformation($"Script update result: {result?.Name ?? "null"}");
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error updating BloatRemoval.ps1 script for {appInfo.Name}", ex);
                    _logService.LogError($"Exception details: {ex.Message}");
                    _logService.LogError($"Stack trace: {ex.StackTrace}");
                    // Don't fail the installation if script update fails
                }
            }
            else
            {
                _logService.LogInformation($"Skipping BloatRemoval.ps1 update because installation of {appInfo.Name} was not successful");
            }

            return OperationResult<bool>.Succeeded(success);
        }
        finally
        {
            _currentProgress = null;
        }
    }


    private async Task InstallWinGetAsync()
    {
        _currentProgress?.Report(
            new TaskProgressDetail
            {
                Progress = 10,
                StatusText = "Installing WinGet...",
                DetailedMessage = "Starting WinGet installation process using Windows PowerShell 5.1",
            }
        );

        // Create a temporary script file
        string scriptPath = Path.Combine(_tempDir, $"WinGetInstall_{Guid.NewGuid()}.ps1");
        
        // Use the Microsoft recommended PowerShell script to install WinGet
        string installScript = @"
            $progressPreference = 'silentlyContinue'
            Write-Host ""Installing WinGet PowerShell module from PSGallery...""
            Install-PackageProvider -Name NuGet -Force | Out-Null
            Install-Module -Name Microsoft.WinGet.Client -Force -Repository PSGallery | Out-Null
            Write-Host ""Using Repair-WinGetPackageManager cmdlet to bootstrap WinGet...""
            Repair-WinGetPackageManager
            Write-Host ""Done.""
        ";
        
        // Write the installation script to the file
        File.WriteAllText(scriptPath, installScript);
        
        try
        {
            _currentProgress?.Report(
                new TaskProgressDetail
                {
                    Progress = 20,
                    StatusText = "Downloading and installing WinGet components...",
                    DetailedMessage = "Downloading and installing required WinGet components using Windows PowerShell 5.1",
                }
            );
            
            // Create a process to run Windows PowerShell 5.1 directly
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
            process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();
            
            process.OutputDataReceived += (sender, e) => {
                if (e.Data != null) {
                    outputBuilder.AppendLine(e.Data);
                    _logService.LogInformation($"WinGet Install: {e.Data}");
                }
            };
            
            process.ErrorDataReceived += (sender, e) => {
                if (e.Data != null) {
                    errorBuilder.AppendLine(e.Data);
                    _logService.LogError($"WinGet Install Error: {e.Data}");
                }
            };
            
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await Task.Run(() => process.WaitForExit());
            
            if (process.ExitCode != 0)
            {
                string errorMessages = errorBuilder.ToString();
                throw new Exception($"Failed to install WinGet. Error: {errorMessages}");
            }
            
            _currentProgress?.Report(
                new TaskProgressDetail
                {
                    Progress = 90,
                    StatusText = "WinGet installation completed.",
                    DetailedMessage = "WinGet has been successfully installed using Windows PowerShell 5.1",
                }
            );
        }
        catch (Exception ex)
        {
            var errorType = InstallationErrorHelper.DetermineErrorType(ex.Message);
            var errorMessage = InstallationErrorHelper.GetUserFriendlyErrorMessage(errorType);

            throw new InstallationException(
                "WinGet",
                $"Error installing WinGet: {errorMessage}",
                true,
                errorType,
                ex);
        }
        finally
        {
            // Clean up the temporary script file
            try
            {
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error cleaning up temporary script file: {ex.Message}", ex);
                // Ignore cleanup errors
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> InstallWithWingetAsync(
        string packageName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default,
        string displayName = null
    )
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            throw new ArgumentException(
                "Package name cannot be null or empty",
                nameof(packageName)
            );
        }

        try
        {
            // Store the progress reporter for use in the private method
            _currentProgress = progress;

            // Report initial progress
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Starting installation of {displayName ?? packageName}...",
                    DetailedMessage = $"Preparing to install {displayName ?? packageName} using WinGet",
                }
            );

            // Call the existing private method
            await InstallWithWingetAsync(packageName, displayName);

            // Report completion
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = $"{displayName ?? packageName} installed successfully!",
                    DetailedMessage = $"Successfully installed {displayName ?? packageName} using WinGet",
                }
            );

            return true;
        }
        catch (OperationCanceledException)
        {
            var errorType = InstallationErrorType.CancelledByUserError;
            var errorMessage = InstallationErrorHelper.GetUserFriendlyErrorMessage(errorType);

            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Installation of {displayName ?? packageName} was cancelled",
                    DetailedMessage = errorMessage,
                    LogLevel = LogLevel.Warning,
                }
            );

            throw new InstallationException(
                packageName,
                errorMessage,
                false,
                errorType,
                new OperationCanceledException());
        }
        catch (Exception ex)
        {
            // Determine the error type based on the exception message
            var errorType = InstallationErrorHelper.DetermineErrorType(ex.Message);
            var errorMessage = InstallationErrorHelper.GetUserFriendlyErrorMessage(errorType);

            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Error installing {displayName ?? packageName}: {errorMessage}",
                    DetailedMessage = $"Exception during installation: {ex.Message}",
                    LogLevel = LogLevel.Error,
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        { "ErrorType", errorType.ToString() },
                        { "PackageName", packageName },
                        { "OriginalError", ex.Message }
                    }
                }
            );

            return false;
        }
        finally
        {
            // Clear the progress reporter
            _currentProgress = null;
        }
    }

    private async Task InstallWithWingetAsync(string packageName, string displayName = null)
    {
        // Create PowerShell instance - not using 'using' so we can replace it if needed
        var powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
        
        // Create a timer to provide more granular progress updates
        var timer = new System.Timers.Timer(500);
        var startTime = DateTime.Now;
        var lastReportedProgress = 5;
        var currentPhase = "Preparing";
        var phaseStartTimes = new Dictionary<string, DateTime>
        {
            { "Preparing", DateTime.Now },
            { "Downloading", DateTime.MinValue },
            { "Verifying", DateTime.MinValue },
            { "Installing", DateTime.MinValue },
            { "Completing", DateTime.MinValue }
        };
        
        // Setup the timer to report progress
        timer.Elapsed += (s, e) =>
        {
            // Only report if PowerShell is still running
            if (powerShell.InvocationStateInfo.State == PSInvocationState.Running)
            {
                // Calculate overall progress based on elapsed time
                var totalElapsed = (DateTime.Now - startTime).TotalSeconds;
                var estimatedTotalTime = 60; // Assume 60 seconds for a typical installation
                var overallProgress = Math.Min(85, (int)(totalElapsed / estimatedTotalTime * 100));
                
                // Ensure progress is always increasing
                if (overallProgress <= lastReportedProgress)
                {
                    overallProgress = lastReportedProgress + 1;
                }
                
                // Cap at 85% to leave room for verification
                overallProgress = Math.Min(85, overallProgress);
                
                // Only report if progress has changed
                if (overallProgress > lastReportedProgress)
                {
                    lastReportedProgress = overallProgress;
                    
                    // Determine current phase based on elapsed time
                    if (totalElapsed > 45 && currentPhase != "Completing")
                    {
                        currentPhase = "Completing";
                        phaseStartTimes["Completing"] = DateTime.Now;
                    }
                    else if (totalElapsed > 30 && currentPhase != "Installing" && currentPhase != "Completing")
                    {
                        currentPhase = "Installing";
                        phaseStartTimes["Installing"] = DateTime.Now;
                    }
                    else if (totalElapsed > 15 && currentPhase != "Verifying" && currentPhase != "Installing" && currentPhase != "Completing")
                    {
                        currentPhase = "Verifying";
                        phaseStartTimes["Verifying"] = DateTime.Now;
                    }
                    else if (totalElapsed > 5 && currentPhase == "Preparing")
                    {
                        currentPhase = "Downloading";
                        phaseStartTimes["Downloading"] = DateTime.Now;
                    }
                    
                    // Calculate phase-specific progress
                    var phaseElapsed = (DateTime.Now - phaseStartTimes[currentPhase]).TotalSeconds;
                    var phaseProgress = 0;
                    
                    switch (currentPhase)
                    {
                        case "Downloading":
                            phaseProgress = Math.Min(100, (int)(phaseElapsed / 10 * 100)); // Assume 10 seconds for downloading
                            break;
                        case "Verifying":
                            phaseProgress = Math.Min(100, (int)(phaseElapsed / 15 * 100)); // Assume 15 seconds for verifying
                            break;
                        case "Installing":
                            phaseProgress = Math.Min(100, (int)(phaseElapsed / 15 * 100)); // Assume 15 seconds for installing
                            break;
                        case "Completing":
                            phaseProgress = Math.Min(100, (int)(phaseElapsed / 15 * 100)); // Assume 15 seconds for completing
                            break;
                        default:
                            phaseProgress = Math.Min(100, (int)(phaseElapsed / 5 * 100)); // Assume 5 seconds for preparing
                            break;
                    }
                    
                    // Report progress
                    _currentProgress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = 10 + (int)(overallProgress * 0.8), // Scale to 10-90% range
                            StatusText = $"Installing {displayName ?? packageName}... {currentPhase} ({phaseProgress}%)",
                            DetailedMessage = GetDetailedMessageForPhase(packageName, currentPhase, phaseProgress)
                        }
                    );
                }
            }
        };
        
        // Start the timer
        timer.Start();

        // First check if WinGet is installed
        _currentProgress?.Report(
            new TaskProgressDetail
            {
                Progress = 5,
                StatusText = "Checking if WinGet is installed...",
                DetailedMessage = "Verifying if WinGet is already installed on the system",
            }
        );

        powerShell.AddScript("winget --version");
        var result = await powerShell.InvokeAsync();

        if (!result.Any())
        {
            _currentProgress?.Report(
                new TaskProgressDetail
                {
                    Progress = 10,
                    StatusText = "WinGet not found. Installing WinGet first...",
                    DetailedMessage = "WinGet is not installed. Installing it now.",
                }
            );
            await InstallWinGetAsync();

            // After installing WinGet, dispose the current PowerShell instance and create a new one
            // to ensure we can use the newly installed WinGet
            timer.Stop();
            timer.Dispose();
            powerShell.Dispose();
            
            _logService.LogInformation("Creating new PowerShell instance after WinGet installation");
            
            // Create a new PowerShell instance
            powerShell = PowerShellFactory.CreateWindowsPowerShell(_logService);
            
            // Create a new timer
            timer = new System.Timers.Timer(500);
            startTime = DateTime.Now;
            lastReportedProgress = 5;
            currentPhase = "Preparing";
            phaseStartTimes = new Dictionary<string, DateTime>
            {
                { "Preparing", DateTime.Now },
                { "Downloading", DateTime.MinValue },
                { "Verifying", DateTime.MinValue },
                { "Installing", DateTime.MinValue },
                { "Completing", DateTime.MinValue }
            };
            
            // Re-setup the timer event handler
            timer.Elapsed += (s, e) =>
            {
                // Only report if PowerShell is still running
                if (powerShell.InvocationStateInfo.State == PSInvocationState.Running)
                {
                    // Calculate overall progress based on elapsed time
                    var totalElapsed = (DateTime.Now - startTime).TotalSeconds;
                    var estimatedTotalTime = 60; // Assume 60 seconds for a typical installation
                    var overallProgress = Math.Min(85, (int)(totalElapsed / estimatedTotalTime * 100));
                    
                    // Ensure progress is always increasing
                    if (overallProgress <= lastReportedProgress)
                    {
                        overallProgress = lastReportedProgress + 1;
                    }
                    
                    // Cap at 85% to leave room for verification
                    overallProgress = Math.Min(85, overallProgress);
                    
                    // Only report if progress has changed
                    if (overallProgress > lastReportedProgress)
                    {
                        lastReportedProgress = overallProgress;
                        
                        // Determine current phase based on elapsed time
                        if (totalElapsed > 45 && currentPhase != "Completing")
                        {
                            currentPhase = "Completing";
                            phaseStartTimes["Completing"] = DateTime.Now;
                        }
                        else if (totalElapsed > 30 && currentPhase != "Installing" && currentPhase != "Completing")
                        {
                            currentPhase = "Installing";
                            phaseStartTimes["Installing"] = DateTime.Now;
                        }
                        else if (totalElapsed > 15 && currentPhase != "Verifying" && currentPhase != "Installing" && currentPhase != "Completing")
                        {
                            currentPhase = "Verifying";
                            phaseStartTimes["Verifying"] = DateTime.Now;
                        }
                        else if (totalElapsed > 5 && currentPhase == "Preparing")
                        {
                            currentPhase = "Downloading";
                            phaseStartTimes["Downloading"] = DateTime.Now;
                        }
                        
                        // Calculate phase-specific progress
                        var phaseElapsed = (DateTime.Now - phaseStartTimes[currentPhase]).TotalSeconds;
                        var phaseProgress = 0;
                        
                        switch (currentPhase)
                        {
                            case "Downloading":
                                phaseProgress = Math.Min(100, (int)(phaseElapsed / 10 * 100)); // Assume 10 seconds for downloading
                                break;
                            case "Verifying":
                                phaseProgress = Math.Min(100, (int)(phaseElapsed / 15 * 100)); // Assume 15 seconds for verifying
                                break;
                            case "Installing":
                                phaseProgress = Math.Min(100, (int)(phaseElapsed / 15 * 100)); // Assume 15 seconds for installing
                                break;
                            case "Completing":
                                phaseProgress = Math.Min(100, (int)(phaseElapsed / 15 * 100)); // Assume 15 seconds for completing
                                break;
                            default:
                                phaseProgress = Math.Min(100, (int)(phaseElapsed / 5 * 100)); // Assume 5 seconds for preparing
                                break;
                        }
                        
                        // Report progress
                        _currentProgress?.Report(
                            new TaskProgressDetail
                            {
                                Progress = 10 + (int)(overallProgress * 0.8), // Scale to 10-90% range
                                StatusText = $"Installing {displayName ?? packageName}... {currentPhase} ({phaseProgress}%)",
                                DetailedMessage = GetDetailedMessageForPhase(packageName, currentPhase, phaseProgress)
                            }
                        );
                    }
                }
            };
            
            // Start the timer
            timer.Start();
            
            // Verify WinGet installation was successful
            powerShell.AddScript("winget --version");
            result = await powerShell.InvokeAsync();

            if (!result.Any())
            {
                timer.Stop();
                timer.Dispose();
                throw new Exception(
                    "Failed to install WinGet. Unable to continue with package installation."
                );
            }
            
            _logService.LogInformation("WinGet verification successful, continuing with package installation");
        }

        // Clear previous commands and install the app
        _currentProgress?.Report(
            new TaskProgressDetail
            {
                Progress = 10,
                StatusText = $"Installing {displayName ?? packageName}...",
                DetailedMessage = $"Starting the installation process for {displayName ?? packageName}",
            }
        );

        powerShell.Commands.Clear();

        // Create a PowerShell script that will handle the installation more robustly
        var installScript = @"
            try {
                # Check if the package is already installed
                $installedPackage = winget list --id 'PACKAGE_NAME' --exact
                $alreadyInstalled = $installedPackage -match 'PACKAGE_NAME'
                
                if ($alreadyInstalled) {
                    Write-Output ""Package PACKAGE_NAME is already installed""
                    return @{
                        Success = $true
                        Message = ""Package is already installed""
                        ExitCode = 0
                    }
                }
                
                # For Microsoft Store apps (package IDs that are alphanumeric with no dots), use msstore source explicitly
                $useStoreSource = 'PACKAGE_NAME' -match '^[A-Z0-9]+$'
                $sourceArg = if ($useStoreSource) { '--source msstore' } else { '' }
                
                # Start the winget process with more detailed output
                $processInfo = New-Object System.Diagnostics.ProcessStartInfo
                $processInfo.FileName = 'winget'
                $processInfo.Arguments = 'install --id PACKAGE_NAME -e --accept-package-agreements --accept-source-agreements ' + $sourceArg
                $processInfo.RedirectStandardOutput = $true
                $processInfo.RedirectStandardError = $true
                $processInfo.UseShellExecute = $false
                $processInfo.CreateNoWindow = $true
                
                Write-Output ""Running command: winget $($processInfo.Arguments)""
                
                $process = New-Object System.Diagnostics.Process
                $process.StartInfo = $processInfo
                $process.Start() | Out-Null
                
                # Monitor the process
                $startTime = Get-Date
                $estimatedDuration = New-TimeSpan -Minutes 5  # Increased timeout for store apps
                $outputBuilder = New-Object System.Text.StringBuilder
                $lastOutput = """"
                $lastProgressReport = 0
                $installPhase = ""Preparing""
                
                # Start async reading
                $outputTask = $process.StandardOutput.ReadToEndAsync()
                $errorTask = $process.StandardError.ReadToEndAsync()
                
                # Create a reader to read output as it becomes available
                $reader = $process.StandardOutput.BaseStream
                $buffer = New-Object byte[] 4096
                
                while (!$process.HasExited) {
                    # Calculate time-based progress
                    $elapsed = (Get-Date) - $startTime
                    $timeProgress = [Math]::Min(1.0, $elapsed.TotalMilliseconds / $estimatedDuration.TotalMilliseconds)
                    $timeProgressPercent = [int]($timeProgress * 100)
                    
                    # Try to read any available output
                    if ($reader.CanRead -and $reader.DataAvailable) {
                        $bytesRead = $reader.Read($buffer, 0, $buffer.Length)
                        if ($bytesRead -gt 0) {
                            $text = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $bytesRead)
                            $outputBuilder.Append($text)
                            $lastOutput = $text
                            
                            # Determine installation phase based on output
                            if ($text -match ""Downloading"") { $installPhase = ""Downloading"" }
                            elseif ($text -match ""Verifying"") { $installPhase = ""Verifying"" }
                            elseif ($text -match ""Installing"") { $installPhase = ""Installing"" }
                            elseif ($text -match ""Completing"") { $installPhase = ""Completing"" }
                        }
                    }
                    
                    # Only report progress if it's changed by at least 2% or the phase has changed
                    if (($timeProgressPercent - $lastProgressReport) -ge 2) {
                        Write-Output ""Progress: $timeProgressPercent% - $installPhase""
                        $lastProgressReport = $timeProgressPercent
                    }
                    
                    # Wait a bit before checking again
                    Start-Sleep -Milliseconds 200
                }
                
                # Get the output
                $output = $outputTask.Result
                $errorOutput = $errorTask.Result
                
                Write-Output ""Command output: $output""
                if ($errorOutput) { Write-Output ""Error output: $errorOutput"" }
                Write-Output ""Exit code: $($process.ExitCode)""
                
                # Check for success indicators in the output
                $isSuccessful = $output -match 'Successfully installed' -or 
                                $output -match 'already installed' -or
                                $output -match 'Installation complete' -or
                                $process.ExitCode -eq 0 -or
                                # Special case: For Windows Store apps, exit code -1 is often a success
                                ($useStoreSource -and $process.ExitCode -eq -1)
                
                # If that fails, try a second installation attempt with msstore source explicitly
                if (!$isSuccessful -and !$useStoreSource) {
                    Write-Output ""First attempt failed, trying with msstore source explicitly...""
                    
                    $processInfo = New-Object System.Diagnostics.ProcessStartInfo
                    $processInfo.FileName = 'winget'
                    $processInfo.Arguments = 'install --id PACKAGE_NAME -e --source msstore --accept-package-agreements --accept-source-agreements'
                    $processInfo.RedirectStandardOutput = $true
                    $processInfo.RedirectStandardError = $true
                    $processInfo.UseShellExecute = $false
                    $processInfo.CreateNoWindow = $true
                    
                    Write-Output ""Running command: winget $($processInfo.Arguments)""
                    
                    $process = New-Object System.Diagnostics.Process
                    $process.StartInfo = $processInfo
                    $process.Start() | Out-Null
                    
                    # Start async reading
                    $outputTask = $process.StandardOutput.ReadToEndAsync()
                    $errorTask = $process.StandardError.ReadToEndAsync()
                    
                    $process.WaitForExit()
                    
                    # Get the output
                    $output = $outputTask.Result
                    $errorOutput = $errorTask.Result
                    
                    Write-Output ""Second attempt output: $output""
                    if ($errorOutput) { Write-Output ""Second attempt error output: $errorOutput"" }
                    Write-Output ""Second attempt exit code: $($process.ExitCode)""
                    
                    # Check for success indicators in the output
                    $isSuccessful = $output -match 'Successfully installed' -or 
                                    $output -match 'already installed' -or
                                    $output -match 'Installation complete' -or
                                    $process.ExitCode -eq 0
                }
                
                # Some packages might return non-zero exit codes even on successful installation
                # So we check both the exit code and the output
                if ($isSuccessful) {
                    Write-Output ""Installation completed successfully""
                    return @{
                        Success = $true
                        Message = ""Installation completed successfully""
                        ExitCode = $process.ExitCode
                        Output = $output
                    }
                } else {
                    return @{
                        Success = $false
                        Message = ""Installation failed with exit code: $($process.ExitCode)""
                        ExitCode = $process.ExitCode
                        Output = $output
                        Error = $errorOutput
                    }
                }
            } catch {
                Write-Output ""Exception occurred: $($_.Exception.Message)""
                return @{
                    Success = $false
                    Message = ""Exception: $($_.Exception.Message)""
                    ExitCode = -1
                    Error = $_.Exception.ToString()
                }
            }
        ";

        // Replace placeholders with actual values
        installScript = installScript.Replace("PACKAGE_NAME", packageName);

        powerShell.AddScript(installScript);

        // Create a data event handler to process output in real-time
        var progressRegex = new Regex(@"Progress: (\d+)% - (\w+)", RegexOptions.Compiled);
        var simpleProgressRegex = new Regex(@"Progress: (\d+)%", RegexOptions.Compiled);

        // Set up the information stream handler
        EventHandler<DataAddedEventArgs> informationHandler = (sender, e) =>
        {
            var info = powerShell.Streams.Information[e.Index];
            var data = info.MessageData.ToString() ?? "";

            var match = progressRegex.Match(data);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int progressValue))
            {
                string phase = match.Groups[2].Value;
                
                // Scale progress from 10-90% range
                var scaledProgress = 10 + (progressValue * 0.8);
                _currentProgress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = (int)scaledProgress,
                        StatusText = $"Installing {packageName}... {progressValue}% - {phase}",
                        DetailedMessage = GetDetailedMessageForPhase(packageName, phase, progressValue),
                    }
                );
            }
            else
            {
                // Try the simple progress regex as fallback
                match = simpleProgressRegex.Match(data);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int progressPercent))
                {
                    // Scale progress from 10-90% range
                    var scaledProgress = 10 + (progressPercent * 0.8);
                    _currentProgress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = (int)scaledProgress,
                            StatusText = $"Installing {packageName}... {progressPercent}%",
                            DetailedMessage = $"Installation of {packageName} is {progressPercent}% complete",
                        }
                    );
                }
                else if (
                    data.Contains("Installation completed successfully")
                    || data.Contains("already installed")
                )
                {
                    _currentProgress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = 95,
                            StatusText = $"Finalizing installation of {packageName}...",
                            DetailedMessage = $"Completing the installation process for {packageName}",
                        }
                    );
                }
            }
        };
        
        powerShell.Streams.Information.DataAdded += informationHandler;

        try
        {
            var results = await powerShell.InvokeAsync();

            if (results.Count > 0 && results[0] is PSObject resultObj)
            {
                // Extract the result properties
                bool success =
                    resultObj.Properties["Success"]?.Value is bool successValue && successValue;
                string message = resultObj.Properties["Message"]?.Value?.ToString() ?? "";
                int exitCode = resultObj.Properties["ExitCode"]?.Value is int code ? code : -1;
                string output = resultObj.Properties["Output"]?.Value?.ToString() ?? "";
                string error = resultObj.Properties["Error"]?.Value?.ToString() ?? "";

                // Stop the timer
                timer.Stop();
                timer.Dispose();
                
                // Unsubscribe from the event handler
                powerShell.Streams.Information.DataAdded -= informationHandler;
                
                // Log detailed information about the installation attempt
                _currentProgress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 90,
                        StatusText = $"Installation for {displayName ?? packageName} completed successfully!",
                        DetailedMessage = $"Installation for {displayName ?? packageName} completed successfully!",
                    }
                );

                // Always verify if the app is actually installed, regardless of reported success
                bool isActuallyInstalled = await VerifyAppInstallationAsync(
                    packageName,
                    powerShell
                );

                if (isActuallyInstalled)
                {
                    _currentProgress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = 95,
                            StatusText = $"Package verified as installed",
                            DetailedMessage = $"Verified that {packageName} is correctly installed",
                        }
                    );
                    return;
                }

                if (!success)
                {
                    // Check for common success patterns in the output that might be missed by the script
                    if (
                        output.Contains("already installed")
                        || output.Contains("Successfully installed")
                        || output.Contains("Installation complete")
                        || output.Contains("installation completed")
                        || output.Contains("installed successfully")
                        ||
                        // Microsoft Store apps often have different success messages
                        (
                            packageName.All(char.IsLetterOrDigit)
                            && !packageName.Contains('.')
                        )
                        ||
                        // Consider any exit code as potentially successful for verification if other indicators are positive
                        exitCode >= 0
                    )
                    {
                        // The package was actually installed despite the error
                        _currentProgress?.Report(
                            new TaskProgressDetail
                            {
                                Progress = 95,
                                StatusText =
                                    $"Package appears to be installed despite reported errors",
                                DetailedMessage =
                                    $"The package {packageName} appears to be installed correctly despite some reported errors",
                            }
                        );
                        return;
                    }

                    var errorType = InstallationErrorType.UnknownError;

                    // Try to determine a more specific error type based on the message or output
                    if (!string.IsNullOrEmpty(message))
                    {
                        errorType = InstallationErrorHelper.DetermineErrorType(message);
                    }
                    else if (!string.IsNullOrEmpty(output))
                    {
                        errorType = InstallationErrorHelper.DetermineErrorType(output);
                    }

                    var errorMessage = InstallationErrorHelper.GetUserFriendlyErrorMessage(errorType);

                    throw new InstallationException(
                        packageName,
                        $"Failed to install {packageName}. {errorMessage} (Exit code: {exitCode})",
                        errorType == InstallationErrorType.PermissionError ||
                        errorType == InstallationErrorType.WinGetNotInstalledError,
                        errorType,
                        new Exception(message)
                    );
                }
            }
            else if (powerShell.HadErrors)
            {
                // Even if there were errors, check if the app was actually installed
                bool isActuallyInstalled = await VerifyAppInstallationAsync(
                    packageName,
                    powerShell
                );

                if (isActuallyInstalled)
                {
                    _currentProgress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = 95,
                            StatusText = $"Package verified as installed despite reported errors",
                            DetailedMessage =
                                $"Verified that {packageName} is correctly installed despite some reported errors",
                        }
                    );
                    return;
                }

                var errorMessages = string.Join(
                    Environment.NewLine,
                    powerShell.Streams.Error.Select(e => e.Exception?.Message ?? e.ToString())
                );

                // Check if we should ignore this error (some packages report errors even when successfully installed)
                if (
                    errorMessages.Contains("already installed")
                    || errorMessages.Contains("Successfully installed")
                    || errorMessages.Contains("Installation complete")
                )
                {
                    _currentProgress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = 95,
                            StatusText = $"Package appears to be installed despite reported errors",
                            DetailedMessage =
                                $"The package {packageName} appears to be installed correctly despite some reported errors",
                        }
                    );
                    return;
                }

                var errorType = InstallationErrorHelper.DetermineErrorType(errorMessages);
                var errorMessage = InstallationErrorHelper.GetUserFriendlyErrorMessage(errorType);

                throw new InstallationException(
                    packageName,
                    $"Failed to install {packageName}. {errorMessage}",
                    errorType == InstallationErrorType.PermissionError ||
                    errorType == InstallationErrorType.WinGetNotInstalledError,
                    errorType,
                    new Exception(errorMessages)
                );
            }
        }
        catch (InstallationException)
        {
            // Stop the timer
            timer.Stop();
            timer.Dispose();
            
            // Unsubscribe from the event handler
            powerShell.Streams.Information.DataAdded -= informationHandler;
            
            // Re-throw InstallationException without wrapping
            throw;
        }
        catch (Exception ex)
        {
            // Stop the timer
            timer.Stop();
            timer.Dispose();
            
            // Unsubscribe from the event handler
            powerShell.Streams.Information.DataAdded -= informationHandler;
            
            var errorType = InstallationErrorHelper.DetermineErrorType(ex.Message);
            var errorMessage = InstallationErrorHelper.GetUserFriendlyErrorMessage(errorType);

            throw new InstallationException(
                packageName,
                $"Error installing {packageName}: {errorMessage}",
                errorType == InstallationErrorType.PermissionError ||
                errorType == InstallationErrorType.WinGetNotInstalledError,
                errorType,
                ex
            );
        }
        finally
        {
            // Ensure PowerShell is disposed
            if (powerShell != null)
            {
                powerShell.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> InstallCustomAppAsync(
        AppInfo appInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Handle different custom app installations based on package name
            switch (appInfo.PackageName.ToLowerInvariant())
            {
                // Add custom app installation logic here
                // case "some-app":
                //    return await InstallSomeAppAsync(progress, cancellationToken);

                default:
                    throw new NotSupportedException(
                        $"Custom installation for '{appInfo.PackageName}' is not supported."
                    );
            }
        }
        catch (Exception ex)
        {
            var errorType = InstallationErrorHelper.DetermineErrorType(ex.Message);
            var errorMessage = InstallationErrorHelper.GetUserFriendlyErrorMessage(errorType);

            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Error in custom installation for {appInfo.Name}: {errorMessage}",
                    DetailedMessage = $"Exception during custom installation: {ex.Message}",
                    LogLevel = LogLevel.Error,
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        { "ErrorType", errorType.ToString() },
                        { "PackageName", appInfo.PackageName },
                        { "AppName", appInfo.Name },
                        { "IsCustomInstall", "True" },
                        { "OriginalError", ex.Message }
                    }
                }
            );

            return false;
        }
    }

    // Common Internet Check Method
    private async Task<bool> CheckInternetConnectionAsync()
    {
        try
        {
            // Try to reach a reliable site to check for internet connectivity
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync("https://www.google.com");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Helper method to verify if an app is actually installed
    private async Task<bool> VerifyAppInstallationAsync(string packageName, PowerShell powerShell)
    {
        try
        {
            // Clear any previous commands
            powerShell.Commands.Clear();

            // For Microsoft Store apps (package IDs that are alphanumeric with no dots)
            if (packageName.All(char.IsLetterOrDigit) && !packageName.Contains('.'))
            {
                // Use Get-AppxPackage to check if the Microsoft Store app is installed
                powerShell.AddScript(
                    $"Get-AppxPackage | Where-Object {{ $_.PackageFullName -like '*{packageName}*' }}"
                );
                var result = await powerShell.InvokeAsync();

                if (result.Count > 0)
                {
                    return true;
                }
            }

            // For all other apps, use winget list to check if installed
            powerShell.Commands.Clear();
            powerShell.AddScript(
                $@"
                try {{
                    $result = winget list --id '{packageName}' --exact
                    $isInstalled = $result -match '{packageName}'
                    Write-Output $isInstalled
                }} catch {{
                    Write-Output $false
                }}
            "
            );

            var results = await powerShell.InvokeAsync();

            // Check if the result indicates the app is installed
            if (results.Count > 0)
            {
                // Extract boolean value from result
                var resultValue = results[0]?.ToString()?.ToLowerInvariant();
                if (resultValue == "true")
                {
                    return true;
                }
            }

            // If we're here, try one more verification with a different approach
            powerShell.Commands.Clear();
            powerShell.AddScript(
                $@"
                try {{
                    # Use where.exe to check if the app is in PATH (for CLI tools)
                    $whereResult = where.exe {packageName} 2>&1
                    if ($whereResult -notmatch 'not found') {{
                        Write-Output 'true'
                        return
                    }}
                    
                    # Check common installation directories
                    $commonPaths = @(
                        [System.Environment]::GetFolderPath('ProgramFiles'),
                        [System.Environment]::GetFolderPath('ProgramFilesX86'),
                        [System.Environment]::GetFolderPath('LocalApplicationData')
                    )
                    
                    foreach ($basePath in $commonPaths) {{
                        if (Test-Path -Path ""$basePath\$packageName"" -PathType Container) {{
                            Write-Output 'true'
                            return
                        }}
                    }}
                    
                    Write-Output 'false'
                }} catch {{
                    Write-Output 'false'
                }}
            "
            );

            results = await powerShell.InvokeAsync();

            // Check if the result indicates the app is installed
            if (results.Count > 0)
            {
                var resultValue = results[0]?.ToString()?.ToLowerInvariant();
                return resultValue == "true";
            }

            return false;
        }
        catch
        {
            // If any error occurs during verification, assume the app is not installed
            return false;
        }
    }

    /// <summary>
    /// Disposes the resources used by the service.
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Installs OneDrive from the Microsoft download link.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if installation was successful; otherwise, false.</returns>
    private async Task<bool> InstallOneDriveAsync(
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = "Starting OneDrive installation...",
                DetailedMessage = "Downloading OneDrive installer from Microsoft"
            });

            // Download OneDrive from the specific URL
            string downloadUrl = "https://go.microsoft.com/fwlink/p/?LinkID=2182910";
            string installerPath = Path.Combine(_tempDir, "OneDriveSetup.exe");

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(downloadUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = 0,
                        StatusText = "Failed to download OneDrive installer",
                        DetailedMessage = $"HTTP error: {response.StatusCode}",
                        LogLevel = LogLevel.Error
                    });
                    return false;
                }

                using (var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                }
            }

            progress?.Report(new TaskProgressDetail
            {
                Progress = 50,
                StatusText = "Installing OneDrive...",
                DetailedMessage = "Running OneDrive installer"
            });

            // Run the installer
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = installerPath;
                process.StartInfo.Arguments = "/silent";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                await Task.Run(() => process.WaitForExit(), cancellationToken);

                bool success = process.ExitCode == 0;

                progress?.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = success ? "OneDrive installed successfully" : "OneDrive installation failed",
                    DetailedMessage = $"Installer exited with code: {process.ExitCode}",
                    LogLevel = success ? LogLevel.Success : LogLevel.Error
                });

                return success;
            }
        }
        catch (Exception ex)
        {
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = "Error installing OneDrive",
                DetailedMessage = $"Exception: {ex.Message}",
                LogLevel = LogLevel.Error
            });
            return false;
        }
    }

    /// <summary>
    /// Gets a detailed message for the current installation phase.
    /// </summary>
    /// <param name="packageName">The name of the package being installed.</param>
    /// <param name="phase">The current installation phase.</param>
    /// <param name="progressValue">The current progress value.</param>
    /// <returns>A detailed message describing the current installation phase.</returns>
    private string GetDetailedMessageForPhase(string packageName, string phase, int progressValue)
    {
        switch (phase.ToLowerInvariant())
        {
            case "downloading":
                return $"Downloading {packageName} from the package source ({progressValue}% complete)";
            case "verifying":
                return $"Verifying package integrity for {packageName} ({progressValue}% complete)";
            case "installing":
                return $"Installing {packageName} files to your system ({progressValue}% complete)";
            case "completing":
                return $"Finalizing installation of {packageName} ({progressValue}% complete)";
            case "preparing":
            default:
                return $"Preparing to install {packageName} ({progressValue}% complete)";
        }
    }
    // SetExecutionPolicy is now handled by PowerShellFactory
}

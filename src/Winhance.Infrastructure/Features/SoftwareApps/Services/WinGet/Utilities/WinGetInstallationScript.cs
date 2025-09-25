using System;
using System.IO;
using System.IO.Compression;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;


namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities
{
    public static class WinGetInstallationScript
    {
        public static readonly string InstallScript =
            @"
# PowerShell script to install WinGet from GitHub
Write-Output ""Starting WinGet installation process... [PROGRESS:25]""

# Create a temporary directory for downloads
$tempDir = Join-Path $env:TEMP ""WinGetInstall""
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force
}
New-Item -Path $tempDir -ItemType Directory -Force | Out-Null

try {
    # Download URLs
    $dependenciesUrl = ""https://github.com/microsoft/winget-cli/releases/latest/download/DesktopAppInstaller_Dependencies.zip""
    $installerUrl = ""https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle""
    $licenseUrl = ""https://github.com/microsoft/winget-cli/releases/latest/download/e53e159d00e04f729cc2180cffd1c02e_License1.xml""
    
    # Download paths
    $dependenciesPath = Join-Path $tempDir ""DesktopAppInstaller_Dependencies.zip""
    $installerPath = Join-Path $tempDir ""Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle""
    $licensePath = Join-Path $tempDir ""e53e159d00e04f729cc2180cffd1c02e_License1.xml""
    
    # Download the dependencies
    Write-Output ""Downloading WinGet dependencies... [PROGRESS:30]""
    Invoke-WebRequest -Uri $dependenciesUrl -OutFile $dependenciesPath -UseBasicParsing
    
    # Download the installer
    Write-Output ""Downloading WinGet installer... [PROGRESS:40]""
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    
    # Download the license file (needed for LTSC editions)
    Write-Output ""Downloading WinGet license file... [PROGRESS:45]""
    Invoke-WebRequest -Uri $licenseUrl -OutFile $licensePath -UseBasicParsing
    
    # Extract the dependencies
    Write-Output ""Extracting dependencies... [PROGRESS:50]""
    $extractPath = Join-Path $tempDir ""Dependencies""
    Expand-Archive -Path $dependenciesPath -DestinationPath $extractPath -Force
    
    # Install all dependencies
    Write-Output ""Installing dependencies... [PROGRESS:60]""
    $dependencyFiles = Get-ChildItem -Path $extractPath -Filter *.appx -Recurse
    foreach ($file in $dependencyFiles) {
        Write-Output ""Installing dependency: $($file.Name)""
        try {
            Add-AppxPackage -Path $file.FullName
        }
        catch {
            Write-Output ""[ERROR] Failed to install dependency $($file.Name): $($_.Exception.Message)""
            Write-Output ""[ERROR] Error Type: $($_.Exception.GetType().Name)""
            if ($_.Exception.HResult) {
                Write-Output ""[ERROR] Error Code: 0x$($_.Exception.HResult.ToString('X8'))""
            }
            if ($_.Exception.InnerException) {
                Write-Output ""[ERROR] Inner Error: $($_.Exception.InnerException.Message)""
            }
            throw $_
        }
    }
    
    # Install the WinGet installer
    Write-Output ""Installing WinGet... [PROGRESS:80]""
    try {
        # Always use Add-AppxProvisionedPackage with license for all Windows editions
        Write-Output ""Installing WinGet with license file""
        Add-AppxProvisionedPackage -Online -PackagePath $installerPath -LicensePath $licensePath
        Write-Output ""WinGet installation completed successfully [PROGRESS:90]""
        
        # Refresh PATH environment variable to include WindowsApps directory where WinGet is installed
        Write-Output ""Refreshing PATH environment to include WinGet...""
        $env:Path = [System.Environment]::GetEnvironmentVariable(""Path"", ""Machine"") + "";"" + [System.Environment]::GetEnvironmentVariable(""Path"", ""User"")
        
        # Verify WinGet installation by running a command
        Write-Output ""Verifying WinGet installation...""
        
        # Try to find WinGet in common locations
        $wingetPaths = @(
            ""winget.exe"", # Check if it's in PATH
            ""$env:LOCALAPPDATA\Microsoft\WindowsApps\winget.exe"",
            ""$env:ProgramFiles\WindowsApps\Microsoft.DesktopAppInstaller_*\winget.exe""
        )
        
        $wingetFound = $false
        foreach ($path in $wingetPaths) {
            if ($path -like ""*`**"") {
                # Handle wildcard paths
                $resolvedPaths = Resolve-Path $path -ErrorAction SilentlyContinue
                if ($resolvedPaths) {
                    foreach ($resolvedPath in $resolvedPaths) {
                        if (Test-Path $resolvedPath) {
                            Write-Output ""Found WinGet at: $resolvedPath""
                            $wingetFound = $true
                            # Add the directory to PATH if not already there
                            $wingetDir = Split-Path $resolvedPath
                            if ($env:Path -notlike ""*$wingetDir*"") {
                                $env:Path += "";$wingetDir""
                            }
                            break
                        }
                    }
                }
            } else {
                # Handle direct paths
                if (Test-Path $path) {
                    Write-Output ""Found WinGet at: $path""
                    $wingetFound = $true
                    # Add the directory to PATH if not already there
                    $wingetDir = Split-Path $path
                    if ($env:Path -notlike ""*$wingetDir*"") {
                        $env:Path += "";$wingetDir""
                    }
                    break
                }
            }
        }
        
        if (-not $wingetFound) {
            # Try running winget command directly to see if it works
            try {
                $wingetVersion = & winget.exe --version 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Output ""WinGet is available in PATH: $wingetVersion""
                    $wingetFound = $true
                }
            } catch {
                Write-Output ""WinGet command not found in PATH""
            }
        }
        
        if (-not $wingetFound) {
            Write-Output ""[WARNING] WinGet was installed but could not be found in PATH. You may need to restart your system.""
        }
    }
    catch {
        Write-Output ""[ERROR] Failed to install WinGet: $($_.Exception.Message)""
        Write-Output ""[ERROR] Error Type: $($_.Exception.GetType().Name)""
        if ($_.Exception.HResult) {
            Write-Output ""[ERROR] Error Code: 0x$($_.Exception.HResult.ToString('X8'))""
        }
        if ($_.Exception.InnerException) {
            Write-Output ""[ERROR] Inner Error: $($_.Exception.InnerException.Message)""
        }
        throw $_
    }
}
catch {
    Write-Output ""[ERROR] An error occurred during WinGet installation: $($_.Exception.Message)""
    Write-Output ""[ERROR] Error Type: $($_.Exception.GetType().Name)""
    if ($_.Exception.HResult) {
        Write-Output ""[ERROR] Error Code: 0x$($_.Exception.HResult.ToString('X8'))""
    }
    if ($_.Exception.InnerException) {
        Write-Output ""[ERROR] Inner Error: $($_.Exception.InnerException.Message)""
    }
    Write-Output ""[ERROR] TROUBLESHOOTING: Ensure you have internet access and are running as Administrator""
    throw $_
}
finally {
    # Clean up
    Write-Output ""Cleaning up temporary files... [PROGRESS:95]""
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force
    }
    Write-Output ""WinGet installation process completed [PROGRESS:100]""
}
";

        /// <summary>
        /// Installs WinGet by downloading it directly from GitHub and installing it.
        /// </summary>
        /// <param name=""progress"">Optional progress reporting.</param>
        /// <param name=""logger"">Optional logger for logging the installation process.</param>
        /// <param name=""cancellationToken"">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with a result indicating success or failure.</returns>
        public static async Task<(bool Success, string Message)> InstallWinGetAsync(
            IProgress<TaskProgressDetail> progress = null,
            ILogService logger = null,
            CancellationToken cancellationToken = default
        )
        {
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 10,
                    StatusText = "Preparing to download WinGet from GitHub...",
                    DetailedMessage =
                        "This may take a few minutes depending on your internet connection.",
                }
            );

            logger?.LogInformation("Starting WinGet installation by downloading from GitHub");

            // Create a temporary directory if it doesn't exist
            string tempDir = Path.Combine(Path.GetTempPath(), "WinhanceTemp");
            Directory.CreateDirectory(tempDir);

            // Create a temporary script file
            string scriptPath = Path.Combine(tempDir, $"WinGetInstall_{Guid.NewGuid()}.ps1");

            // Write the installation script to the file
            File.WriteAllText(scriptPath, InstallScript);

            try
            {
                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 20,
                        StatusText = "Downloading and installing WinGet components...",
                        DetailedMessage =
                            "This may take a few minutes depending on your internet connection.",
                    }
                );

                // Execute the PowerShell script with elevated privileges
                // Create Windows PowerShell 5.1 instance
                var sessionState = InitialSessionState.CreateDefault();
                using var runspace = RunspaceFactory.CreateRunspace(sessionState);
                runspace.Open();
                
                using (var powerShell = PowerShell.Create())
                {
                    powerShell.Runspace = runspace;
                    // Add the script to execute
                    powerShell.AddScript($". '{scriptPath}'");

                    // Set up real-time output processing using PowerShell events
                    var outputBuilder = new StringBuilder();

                    // Subscribe to the DataAdded event for Information stream
                    powerShell.Streams.Information.DataAdded += (sender, e) =>
                    {
                        var streamObjectsReceived = sender as PSDataCollection<InformationRecord>;
                        if (streamObjectsReceived != null)
                        {
                            var informationRecord = streamObjectsReceived[e.Index];
                            string output = informationRecord.MessageData.ToString();
                            outputBuilder.AppendLine(output);
                            logger?.LogInformation($"WinGet installation: {output}");

                            ProcessOutputLine(output, progress, logger);
                        }
                    };

                    // Subscribe to the DataAdded event for Output stream
                    var outputCollection = new PSDataCollection<PSObject>();
                    outputCollection.DataAdded += (sender, e) =>
                    {
                        var streamObjectsReceived = sender as PSDataCollection<PSObject>;
                        if (streamObjectsReceived != null)
                        {
                            var outputObject = streamObjectsReceived[e.Index];
                            string output = outputObject.ToString();
                            outputBuilder.AppendLine(output);
                            logger?.LogInformation($"WinGet installation: {output}");

                            ProcessOutputLine(output, progress, logger);
                        }
                    };

                    // Execute the script asynchronously to capture real-time output
                    await Task.Run(
                        () => powerShell.Invoke(null, outputCollection),
                        cancellationToken
                    );

                    // Helper method to process output lines for progress and error markers
                    void ProcessOutputLine(
                        string output,
                        IProgress<TaskProgressDetail> progress,
                        ILogService logger
                    )
                    {
                        // Check for progress markers in the output
                        if (output.Contains("[PROGRESS:"))
                        {
                            var progressMatch = System.Text.RegularExpressions.Regex.Match(
                                output,
                                @"\[PROGRESS:(\d+)\]"
                            );
                            if (
                                progressMatch.Success
                                && int.TryParse(
                                    progressMatch.Groups[1].Value,
                                    out int progressValue
                                )
                            )
                            {
                                progress?.Report(
                                    new TaskProgressDetail
                                    {
                                        Progress = progressValue,
                                        StatusText = output.Replace(progressMatch.Value, "").Trim(),
                                    }
                                );
                            }
                        }
                        // Check for error markers in the output
                        else if (output.Contains("[ERROR]"))
                        {
                            var errorMessage = output.Replace("[ERROR]", "").Trim();
                            logger?.LogError($"WinGet installation error: {errorMessage}");
                        }
                    }

                    // Check for errors
                    if (powerShell.HadErrors)
                    {
                        var errorBuilder = new StringBuilder("Errors during WinGet installation:");
                        foreach (var error in powerShell.Streams.Error)
                        {
                            errorBuilder.AppendLine(error.Exception.Message);
                            logger?.LogError(
                                $"WinGet installation error: {error.Exception.Message}"
                            );
                        }

                        progress?.Report(
                            new TaskProgressDetail
                            {
                                Progress = 100,
                                StatusText = "WinGet installation failed",
                                DetailedMessage = errorBuilder.ToString(),
                                LogLevel = Winhance.Core.Features.Common.Enums.LogLevel.Error,
                            }
                        );

                        return (false, errorBuilder.ToString());
                    }

                    // Report success
                    progress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = 100,
                            StatusText = "WinGet installation completed successfully",
                            DetailedMessage = "WinGet has been installed and is ready to use.",
                        }
                    );

                    logger?.LogInformation("WinGet installation completed successfully");
                    return (true, "WinGet has been successfully installed.");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error installing WinGet: {ex.Message}";
                logger?.LogError(errorMessage, ex);

                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 100,
                        StatusText = "WinGet installation failed",
                        DetailedMessage = errorMessage,
                        LogLevel = Winhance.Core.Features.Common.Enums.LogLevel.Error,
                    }
                );

                return (false, errorMessage);
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
                    logger?.LogWarning($"Failed to delete temporary script file: {ex.Message}");
                }
            }
        }
    }
}

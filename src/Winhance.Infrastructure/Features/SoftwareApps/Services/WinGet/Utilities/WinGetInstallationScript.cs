// This file is not used by the codebase but is being kept for future development purposes
// when it might be needed in the Winhancements.ps1/autounattend.xml file

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;


namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities
{
    public static class WinGetInstallationScript
    {
        public static readonly string InstallScript = @"
# Check if script is running as Administrator
If (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]""Administrator"")) {
    Try {
        Start-Process PowerShell.exe -ArgumentList (""-NoProfile -ExecutionPolicy Bypass -File `""{0}`"""" -f $PSCommandPath) -Verb RunAs
        Exit
    }
    Catch {
        Write-Host ""Failed to run as Administrator. Please rerun with elevated privileges.""
        Exit
    }
}

# Setup logging
$logFolder = ""C:\ProgramData\Winhance\Logs""
$logFile = ""$logFolder\WinGetInstallationLog.txt""

# Create log directory if it doesn't exist
if (!(Test-Path $logFolder)) {
    New-Item -ItemType Directory -Path $logFolder -Force | Out-Null
}

# Function to write to log file
function Write-Log {
    param (
        [string]$Message
    )
    
    # Check if log file exists and is over 500KB (512000 bytes)
    if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 512000) {
        Remove-Item $logFile -Force -ErrorAction SilentlyContinue
        $timestamp = Get-Date -Format ""yyyy-MM-dd HH:mm:ss""
        ""$timestamp - Log rotated - previous log exceeded 500KB"" | Out-File -FilePath $logFile
    }
    
    $timestamp = Get-Date -Format ""yyyy-MM-dd HH:mm:ss""
    ""$timestamp - $Message"" | Out-File -FilePath $logFile -Append
    
    # Also output to console for real-time progress
    Write-Host $Message
}

# Function to download files with progress tracking
function Get-FileFromWeb {
    param (
        [Parameter(Mandatory)][string]$URL,
        [Parameter(Mandatory)][string]$File
    )
    
    function Show-Progress {
        param (
            [Parameter(Mandatory)][Single]$TotalValue,
            [Parameter(Mandatory)][Single]$CurrentValue,
            [Parameter(Mandatory)][string]$ProgressText,
            [Parameter()][int]$BarSize = 20,
            [Parameter()][switch]$Complete
        )
        
        $percent = $CurrentValue / $TotalValue
        $percentComplete = $percent * 100
        $progressBar = $(''.PadRight($BarSize * $percent, '#').PadRight($BarSize, '-'))
        $progressMessage = ""$ProgressText [$progressBar] $($percentComplete.ToString('##0.0').PadLeft(5))%""
        
        # Use Write-Host for real-time progress (gets captured by PowerShellRunner)
        Write-Host $progressMessage
        
        # Log completion to file
        if ($Complete -or $percentComplete -ge 100) {
            Write-Host """" # New line after progress bar
            Write-Log ""$ProgressText completed (100%)""
        }
    }
    
    try {
        $fileName = [System.IO.Path]::GetFileName($File)
        Write-Log ""Starting download: $fileName from $URL""
        
        $request = [System.Net.HttpWebRequest]::Create($URL)
        $response = $request.GetResponse()
        
        if ($response.StatusCode -eq 401 -or $response.StatusCode -eq 403 -or $response.StatusCode -eq 404) {
            throw ""Remote file either doesn't exist, is unauthorized, or is forbidden for '$URL'.""
        }
        
        if ($File -match '^\\.(\\|\\/)')  {
            $File = Join-Path (Get-Location -PSProvider 'FileSystem') ($File -Split '^\\.(\\|\\/)')
        }
        
        if ($File -and !(Split-Path $File)) {
            $File = Join-Path (Get-Location -PSProvider 'FileSystem') $File
        }
        
        if ($File) {
            $fileDirectory = $([System.IO.Path]::GetDirectoryName($File))
            if (!(Test-Path($fileDirectory))) {
                [System.IO.Directory]::CreateDirectory($fileDirectory) | Out-Null
            }
        }
        
        [long]$fullSize = $response.ContentLength
        [byte[]]$buffer = New-Object byte[] 1048576  # 1MB buffer
        [long]$total = [long]$count = 0
        
        $reader = $response.GetResponseStream()
        $writer = New-Object System.IO.FileStream $File, 'Create'
        
        $lastProgressTime = Get-Date
        
        do {
            $count = $reader.Read($buffer, 0, $buffer.Length)
            $writer.Write($buffer, 0, $count)
            $total += $count
            
            # Update progress every 500ms to avoid too frequent updates
            $now = Get-Date
            if ($fullSize -gt 0 -and ($now - $lastProgressTime).TotalMilliseconds -gt 500) {
                Show-Progress -TotalValue $fullSize -CurrentValue $total -ProgressText ""Downloading $fileName""
                $lastProgressTime = $now
            }
        } while ($count -gt 0)
        
        # Show final progress
        if ($fullSize -gt 0) {
            Show-Progress -TotalValue $fullSize -CurrentValue $total -ProgressText ""Downloading $fileName"" -Complete
        }
        
        Write-Log ""Successfully downloaded: $fileName ($total bytes)""
    }
    catch {
        Write-Log ""Failed to download $fileName : $($_.Exception.Message)""
        throw
    }
    finally {
        if ($reader) { $reader.Close() }
        if ($writer) { $writer.Close() }
    }
}

$tempDir = Join-Path $env:TEMP ""WinGetInstall""
if (Test-Path $tempDir) { Remove-Item -Path $tempDir -Recurse -Force }
New-Item -Path $tempDir -ItemType Directory -Force | Out-Null

Write-Log ""Starting WinGet installation...""

try {
    $baseUrl = ""https://github.com/microsoft/winget-cli/releases/latest/download""
    $files = @{
        Dependencies = ""$baseUrl/DesktopAppInstaller_Dependencies.zip""
        Installer = ""$baseUrl/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle""
        License = ""$baseUrl/e53e159d00e04f729cc2180cffd1c02e_License1.xml""
    }
    
    Write-Log ""Downloading WinGet components from GitHub...""
    $paths = @{}
    
    foreach ($key in $files.Keys) {
        $paths[$key] = Join-Path $tempDir (Split-Path $files[$key] -Leaf)
        Write-Log ""Starting $key download...""
        try {
            Get-FileFromWeb -URL $files[$key] -File $paths[$key]
            Write-Log ""$key download completed""
        }
        catch {
            Write-Log ""Failed to download $key : $($_.Exception.Message)""
            throw
        }
    }
    
    Write-Log ""Extracting dependencies...""
    $extractPath = Join-Path $tempDir ""Dependencies""
    try {
        Expand-Archive -Path $paths.Dependencies -DestinationPath $extractPath -Force
        Write-Log ""Dependencies extracted successfully""
    }
    catch {
        Write-Log ""Failed to extract dependencies: $($_.Exception.Message)""
        throw
    }
    
    Write-Log ""Installing dependencies...""
    $dependencyFiles = Get-ChildItem -Path $extractPath -Filter *.appx -Recurse
    Write-Log ""Found $($dependencyFiles.Count) dependency files to install""
    
    foreach ($file in $dependencyFiles) {
        try {
            Write-Log ""Installing dependency: $($file.Name)""
            Add-AppxPackage -Path $file.FullName
            Write-Log ""Successfully installed: $($file.Name)""
        }
        catch {
            Write-Log ""Failed to install $($file.Name): $($_.Exception.Message)""
            throw
        }
    }
    
    Write-Log ""Installing WinGet...""
    try {
        Add-AppxProvisionedPackage -Online -PackagePath $paths.Installer -LicensePath $paths.License
        Write-Log ""WinGet installation completed successfully""
    }
    catch {
        Write-Log ""Failed to install WinGet: $($_.Exception.Message)""
        throw
    }
}
catch {
    Write-Log ""Error during WinGet installation: $($_.Exception.Message)""
    Write-Log ""Installation failed - check log for details""
    throw
}
finally {
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force
        Write-Log ""Cleaned up temporary files""
    }
}
";

        public static async Task<(bool Success, string Message)> InstallWinGetAsync(
            IProgress<TaskProgressDetail>? progress = null,
            ILogService? logger = null,
            CancellationToken cancellationToken = default
        )
        {
            logger?.LogInformation("Starting WinGet installation from GitHub");

            string tempDir = Path.Combine(Path.GetTempPath(), "WinhanceTemp");
            Directory.CreateDirectory(tempDir);
            string scriptPath = Path.Combine(tempDir, $"WinGetInstall_{Guid.NewGuid()}.ps1");
            File.WriteAllText(scriptPath, InstallScript);

            try
            {
                await Winhance.Infrastructure.Features.Common.Utilities.PowerShellRunner.RunScriptFileAsync(
                    scriptPath, progress: progress, ct: cancellationToken);

                logger?.LogInformation("WinGet installation completed successfully");
                return (true, "WinGet installed successfully");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error installing WinGet: {ex.Message}";
                logger?.LogError(errorMessage, ex);
                return (false, errorMessage);
            }
            finally
            {
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

        private static void ProcessOutputLine(string output, IProgress<TaskProgressDetail> progress)
        {
            if (string.IsNullOrEmpty(output)) return;

            // Check for progress markers in the output
            var progressMatch = Regex.Match(output, @"\[PROGRESS:(\d+)\]");
            if (progressMatch.Success && int.TryParse(progressMatch.Groups[1].Value, out int progressValue))
            {
                progress?.Report(new TaskProgressDetail
                {
                    Progress = progressValue,
                    StatusText = output.Replace(progressMatch.Value, "").Trim(),
                });
            }
        }
    }
}

using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Utilities
{
    /// <summary>
    /// Factory for creating PowerShell instances with specific runtime configurations.
    /// </summary>
    public static class PowerShellFactory
    {
        private static readonly string WindowsPowerShellPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
        private static readonly string PowerShellCorePath = @"C:\Program Files\PowerShell\7\pwsh.exe";
        
        // Reference to the system service - will be set by the first call to CreateWindowsPowerShell
        private static ISystemServices _systemServices;
        
        /// <summary>
        /// Determines if the current OS is Windows 10 (which has issues with Appx module in PowerShell Core)
        /// </summary>
        /// <returns>True if running on Windows 10, false otherwise</returns>
        private static bool IsWindows10()
        {
            // Use the system services if available
            if (_systemServices != null)
            {
                return !_systemServices.IsWindows11();
            }
            
            // Fallback to direct OS version check
            try
            {
                // Get OS version information
                var osVersion = Environment.OSVersion;
                
                // Windows 10 has major version 10 and build number less than 22000
                // Windows 11 has build number 22000 or higher
                bool isWin10ByVersion = osVersion.Platform == PlatformID.Win32NT &&
                       osVersion.Version.Major == 10 &&
                       osVersion.Version.Build < 22000;
                
                // Additional check using ProductName which is more reliable
                bool isWin10ByProductName = false;
                try
                {
                    // Check the product name from registry
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        if (key != null)
                        {
                            var productName = key.GetValue("ProductName") as string;
                            isWin10ByProductName = productName != null && productName.Contains("Windows 10");
                        }
                    }
                }
                catch
                {
                    // If registry check fails, rely on version check only
                }
                
                // Return true if either method indicates Windows 10
                return isWin10ByVersion || isWin10ByProductName;
            }
            catch
            {
                // If there's any error, assume it's Windows 10 to ensure compatibility
                // This is safer than assuming it's not Windows 10
                return true;
            }
        }
        
        /// <summary>
        /// Creates a PowerShell instance configured to use Windows PowerShell 5.1.
        /// On Windows 10, it ensures compatibility with the Appx module by using Windows PowerShell.
        /// </summary>
        /// <param name="logService">Optional log service for diagnostic information.</param>
        /// <returns>A PowerShell instance configured to use Windows PowerShell 5.1.</returns>
        public static PowerShell CreateWindowsPowerShell(ILogService logService = null, ISystemServices systemServices = null)
        {
            try
            {
                // Store the system services reference if provided
                if (systemServices != null)
                {
                    _systemServices = systemServices;
                }
                
                PowerShell powerShell;
                
                // Create a default PowerShell instance
                powerShell = PowerShell.Create();
                
                // Check if we're running on Windows 10
                bool isWin10 = IsWindows10();
                
                if (isWin10)
                {
                    logService?.LogInformation("Detected Windows 10 - Using direct Windows PowerShell execution for Appx commands");
                    
                    // On Windows 10, immediately set up direct execution for Appx commands
                    // This avoids WinRM connection issues and ensures compatibility
                    powerShell.AddScript($@"
                        function Invoke-WindowsPowerShell {{
                            param(
                                [Parameter(Mandatory=$true)]
                                [string]$Command
                            )
                            
                            try {{
                                $psi = New-Object System.Diagnostics.ProcessStartInfo
                                $psi.FileName = '{WindowsPowerShellPath}'
                                $psi.Arguments = ""-NoProfile -ExecutionPolicy Bypass -Command `""$Command`""""
                                $psi.RedirectStandardOutput = $true
                                $psi.RedirectStandardError = $true
                                $psi.UseShellExecute = $false
                                $psi.CreateNoWindow = $true
                                
                                $process = New-Object System.Diagnostics.Process
                                $process.StartInfo = $psi
                                $process.Start() | Out-Null
                                
                                $output = $process.StandardOutput.ReadToEnd()
                                $error = $process.StandardError.ReadToEnd()
                                $process.WaitForExit()
                                
                                if ($error) {{
                                    Write-Warning ""Windows PowerShell error: $error""
                                }}
                                
                                return $output
                            }} catch {{
                                Write-Warning ""Error invoking Windows PowerShell: $_""
                                return $null
                            }}
                        }}
                        
                        # Override Get-AppxPackage to use Windows PowerShell directly
                        function Get-AppxPackage {{
                            param(
                                [Parameter(Position=0)]
                                [string]$Name = '*'
                            )
                            
                            Write-Output ""Using direct Windows PowerShell execution for Get-AppxPackage""
                            $result = Invoke-WindowsPowerShell ""Get-AppxPackage -Name '$Name' | ConvertTo-Json -Depth 5 -Compress""
                            if ($result) {{
                                try {{
                                    $packages = $result | ConvertFrom-Json -ErrorAction SilentlyContinue
                                    return $packages
                                }} catch {{
                                    Write-Warning ""Error parsing AppX package results: $_""
                                    return $null
                                }}
                            }}
                            return $null
                        }}
                        
                        # Override Remove-AppxPackage to use Windows PowerShell directly
                        function Remove-AppxPackage {{
                            param(
                                [Parameter(Mandatory=$true)]
                                [string]$Package
                            )
                            
                            Write-Output ""Using direct Windows PowerShell execution for Remove-AppxPackage""
                            $result = Invoke-WindowsPowerShell ""Remove-AppxPackage -Package '$Package'""
                            return $result
                        }}
                        
                        # Override Get-AppxProvisionedPackage to use Windows PowerShell directly
                        function Get-AppxProvisionedPackage {{
                            param(
                                [Parameter(Mandatory=$true)]
                                [switch]$Online
                            )
                            
                            Write-Output ""Using direct Windows PowerShell execution for Get-AppxProvisionedPackage""
                            $result = Invoke-WindowsPowerShell ""Get-AppxProvisionedPackage -Online | ConvertTo-Json -Depth 5 -Compress""
                            if ($result) {{
                                try {{
                                    $packages = $result | ConvertFrom-Json -ErrorAction SilentlyContinue
                                    return $packages
                                }} catch {{
                                    Write-Warning ""Error parsing provisioned package results: $_""
                                    return $null
                                }}
                            }}
                            return $null
                        }}
                        
                        # Override Remove-AppxProvisionedPackage to use Windows PowerShell directly
                        function Remove-AppxProvisionedPackage {{
                            param(
                                [Parameter(Mandatory=$true)]
                                [switch]$Online,
                                
                                [Parameter(Mandatory=$true)]
                                [string]$PackageName
                            )
                            
                            Write-Output ""Using direct Windows PowerShell execution for Remove-AppxProvisionedPackage""
                            $result = Invoke-WindowsPowerShell ""Remove-AppxProvisionedPackage -Online -PackageName '$PackageName'""
                            return $result
                        }}
                        
                        Write-Output ""Configured Windows PowerShell direct execution for Appx commands""
                    ");
                    
                    var directExecutionResults = powerShell.Invoke();
                    foreach (var result in directExecutionResults)
                    {
                        logService?.LogInformation($"Direct execution setup: {result}");
                    }
                }
                else
                {
                    logService?.LogInformation("Not running on Windows 10 - Using standard PowerShell Core for Appx commands");
                }
                
                // Configure PowerShell to use Windows PowerShell modules and set execution policy
                powerShell.Commands.Clear();
                powerShell.AddScript(@"
                    # Set execution policy
                    try {
                        Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force -ErrorAction SilentlyContinue
                    } catch {
                        # Ignore errors
                    }
                    
                    # Ensure Windows PowerShell modules are available
                    $WindowsPowerShellModulePath = ""$env:SystemRoot\System32\WindowsPowerShell\v1.0\Modules""
                    if ($env:PSModulePath -notlike ""*$WindowsPowerShellModulePath*"") {
                        $env:PSModulePath = $env:PSModulePath + "";"" + $WindowsPowerShellModulePath
                    }
                    
                    # Only try to import Appx module on non-Windows 10 systems
                    # On Windows 10, we're using direct execution instead
                    if (-not $($PSVersionTable.OS -like '*10.0*' -or $env:OS -like '*Windows_NT*10.0*')) {
                        try {
                            Import-Module Appx -ErrorAction SilentlyContinue
                        } catch {
                            # Module might not be available, continue anyway
                        }
                    }
                    
                    # Log PowerShell version for diagnostics
                    $PSVersionInfo = $PSVersionTable.PSVersion
                    Write-Output ""Using PowerShell version: $($PSVersionInfo.Major).$($PSVersionInfo.Minor) ($($PSVersionTable.PSEdition))""
                    Write-Output ""OS Version: $([System.Environment]::OSVersion.Version)""
                ");
                
                var results = powerShell.Invoke();
                foreach (var result in results)
                {
                    logService?.LogInformation($"PowerShell initialization: {result}");
                }
                
                powerShell.Commands.Clear();
                
                return powerShell;
            }
            catch (Exception ex)
            {
                logService?.LogError($"Error creating Windows PowerShell instance: {ex.Message}", ex);
                
                // Fall back to default PowerShell instance if creation fails
                return PowerShell.Create();
            }
        }
        
        /// <summary>
        /// Creates a PowerShell instance with the default configuration.
        /// On Windows 10, it will use the same compatibility approach as CreateWindowsPowerShell.
        /// </summary>
        /// <returns>A default PowerShell instance.</returns>
        public static PowerShell CreateDefault()
        {
            // Use the same Windows 10 compatibility approach as CreateWindowsPowerShell
            return CreateWindowsPowerShell();
        }
        
        /// <summary>
        /// Creates a PowerShell instance for executing Appx-related commands.
        /// This method always uses Windows PowerShell 5.1 on Windows 10 for compatibility.
        /// </summary>
        /// <param name="logService">Optional log service for diagnostic information.</param>
        /// <param name="systemServices">Optional system services for OS detection.</param>
        /// <returns>A PowerShell instance configured for Appx commands.</returns>
        public static PowerShell CreateForAppxCommands(ILogService logService = null, ISystemServices systemServices = null)
        {
            // Always use Windows PowerShell for Appx commands on Windows 10
            return CreateWindowsPowerShell(logService, systemServices);
        }
    }
}
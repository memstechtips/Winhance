using System;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;


namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service that handles verification of application installations.
/// </summary>
public class AppVerificationService : IAppVerificationService
{
    private readonly ILogService _logService;
    private readonly IPowerShellDetectionService _powerShellDetectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppVerificationService"/> class.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    /// <param name="powerShellDetectionService">The PowerShell detection service.</param>
    public AppVerificationService(ILogService logService, IPowerShellDetectionService powerShellDetectionService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _powerShellDetectionService = powerShellDetectionService ?? throw new ArgumentNullException(nameof(powerShellDetectionService));
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyAppInstallationAsync(string packageName)
    {
        try
        {
            // Create PowerShell instance
            using var powerShell = PowerShell.Create();

            // For Microsoft Store apps (package IDs that are alphanumeric with no dots)
            if (packageName.All(char.IsLetterOrDigit) && !packageName.Contains('.'))
            {
                // Use Get-AppxPackage to check if the Microsoft Store app is installed
                // Ensure we're using Windows PowerShell for Appx commands
                using var appxPowerShell = PowerShell.Create();
                appxPowerShell.AddScript(
                    $"Get-AppxPackage | Where-Object {{ $_.PackageFullName -like '*{packageName}*' }}"
                );
                var result = await appxPowerShell.InvokeAsync();

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
                    # Ensure we're using Windows PowerShell for Winget operations
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
        catch (Exception ex)
        {
            _logService.LogError($"Error verifying app installation: {ex.Message}", ex);
            // If any error occurs during verification, assume the app is not installed
            return false;
        }
    }
}

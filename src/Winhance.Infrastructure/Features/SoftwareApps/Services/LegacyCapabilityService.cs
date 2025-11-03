using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class LegacyCapabilityService(
    ILogService logService,
    IPowerShellExecutionService powerShellExecutionService) : ILegacyCapabilityService
{
    public async Task<bool> EnableCapabilityAsync(string capabilityName, string displayName = null, CancellationToken cancellationToken = default)
    {
        displayName ??= capabilityName;

        try
        {
            var script = $@"
                try {{
                    Write-Host 'Enabling Windows Capability: {displayName}. This may take a while, please wait...' -ForegroundColor Yellow

                    Write-Host 'Searching for capability...' -ForegroundColor Gray
                    $capabilities = Get-WindowsCapability -Online | Where-Object {{ $_.Name -like '{capabilityName}*' }}

                    if ($capabilities.Count -eq 0) {{
                        Write-Host ""Capability '{capabilityName}' not found on this system."" -ForegroundColor Red
                        Write-Host ""Available capabilities containing '{capabilityName}':"" -ForegroundColor Yellow
                        Get-WindowsCapability -Online | Where-Object {{ $_.Name -like ""*{capabilityName}*"" }} |
                            ForEach-Object {{ Write-Host ""  - $($_.Name)"" -ForegroundColor Cyan }}
                        Write-Host 'Press any key to close...' -ForegroundColor Gray
                        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                        return
                    }}

                    $capability = $capabilities | Select-Object -First 1
                    Write-Host ""Capability found: $($capability.Name)"" -ForegroundColor Gray
                    Write-Host ""Current state: $($capability.State)"" -ForegroundColor Gray

                    if ($capability.State -eq 'Installed') {{
                        Write-Host 'Capability is already enabled.' -ForegroundColor Green
                        Write-Host 'Press any key to close...' -ForegroundColor Gray
                        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                        return
                    }}

                    Write-Host 'Attempting to install capability...' -ForegroundColor Gray
                    Add-WindowsCapability -Online -Name $capability.Name
                    Write-Host 'Installation completed successfully!' -ForegroundColor Green
                    Write-Host 'Press any key to close...' -ForegroundColor Gray
                    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                }} catch {{
                    Write-Host '=== CAPABILITY INSTALLATION FAILED ===' -ForegroundColor Red
                    Write-Host 'Capability: {capabilityName}' -ForegroundColor Yellow
                    Write-Host 'Operation: Add-WindowsCapability' -ForegroundColor Yellow
                    Write-Host ('Error: ' + $_.Exception.Message) -ForegroundColor Yellow
                    Write-Host ('Type: ' + $_.Exception.GetType().Name) -ForegroundColor Yellow

                    if ($_.Exception.InnerException) {{
                        Write-Host ('Inner Error: ' + $_.Exception.InnerException.Message) -ForegroundColor Cyan
                    }}

                    if ($_.Exception.HResult) {{
                        Write-Host ('Error Code: 0x' + $_.Exception.HResult.ToString('X8')) -ForegroundColor Cyan
                    }}

                    Write-Host '' -ForegroundColor Gray
                    Write-Host 'TROUBLESHOOTING HINTS:' -ForegroundColor Magenta
                    Write-Host '- Restart your computer and try again' -ForegroundColor Gray
                    Write-Host '- Ensure PowerShell is running as Administrator' -ForegroundColor Gray
                    Write-Host '- Check internet connection for online packages' -ForegroundColor Gray
                    Write-Host '- Verify Windows Update service is running' -ForegroundColor Gray
                    Write-Host '- Try: DISM /Online /Cleanup-Image /RestoreHealth' -ForegroundColor Gray
                    Write-Host '=====================================' -ForegroundColor Red
                    Write-Host 'Press any key to close...' -ForegroundColor Gray
                    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                }}";

            var launched = await powerShellExecutionService.ExecuteScriptVisibleAsync(script);

            if (!launched)
            {
                logService?.LogError($"Failed to launch PowerShell for capability {capabilityName}");
            }

            return launched;
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error launching PowerShell for capability {capabilityName}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DisableCapabilityAsync(string capabilityName, string displayName = null, CancellationToken cancellationToken = default)
    {
        displayName ??= capabilityName;

        try
        {
            var script = $@"
                try {{
                    Write-Host 'Winhance - Disabling Windows Capability: {displayName}. This may take a few minutes...' -ForegroundColor Yellow

                    Write-Host 'Searching for capability...' -ForegroundColor Gray
                    $capabilities = Get-WindowsCapability -Online | Where-Object {{ $_.Name -like '{capabilityName}*' }}

                    if ($capabilities.Count -eq 0) {{
                        Write-Host ""Capability '{capabilityName}' not found on this system."" -ForegroundColor Red
                        Write-Host ""Available capabilities containing '{capabilityName}':"" -ForegroundColor Yellow
                        Get-WindowsCapability -Online | Where-Object {{ $_.Name -like ""*{capabilityName}*"" }} |
                            ForEach-Object {{ Write-Host ""  - $($_.Name)"" -ForegroundColor Cyan }}
                        Write-Host 'Press any key to close...' -ForegroundColor Gray
                        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                        return
                    }}

                    $capability = $capabilities | Select-Object -First 1
                    Write-Host ""Capability found: $($capability.Name)"" -ForegroundColor Gray
                    Write-Host ""Current state: $($capability.State)"" -ForegroundColor Gray

                    if ($capability.State -ne 'Installed') {{
                        Write-Host 'Capability is already disabled.' -ForegroundColor Green
                        Write-Host 'Press any key to close...' -ForegroundColor Gray
                        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                        return
                    }}

                    Write-Host 'Attempting to remove capability...' -ForegroundColor Gray
                    Remove-WindowsCapability -Online -Name $capability.Name
                    Write-Host 'Removal completed successfully!' -ForegroundColor Green
                    Write-Host 'Press any key to close...' -ForegroundColor Gray
                    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                }} catch {{
                    Write-Host '=== CAPABILITY REMOVAL FAILED ===' -ForegroundColor Red
                    Write-Host 'Capability: {capabilityName}' -ForegroundColor Yellow
                    Write-Host 'Operation: Remove-WindowsCapability' -ForegroundColor Yellow
                    Write-Host ('Error: ' + $_.Exception.Message) -ForegroundColor Yellow
                    Write-Host ('Type: ' + $_.Exception.GetType().Name) -ForegroundColor Yellow

                    if ($_.Exception.InnerException) {{
                        Write-Host ('Inner Error: ' + $_.Exception.InnerException.Message) -ForegroundColor Cyan
                    }}

                    if ($_.Exception.HResult) {{
                        Write-Host ('Error Code: 0x' + $_.Exception.HResult.ToString('X8')) -ForegroundColor Cyan
                    }}

                    Write-Host '' -ForegroundColor Gray
                    Write-Host 'TROUBLESHOOTING HINTS:' -ForegroundColor Magenta
                    Write-Host '- Restart your computer and try again' -ForegroundColor Gray
                    Write-Host '- Ensure PowerShell is running as Administrator' -ForegroundColor Gray
                    Write-Host '- Check if capability has dependencies that prevent removal' -ForegroundColor Gray
                    Write-Host '- Verify system files: sfc /scannow' -ForegroundColor Gray
                    Write-Host '- Try: DISM /Online /Cleanup-Image /RestoreHealth' -ForegroundColor Gray
                    Write-Host '==================================' -ForegroundColor Red
                    Write-Host 'Press any key to close...' -ForegroundColor Gray
                    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                }}";

            var launched = await powerShellExecutionService.ExecuteScriptVisibleAsync(script);

            if (!launched)
            {
                logService?.LogError($"Failed to launch PowerShell for capability {capabilityName}");
            }

            return launched;
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error launching PowerShell for capability {capabilityName}: {ex.Message}");
            return false;
        }
    }


}
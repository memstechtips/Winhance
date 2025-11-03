using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class OptionalFeatureService(
    ILogService logService,
    IPowerShellExecutionService powerShellExecutionService) : IOptionalFeatureService
{
    public async Task<bool> EnableFeatureAsync(string featureName, string displayName = null, CancellationToken cancellationToken = default)
    {
        displayName ??= featureName;

        try
        {
            var script = $@"
                try {{
                    Write-Host 'Enabling Windows Optional Feature: {displayName}. This may take a while, please wait...' -ForegroundColor Yellow

                    Write-Host 'Checking if feature exists...' -ForegroundColor Gray
                    $feature = Get-WindowsOptionalFeature -Online -FeatureName '{featureName}'

                    if (-not $feature) {{
                        Write-Host ""Feature '{featureName}' not found on this system."" -ForegroundColor Red
                        Write-Host ""Available features containing '{featureName}':"" -ForegroundColor Yellow
                        Get-WindowsOptionalFeature -Online | Where-Object {{ $_.FeatureName -like ""*{featureName}*"" }} |
                            ForEach-Object {{ Write-Host ""  - $($_.FeatureName)"" -ForegroundColor Cyan }}
                        Write-Host 'Press any key to close...' -ForegroundColor Gray
                        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                        return
                    }}

                    Write-Host ""Feature found. Current state: $($feature.State)"" -ForegroundColor Gray

                    if ($feature.State -eq 'Enabled') {{
                        Write-Host 'Feature is already enabled.' -ForegroundColor Green
                        Write-Host 'Press any key to close...' -ForegroundColor Gray
                        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                        return
                    }}

                    Write-Host 'Attempting to enable feature...' -ForegroundColor Gray
                    $result = Enable-WindowsOptionalFeature -Online -FeatureName '{featureName}' -NoRestart

                    if ($result.RestartNeeded) {{
                        Write-Host 'Feature enabled successfully (restart required)!' -ForegroundColor Green
                    }} else {{
                        Write-Host 'Feature enabled successfully!' -ForegroundColor Green
                    }}
                    Write-Host 'Press any key to close...' -ForegroundColor Gray
                    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                }} catch {{
                    Write-Host '=== FEATURE ENABLEMENT FAILED ===' -ForegroundColor Red
                    Write-Host 'Feature: {featureName}' -ForegroundColor Yellow
                    Write-Host 'Operation: Enable-WindowsOptionalFeature' -ForegroundColor Yellow
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
                    Write-Host '- Check Windows Update status' -ForegroundColor Gray
                    Write-Host '- Verify system files: sfc /scannow' -ForegroundColor Gray
                    Write-Host '- Try: DISM /Online /Cleanup-Image /RestoreHealth' -ForegroundColor Gray
                    Write-Host '=================================' -ForegroundColor Red
                    Write-Host 'Press any key to close...' -ForegroundColor Gray
                    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                }}";

            var launched = await powerShellExecutionService.ExecuteScriptVisibleAsync(script);

            if (!launched)
            {
                logService?.LogError($"Failed to launch PowerShell for feature {featureName}");
            }

            return launched;
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error launching PowerShell for feature {featureName}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DisableFeatureAsync(string featureName, string displayName = null, CancellationToken cancellationToken = default)
    {
        displayName ??= featureName;

        try
        {
            var script = $@"
                try {{
                    Write-Host 'Winhance - Disabling Windows Optional Feature: {displayName}. This may take a few minutes...' -ForegroundColor Yellow

                    Write-Host 'Checking if feature exists...' -ForegroundColor Gray
                    $feature = Get-WindowsOptionalFeature -Online -FeatureName '{featureName}'

                    if (-not $feature) {{
                        Write-Host ""Feature '{featureName}' not found on this system."" -ForegroundColor Red
                        Write-Host ""Available features containing '{featureName}':"" -ForegroundColor Yellow
                        Get-WindowsOptionalFeature -Online | Where-Object {{ $_.FeatureName -like ""*{featureName}*"" }} |
                            ForEach-Object {{ Write-Host ""  - $($_.FeatureName)"" -ForegroundColor Cyan }}
                        Write-Host 'Press any key to close...' -ForegroundColor Gray
                        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                        return
                    }}

                    Write-Host ""Feature found. Current state: $($feature.State)"" -ForegroundColor Gray

                    if ($feature.State -ne 'Enabled') {{
                        Write-Host 'Feature is already disabled.' -ForegroundColor Green
                        Write-Host 'Press any key to close...' -ForegroundColor Gray
                        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                        return
                    }}

                    Write-Host 'Attempting to disable feature...' -ForegroundColor Gray
                    $result = Disable-WindowsOptionalFeature -Online -FeatureName '{featureName}' -NoRestart

                    if ($result.RestartNeeded) {{
                        Write-Host 'Feature disabled successfully (restart required)!' -ForegroundColor Green
                    }} else {{
                        Write-Host 'Feature disabled successfully!' -ForegroundColor Green
                    }}
                    Write-Host 'Press any key to close...' -ForegroundColor Gray
                    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                }} catch {{
                    Write-Host '=== FEATURE DISABLEMENT FAILED ===' -ForegroundColor Red
                    Write-Host 'Feature: {featureName}' -ForegroundColor Yellow
                    Write-Host 'Operation: Disable-WindowsOptionalFeature' -ForegroundColor Yellow
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
                    Write-Host '- Check if feature has dependencies that prevent removal' -ForegroundColor Gray
                    Write-Host '- Verify system files: sfc /scannow' -ForegroundColor Gray
                    Write-Host '- Try: DISM /Online /Cleanup-Image /RestoreHealth' -ForegroundColor Gray
                    Write-Host '=================================' -ForegroundColor Red
                    Write-Host 'Press any key to close...' -ForegroundColor Gray
                    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
                }}";

            var launched = await powerShellExecutionService.ExecuteScriptVisibleAsync(script);

            if (!launched)
            {
                logService?.LogError($"Failed to launch PowerShell for feature {featureName}");
            }

            return launched;
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error launching PowerShell for feature {featureName}: {ex.Message}");
            return false;
        }
    }


}
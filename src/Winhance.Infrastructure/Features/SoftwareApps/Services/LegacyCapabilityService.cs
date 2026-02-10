using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class LegacyCapabilityService(
    ILogService logService,
    IBloatRemovalService bloatRemovalService) : ILegacyCapabilityService
{
    public async Task<bool> EnableCapabilityAsync(
        string capabilityName,
        string displayName = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        displayName ??= capabilityName;

        try
        {
            logService?.LogInformation($"Enabling Windows Capability: {displayName} ({capabilityName})");

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"Enabling {displayName}...",
                IsIndeterminate = true
            });

            var psCommand = $"Add-WindowsCapability -Online -Name '{capabilityName}'";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"& {{ {psCommand}; pause }}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                }
            };
            process.Start();

            logService?.LogInformation($"PowerShell launched for capability '{capabilityName}'.");

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"PowerShell launched for {displayName}",
                IsIndeterminate = false
            });

            return true;
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error enabling capability {capabilityName}: {ex.Message}");
            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"Failed to enable {displayName}: {ex.Message}",
                IsIndeterminate = false,
                LogLevel = Core.Features.Common.Enums.LogLevel.Error
            });
            return false;
        }
    }

    public async Task<bool> DisableCapabilityAsync(
        string capabilityName,
        string displayName = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        displayName ??= capabilityName;

        try
        {
            logService?.LogInformation($"Disabling Windows Capability: {displayName} ({capabilityName})");

            var item = new ItemDefinition
            {
                Id = capabilityName,
                Name = displayName,
                Description = string.Empty,
                CapabilityName = capabilityName
            };

            var success = await bloatRemovalService.RemoveAppsAsync(
                new List<ItemDefinition> { item },
                progress,
                cancellationToken);

            if (success)
            {
                logService?.LogInformation($"Capability '{capabilityName}' removed successfully via BloatRemovalService.");
            }
            else
            {
                logService?.LogError($"BloatRemovalService failed to remove capability '{capabilityName}'.");
            }

            return success;
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error disabling capability {capabilityName}: {ex.Message}");
            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"Failed to disable {displayName}: {ex.Message}",
                IsIndeterminate = false,
                LogLevel = Core.Features.Common.Enums.LogLevel.Error
            });
            return false;
        }
    }
}

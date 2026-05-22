using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class LegacyCapabilityService(
    ILogService logService) : ILegacyCapabilityService
{
    public Task<bool> EnableCapabilityAsync(
        string capabilityName,
        string? displayName = null,
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
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"& {{ {psCommand}; pause }}\"",
                UseShellExecute = true,
                CreateNoWindow = false
            });

            logService?.LogInformation($"PowerShell launched for capability '{capabilityName}'.");

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"PowerShell launched for {displayName}",
                IsIndeterminate = false
            });

            return Task.FromResult(true);
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
            return Task.FromResult(false);
        }
    }
}

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class OptionalFeatureService(
    ILogService logService) : IOptionalFeatureService
{
    public Task<bool> EnableFeatureAsync(
        string featureName,
        string? displayName = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        displayName ??= featureName;

        try
        {
            logService?.LogInformation($"Enabling Windows Optional Feature: {displayName} ({featureName})");

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"Enabling {displayName}...",
                IsIndeterminate = true
            });

            var psCommand = $"Enable-WindowsOptionalFeature -Online -FeatureName '{featureName}' -All -NoRestart";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"& {{ {psCommand}; pause }}\"",
                UseShellExecute = true,
                CreateNoWindow = false
            });

            logService?.LogInformation($"PowerShell launched for feature '{featureName}'.");

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"PowerShell launched for {displayName}",
                IsIndeterminate = false
            });

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error enabling feature {featureName}: {ex.Message}");
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

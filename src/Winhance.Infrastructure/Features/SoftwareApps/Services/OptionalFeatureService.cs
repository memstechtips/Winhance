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
    ILogService logService,
    IWindowsAppsService windowsAppsService) : IOptionalFeatureService
{
    public async Task<bool> EnableFeatureAsync(
        string featureName,
        string displayName = null,
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

            logService?.LogInformation($"PowerShell launched for feature '{featureName}'.");

            progress?.Report(new TaskProgressDetail
            {
                StatusText = $"PowerShell launched for {displayName}",
                IsIndeterminate = false
            });

            return true;
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
            return false;
        }
    }

    public async Task<bool> DisableFeatureAsync(
        string featureName,
        string displayName = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        displayName ??= featureName;

        try
        {
            logService?.LogInformation($"Disabling Windows Optional Feature: {displayName} ({featureName})");

            var item = new ItemDefinition
            {
                Id = featureName,
                Name = displayName,
                Description = string.Empty,
                OptionalFeatureName = featureName
            };

            var result = await windowsAppsService.DisableOptionalFeatureNativeAsync(item, cancellationToken);

            if (result.Success)
            {
                logService?.LogInformation($"Feature '{featureName}' disabled successfully via DISM.");
            }
            else
            {
                logService?.LogError($"DISM failed to disable feature '{featureName}': {result.ErrorMessage}");
            }

            return result.Success;
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error disabling feature {featureName}: {ex.Message}");
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

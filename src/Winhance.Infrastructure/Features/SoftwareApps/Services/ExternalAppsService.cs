using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class ExternalAppsService(
    ILogService logService,
    IWinGetService winGetService,
    IAppStatusDiscoveryService appStatusDiscoveryService,
    IAppUninstallService appUninstallService,
    IDirectDownloadService directDownloadService,
    ITaskProgressService taskProgressService) : IExternalAppsService
{
    public string DomainName => FeatureIds.ExternalApps;

    private CancellationToken GetCurrentCancellationToken()
    {
        return taskProgressService?.CurrentTaskCancellationSource?.Token ?? CancellationToken.None;
    }

    public async Task<IEnumerable<ItemDefinition>> GetAppsAsync()
    {
        return ExternalAppDefinitions.GetExternalApps().Items;
    }

    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        var cancellationToken = GetCurrentCancellationToken();

        try
        {
            if (item.CustomProperties.TryGetValue("RequiresDirectDownload", out var requiresDownload)
                && requiresDownload is bool isDirect && isDirect)
            {
                logService.LogInformation($"Installing {item.Name} via direct download");
                var success = await directDownloadService.DownloadAndInstallAsync(item, progress, cancellationToken);
                return success
                    ? OperationResult<bool>.Succeeded(true)
                    : OperationResult<bool>.Failed("Direct download installation failed");
            }

            if (string.IsNullOrEmpty(item.WinGetPackageId))
                return OperationResult<bool>.Failed("No WinGet package ID or download URL specified");

            var wingetSuccess = await winGetService.InstallPackageAsync(item.WinGetPackageId, item.Name, cancellationToken);
            return wingetSuccess ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("Installation failed");
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation($"Installation of {item.Name} was cancelled");
            return OperationResult<bool>.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> UninstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            return await appUninstallService.UninstallAsync(item, progress, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            logService.LogInformation($"Uninstall of {item.Name} was cancelled by user");
            return OperationResult<bool>.Cancelled("Uninstall cancelled by user");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to uninstall {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<bool> CheckIfInstalledAsync(string winGetPackageId)
    {
        if (string.IsNullOrWhiteSpace(winGetPackageId))
            return false;

        try
        {
            var batch = await CheckBatchInstalledAsync(new[] { winGetPackageId });
            return batch.GetValueOrDefault(winGetPackageId, false);
        }
        catch (Exception ex)
        {
            logService.LogError($"Error checking if {winGetPackageId} is installed: {ex.Message}");
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> CheckBatchInstalledAsync(IEnumerable<string> winGetPackageIds)
    {
        return await appStatusDiscoveryService.GetExternalAppsInstallationStatusAsync(winGetPackageIds);
    }

    public async Task<Dictionary<string, bool>> CheckInstalledByDisplayNameAsync(IEnumerable<string> displayNames)
    {
        return await appStatusDiscoveryService.CheckInstalledByDisplayNameAsync(displayNames);
    }
}
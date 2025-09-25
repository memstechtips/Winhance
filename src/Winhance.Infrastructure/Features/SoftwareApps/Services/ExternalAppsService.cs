using System;
using System.Collections.Generic;
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
    IWinGetService winGetService) : IExternalAppsService
{
    public string DomainName => FeatureIds.ExternalApps;

    public async Task<IEnumerable<ItemDefinition>> GetAppsAsync()
    {
        return ExternalAppDefinitions.GetExternalApps().Items;
    }

    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (string.IsNullOrEmpty(item.WinGetPackageId))
                return OperationResult<bool>.Failed("No WinGet package ID specified");

            var success = await winGetService.InstallPackageAsync(item.WinGetPackageId, item.Name, CancellationToken.None);
            return success ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("Installation failed");
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to install {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }
}
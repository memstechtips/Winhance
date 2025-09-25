using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class WindowsAppsService(
    ILogService logService,
    IPowerShellExecutionService powerShellService,
    IWinGetService winGetService) : IWindowsAppsService
{
    public string DomainName => FeatureIds.WindowsApps;

    public async Task<IEnumerable<ItemDefinition>> GetAppsAsync()
    {
        var allItems = new List<ItemDefinition>();
        allItems.AddRange(WindowsAppDefinitions.GetWindowsApps().Items);
        allItems.AddRange(CapabilityDefinitions.GetWindowsCapabilities().Items);
        allItems.AddRange(OptionalFeatureDefinitions.GetWindowsOptionalFeatures().Items);
        return allItems;
    }

    public async Task<ItemDefinition?> GetAppByIdAsync(string appId)
    {
        var apps = await GetAppsAsync();
        return apps.FirstOrDefault(app => app.Id == appId);
    }

    public async Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(item.WinGetPackageId) || !string.IsNullOrEmpty(item.AppxPackageName))
            {
                var packageId = item.WinGetPackageId ?? item.AppxPackageName;
                var success = await winGetService.InstallPackageAsync(packageId, item.Name, CancellationToken.None);
                return success ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("Installation failed");
            }

            return OperationResult<bool>.Failed($"App type not supported: {item.Name}");
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
            if (string.IsNullOrEmpty(item.AppxPackageName))
                return OperationResult<bool>.Failed("No package name specified");

            var script = $"Get-AppxPackage '*{item.AppxPackageName}*' | Remove-AppxPackage";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to uninstall {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> EnableCapabilityAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (string.IsNullOrEmpty(item.CapabilityName))
                return OperationResult<bool>.Failed("No capability name specified");

            var script = $"Add-WindowsCapability -Online -Name '{item.CapabilityName}'";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to enable capability {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> DisableCapabilityAsync(ItemDefinition item)
    {
        try
        {
            if (string.IsNullOrEmpty(item.CapabilityName))
                return OperationResult<bool>.Failed("No capability name specified");

            var script = $"Remove-WindowsCapability -Online -Name '{item.CapabilityName}'";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to disable capability {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> EnableOptionalFeatureAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null)
    {
        try
        {
            if (string.IsNullOrEmpty(item.OptionalFeatureName))
                return OperationResult<bool>.Failed("No feature name specified");

            var script = $"Enable-WindowsOptionalFeature -Online -FeatureName '{item.OptionalFeatureName}' -All";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to enable feature {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }

    public async Task<OperationResult<bool>> DisableOptionalFeatureAsync(ItemDefinition item)
    {
        try
        {
            if (string.IsNullOrEmpty(item.OptionalFeatureName))
                return OperationResult<bool>.Failed("No feature name specified");

            var script = $"Disable-WindowsOptionalFeature -Online -FeatureName '{item.OptionalFeatureName}'";
            try
            {
                var output = await powerShellService.ExecuteScriptAsync(script);
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(ex.Message);
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"Failed to disable feature {item.Name}: {ex.Message}");
            return OperationResult<bool>.Failed(ex.Message);
        }
    }
}
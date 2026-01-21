using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWindowsAppsService : IAppDomainService
{
    Task<IEnumerable<ItemDefinition>> GetAppsAsync();
    Task<ItemDefinition?> GetAppByIdAsync(string appId);
    Task<Dictionary<string, bool>> CheckBatchInstalledAsync(IEnumerable<ItemDefinition> definitions);
    Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<bool>> UninstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<bool>> EnableCapabilityAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<bool>> DisableCapabilityAsync(ItemDefinition item);
    Task<OperationResult<bool>> EnableOptionalFeatureAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<bool>> DisableOptionalFeatureAsync(ItemDefinition item);
}
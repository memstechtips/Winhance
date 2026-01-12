using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IExternalAppsService : IAppDomainService
{
    Task<IEnumerable<ItemDefinition>> GetAppsAsync();
    Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<bool>> UninstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<bool> CheckIfInstalledAsync(string winGetPackageId);
    Task<Dictionary<string, bool>> CheckBatchInstalledAsync(IEnumerable<ItemDefinition> definitions);
    Task<Dictionary<string, bool>> CheckInstalledByDisplayNameAsync(IEnumerable<string> displayNames);
}
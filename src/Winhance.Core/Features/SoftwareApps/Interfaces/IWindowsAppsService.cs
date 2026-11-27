using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWindowsAppsService
{
    Task<IEnumerable<ItemDefinition>> GetAppsAsync();

    Task<ItemDefinition?> GetAppByIdAsync(string appId);
    Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
}
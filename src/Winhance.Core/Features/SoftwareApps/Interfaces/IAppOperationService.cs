using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IAppOperationService
{
    Task<OperationResult<bool>> InstallAppAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null, bool shouldRemoveFromBloatScript = true);
    Task<OperationResult<int>> InstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null, bool shouldRemoveFromBloatScript = true);
    Task<OperationResult<bool>> UninstallAppAsync(string appId, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<int>> UninstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null, bool saveRemovalScripts = true);
    Task<OperationResult<bool>> UninstallExternalAppAsync(string packageId, string displayName, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<int>> UninstallExternalAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<int>> UninstallAppsInParallelAsync(List<ItemDefinition> apps, bool saveRemovalScripts = true);
}
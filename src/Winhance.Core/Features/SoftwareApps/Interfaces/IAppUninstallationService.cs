using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IAppRemovalService
{
    Task<OperationResult<bool>> UninstallAppAsync(string appId, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<int>> UninstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null, bool saveRemovalScripts = true);
    Task<OperationResult<int>> UninstallAppsInParallelAsync(List<ItemDefinition> apps, bool saveRemovalScripts = true);
}

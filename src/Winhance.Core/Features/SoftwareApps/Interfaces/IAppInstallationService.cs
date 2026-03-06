using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IAppInstallationService
{
    Task<OperationResult<bool>> InstallAppAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null, bool shouldRemoveFromBloatScript = true);
    Task<OperationResult<int>> InstallAppsAsync(List<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null, bool shouldRemoveFromBloatScript = true);
}

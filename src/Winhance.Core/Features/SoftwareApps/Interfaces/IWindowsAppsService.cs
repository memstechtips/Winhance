using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWindowsAppsService
{
    string DomainName { get; }
    Task<IEnumerable<ItemDefinition>> GetAppsAsync();
    void InvalidateStatusCache();
    event EventHandler? WinGetReady;

    Task<ItemDefinition?> GetAppByIdAsync(string appId);
    Task<Dictionary<string, bool>> CheckBatchInstalledAsync(IEnumerable<ItemDefinition> definitions);
    Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
}
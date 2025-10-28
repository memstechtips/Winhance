using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IBloatRemovalService
{
    Task<bool> RemoveAppsAsync(
        List<ItemDefinition> selectedApps,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveSpecialAppsAsync(
        List<string> specialAppTypes,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveItemsFromScriptAsync(List<ItemDefinition> itemsToRemove);
    Task<bool> ExecuteRemovalScriptAsync(string scriptPath, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> RegisterStartupTaskAsync(string scriptPath);
}
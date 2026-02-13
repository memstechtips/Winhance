using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IBloatRemovalService
{
    Task<bool> ExecuteDedicatedScriptAsync(ItemDefinition app,
        IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    Task<bool> ExecuteBloatRemovalAsync(List<ItemDefinition> apps,
        IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    Task PersistRemovalScriptsAsync(List<ItemDefinition> allApps);
    Task CleanupAllRemovalArtifactsAsync();

    Task<bool> RemoveItemsFromScriptAsync(List<ItemDefinition> itemsToRemove);
    Task CleanupBloatRemovalArtifactsAsync();
}

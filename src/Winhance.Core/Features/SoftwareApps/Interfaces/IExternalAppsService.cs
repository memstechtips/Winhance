using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IExternalAppsService
{
    Task<IEnumerable<ItemDefinition>> GetAppsAsync();

    Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<bool>> InstallAppOnNewPcAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<bool>> UninstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<PackageSearchResult> SearchPackagesAsync(string query, CancellationToken cancellationToken = default);

    // Apps whose only source is winget and whose winget ids are all gone from it, so nothing could install them.
    Task<IReadOnlyCollection<string>> GetUnavailableAppIdsAsync(IEnumerable<ItemDefinition> items, CancellationToken cancellationToken = default);
}
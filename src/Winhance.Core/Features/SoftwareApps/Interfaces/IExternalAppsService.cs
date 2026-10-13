using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IExternalAppsService
{
    string DomainName { get; }
    Task<IEnumerable<ItemDefinition>> GetAppsAsync();
    void InvalidateStatusCache();
    event EventHandler? WinGetReady;

    Task<OperationResult<bool>> InstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<bool>> InstallAppForAllUsersAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<OperationResult<bool>> UninstallAppAsync(ItemDefinition item, IProgress<TaskProgressDetail>? progress = null);
    Task<Dictionary<string, bool>> CheckBatchInstalledAsync(IEnumerable<ItemDefinition> definitions);
    Task<PackageSearchResult> SearchPackagesAsync(string query, CancellationToken cancellationToken = default);

    // Apps whose only source is winget and whose winget ids are all gone from it, so nothing could install them.
    Task<IReadOnlyCollection<string>> GetUnavailableAppIdsAsync(IEnumerable<ItemDefinition> items, CancellationToken cancellationToken = default);
}
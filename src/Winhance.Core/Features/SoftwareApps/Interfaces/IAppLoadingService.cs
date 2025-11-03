using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    public interface IAppLoadingService
    {
        Task<OperationResult<IEnumerable<ItemDefinition>>> LoadAppsAsync();
        Task<OperationResult<bool>> RefreshInstallationStatusAsync(IEnumerable<ItemDefinition> apps);
        Task<OperationResult<InstallStatus>> GetInstallStatusAsync(string appId);
        Task<OperationResult<bool>> SetInstallStatusAsync(string appId, InstallStatus status);
        Task<ItemDefinition?> GetAppByIdAsync(string appId);
        Task<IEnumerable<ItemDefinition>> LoadCapabilitiesAsync();
        Task<Dictionary<string, bool>> GetBatchInstallStatusAsync(IEnumerable<ItemDefinition> definitions);
        void ClearStatusCache();
    }
}
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces
{
    public interface IInstallationStatusService
    {
        Task<InstallStatus> GetInstallStatusAsync(string appId);
        Task<RefreshResult> RefreshStatusAsync(IEnumerable<string> appIds);
        Task<bool> SetInstallStatusAsync(string appId, InstallStatus status);
    }
}

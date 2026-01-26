using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IAppStatusDiscoveryService
{
    Task<Dictionary<string, bool>> GetInstallationStatusBatchAsync(IEnumerable<ItemDefinition> definitions);
    Task<Dictionary<string, bool>> GetInstallationStatusByIdAsync(IEnumerable<string> appIds);
    Task<Dictionary<string, bool>> GetExternalAppsInstallationStatusAsync(IEnumerable<ItemDefinition> definitions);
}
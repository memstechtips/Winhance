using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IInstalledAppsService
{
    Task<InstalledAppsScan> GetInstalledAppsAsync(CancellationToken cancellationToken = default);

    Task<OperationResult<bool>> UpdateAsync(InstalledApp app, CancellationToken cancellationToken = default);
}

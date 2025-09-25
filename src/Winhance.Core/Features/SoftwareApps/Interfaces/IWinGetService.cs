using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IWinGetService
{
    Task<bool> InstallPackageAsync(string packageId, string displayName = null, CancellationToken cancellationToken = default);
    Task<bool> EnsureWinGetInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> IsWinGetInstalledAsync();
    Task<bool> IsPackageInstalledAsync(string packageId, CancellationToken cancellationToken = default);
}
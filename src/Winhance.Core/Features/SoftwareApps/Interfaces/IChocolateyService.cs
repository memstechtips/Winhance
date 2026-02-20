using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IChocolateyService
{
    Task<bool> IsChocolateyInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallChocolateyAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallPackageAsync(string chocoPackageId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);
}

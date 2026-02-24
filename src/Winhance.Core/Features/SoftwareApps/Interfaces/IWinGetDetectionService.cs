using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Handles WinGet package detection — installed package enumeration and installer type lookup.
/// Uses COM API with CLI fallback.
/// </summary>
public interface IWinGetDetectionService
{
    Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default);
    Task<string?> GetInstallerTypeAsync(string packageId, CancellationToken cancellationToken = default);
}

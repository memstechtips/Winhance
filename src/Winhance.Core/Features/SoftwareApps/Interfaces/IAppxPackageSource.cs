using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Enumerates installed AppX packages (PackageManager COM → WMI → PowerShell fallback).
/// </summary>
public interface IAppxPackageSource
{
    Task<HashSet<string>> GetInstalledPackageNamesAsync(CancellationToken cancellationToken = default);
}

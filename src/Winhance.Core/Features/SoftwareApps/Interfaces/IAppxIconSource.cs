using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Source for AppX package logos. Wraps the Windows-only
/// Windows.Management.Deployment.PackageManager + AppListEntry.DisplayInfo.GetLogo
/// API surface so consumers (e.g. AppIconResolver) can be unit-tested.
/// </summary>
public interface IAppxIconSource
{
    /// <summary>
    /// Enumerates installed AppX packages for the current user.
    /// Returns a dictionary keyed by Package.Id.Name (e.g. "Microsoft.WindowsCalculator")
    /// with values being Package.Id.FullName (e.g.
    /// "Microsoft.WindowsCalculator_10.2103.8.0_x64__8wekyb3d8bbwe").
    ///
    /// Case-insensitive on the key (uses StringComparer.OrdinalIgnoreCase).
    /// Returns an empty dictionary on enumeration failure (caller treats as "no icons available").
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetInstalledPackageMapAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Returns a stream containing the package's primary app logo PNG at the requested size,
    /// or null if the package is not found, has no app list entry, or logo extraction fails.
    /// Caller takes ownership and disposes.
    /// </summary>
    Task<Stream?> GetLogoStreamAsync(
        string packageFullName,
        Size size,
        CancellationToken ct = default);
}

using Windows.Foundation;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IAppxIconSource
{
    // Keyed by Package.Id.Name (OrdinalIgnoreCase) -> Package.Id.FullName. Empty on enumeration failure, which the
    // caller treats as "no icons available".
    Task<IReadOnlyDictionary<string, string>> GetInstalledPackageMapAsync(
        CancellationToken ct = default);

    // The caller takes ownership of the stream.
    Task<Stream?> GetLogoStreamAsync(
        string packageFullName,
        Size size,
        CancellationToken ct = default);
}

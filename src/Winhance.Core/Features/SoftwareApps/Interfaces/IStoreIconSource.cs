using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Network-based icon source for Microsoft Store apps. Used as a fallback when
/// IAppxIconSource cannot resolve an icon (e.g. the package is not installed,
/// not provisioned, and not registered for any other user on this machine).
/// </summary>
public interface IStoreIconSource
{
    /// <summary>
    /// Fetches the app icon image stream for the given Microsoft Store product ID
    /// (e.g. "9NBLGGH42THS"). Returns null on any failure — no network, rate
    /// limit, product not found, malformed response, etc. Caller takes ownership
    /// and disposes the stream.
    /// </summary>
    Task<Stream?> GetIconStreamAsync(string msStoreId, CancellationToken ct = default);
}

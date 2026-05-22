using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Layer 1b of the icon resolution pipeline: extract an icon from an installed
/// binary on disk (.exe, .ico, install folder) using Windows Shell APIs.
/// Returns a PNG stream on success, null on any failure (path missing, shell
/// call returned a generic file glyph, decode error, etc).
/// </summary>
public interface IBinaryIconSource
{
    Task<Stream?> GetIconStreamAsync(string filePath, Size size, CancellationToken ct = default);
}

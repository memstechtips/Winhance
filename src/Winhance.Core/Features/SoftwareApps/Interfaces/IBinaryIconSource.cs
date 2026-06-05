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

    /// <summary>
    /// Extract a specific icon from <paramref name="filePath"/> by selector (non-negative =
    /// zero-based index; negative = negated resource ID), returning a PNG stream or null on
    /// failure. Used for system-DLL icon sources like <c>shell32.dll,#512</c>.
    /// </summary>
    Task<Stream?> GetIconStreamByIndexAsync(string filePath, int iconSelector, Size size, CancellationToken ct = default);
}

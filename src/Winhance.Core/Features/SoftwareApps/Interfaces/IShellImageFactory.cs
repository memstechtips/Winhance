using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Inner seam for Layer 1b — the actual Windows Shell API call that produces a
/// PNG-encoded byte buffer for a file path. Separated from IBinaryIconSource so
/// the BinaryIconSource orchestration (timeout, error wrapping, generic-glyph
/// detection) is testable on Linux against a mocked factory.
/// </summary>
public interface IShellImageFactory
{
    /// <summary>
    /// Returns PNG-encoded bytes for the icon associated with <paramref name="filePath"/>,
    /// at the requested size. Throws on failure — callers are expected to catch and
    /// translate to null.
    /// </summary>
    Task<byte[]> GetIconBytesAsync(string filePath, Size size, CancellationToken ct = default);
}

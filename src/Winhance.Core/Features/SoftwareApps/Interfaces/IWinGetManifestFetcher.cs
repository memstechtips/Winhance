using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Inner seam for Layer 2b's GitHub-fallback path. Encapsulates the Contents API
/// call (find latest version), raw fetch (locale or singleton manifest), and YAML
/// parse, returning the first usable icon URL or null. Throws on rate-limit so
/// callers can short-circuit subsequent calls within the same session.
/// </summary>
public interface IWinGetManifestFetcher
{
    Task<string?> GetIconUrlAsync(string winGetPackageId, CancellationToken ct = default);
}

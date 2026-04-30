using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Layer 2b of the icon resolution pipeline: source an icon for a WinGet package.
/// Tries the local WinGet COM catalog first (fast, in-process, no network rate
/// limit); on COM failure or unavailability falls back to the UniGetUI icon
/// database (see <see cref="IWinGetIconUrlOverrides"/>) — a community-curated
/// mapping of WinGet package IDs to icon URLs, fetched once per session.
/// Returns null on any miss.
/// </summary>
public interface IWinGetIconSource
{
    Task<Stream?> GetIconStreamAsync(string winGetPackageId, CancellationToken ct = default);
}

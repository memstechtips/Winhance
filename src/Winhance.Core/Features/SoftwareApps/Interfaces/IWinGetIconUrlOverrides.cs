using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Static-data fallback for <see cref="IWinGetIconSource"/>: maps a
/// <c>WinGetPackageId</c> to a vendor icon URL when the local WinGet COM
/// catalog can't surface one. Backed by the UniGetUI community icon database
/// (MIT-licensed, see <c>THIRD-PARTY-NOTICES.txt</c>); fetched once per
/// session, failures are fail-soft (empty map for the session, retry on next
/// launch).
/// </summary>
public interface IWinGetIconUrlOverrides
{
    Task<string?> TryGetAsync(string winGetPackageId, CancellationToken ct = default);
}

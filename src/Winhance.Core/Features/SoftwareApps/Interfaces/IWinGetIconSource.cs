using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Layer 2b of the icon resolution pipeline: source an icon for a WinGet package.
/// Tries the bundled COM API first (fast, in-process); on COM failure or
/// unavailability falls back to fetching the raw manifest YAML from
/// <c>microsoft/winget-pkgs</c> on GitHub. Returns null on any miss.
/// </summary>
public interface IWinGetIconSource
{
    Task<Stream?> GetIconStreamAsync(string winGetPackageId, CancellationToken ct = default);
}

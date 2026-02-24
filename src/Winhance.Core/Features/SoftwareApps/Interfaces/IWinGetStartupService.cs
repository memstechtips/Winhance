using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Handles WinGet/AppInstaller readiness on application startup.
/// </summary>
public interface IWinGetStartupService
{
    /// <summary>
    /// Ensures WinGet is ready for use on startup. If system winget is available,
    /// silently attempts an upgrade. Otherwise installs AppInstaller with progress.
    /// </summary>
    Task EnsureWinGetReadyOnStartupAsync();
}

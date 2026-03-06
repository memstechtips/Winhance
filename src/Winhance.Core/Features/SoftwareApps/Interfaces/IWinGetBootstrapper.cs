using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Handles WinGet/AppInstaller bootstrapping, upgrade, and readiness checks.
/// </summary>
public interface IWinGetBootstrapper
{
    /// <summary>
    /// Raised after WinGet is successfully installed and the COM API is verified ready.
    /// Subscribers can use this to refresh installation status that depends on WinGet.
    /// </summary>
    event EventHandler? WinGetInstalled;

    /// <summary>
    /// True if the DesktopAppInstaller MSIX is registered (system winget found in PATH or WindowsApps).
    /// False means only the bundled CLI is available.
    /// </summary>
    bool IsSystemWinGetAvailable { get; }

    Task<bool> InstallWinGetAsync(CancellationToken cancellationToken = default);
    Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default);
    Task<bool> UpgradeAppInstallerAsync(CancellationToken cancellationToken = default);
}

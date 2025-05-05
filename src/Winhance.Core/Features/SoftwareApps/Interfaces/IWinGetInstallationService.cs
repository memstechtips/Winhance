using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Interface for services that handle WinGet-related installation operations.
/// </summary>
public interface IWinGetInstallationService
{
    /// <summary>
    /// Installs a package using WinGet.
    /// </summary>
    /// <param name="packageName">The package name or ID to install.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <param name="displayName">The display name of the package for progress reporting.</param>
    /// <returns>True if installation was successful; otherwise, false.</returns>
    Task<bool> InstallWithWingetAsync(
        string packageName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default,
        string? displayName = null);

    /// <summary>
    /// Installs WinGet if not already installed.
    /// </summary>
    Task InstallWinGetAsync(IProgress<TaskProgressDetail>? progress = null);

    /// <summary>
    /// Checks if WinGet is installed on the system.
    /// </summary>
    /// <returns>True if WinGet is installed, false otherwise</returns>
    Task<bool> IsWinGetInstalledAsync();
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Interface for services that handle custom application installations.
/// </summary>
public interface ICustomAppInstallationService
{
    /// <summary>
    /// Installs a custom application.
    /// </summary>
    /// <param name="appInfo">Information about the application to install.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if installation was successful; otherwise, false.</returns>
    Task<bool> InstallCustomAppAsync(
        AppInfo appInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if internet connection is available.
    /// </summary>
    /// <returns>True if internet connection is available; otherwise, false.</returns>
    Task<bool> CheckInternetConnectionAsync();
}

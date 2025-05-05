using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Interface for services that handle OneDrive installation.
/// </summary>
public interface IOneDriveInstallationService
{
    /// <summary>
    /// Installs OneDrive from the Microsoft download link.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if installation was successful; otherwise, false.</returns>
    Task<bool> InstallOneDriveAsync(
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}

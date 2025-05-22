using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="ITaskProgressService"/> specific to WinGet operations.
    /// </summary>
    public static class WinGetProgressExtensions
    {
        /// <summary>
        /// Tracks the progress of a WinGet installation operation.
        /// </summary>
        /// <param name="progressService">The progress service.</param>
        /// <param name="operation">The asynchronous operation to track.</param>
        /// <param name="displayName">The display name of the application.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the result.</returns>
        public static async Task<InstallationResult> TrackWinGetInstallationAsync(
            this ITaskProgressService progressService,
            Func<IProgress<InstallationProgress>, Task<InstallationResult>> operation,
            string displayName,
            CancellationToken cancellationToken = default
        )
        {
            if (progressService == null)
                throw new ArgumentNullException(nameof(progressService));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var progress = new Progress<InstallationProgress>(p =>
            {
                progressService.UpdateDetailedProgress(
                    new TaskProgressDetail
                    {
                        Progress = p.Percentage,
                        StatusText = $"Installing {displayName}: {p.Status}",
                        IsIndeterminate = p.IsIndeterminate,
                    }
                );
            });

            try
            {
                return await operation(progress).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                progressService.UpdateProgress(0, $"Failed to install {displayName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Tracks the progress of a WinGet upgrade operation.
        /// </summary>
        /// <param name="progressService">The progress service.</param>
        /// <param name="operation">The asynchronous operation to track.</param>
        /// <param name="displayName">The display name of the application.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the result.</returns>
        public static async Task<UpgradeResult> TrackWinGetUpgradeAsync(
            this ITaskProgressService progressService,
            Func<IProgress<UpgradeProgress>, Task<UpgradeResult>> operation,
            string displayName,
            CancellationToken cancellationToken = default
        )
        {
            if (progressService == null)
                throw new ArgumentNullException(nameof(progressService));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var progress = new Progress<UpgradeProgress>(p =>
            {
                progressService.UpdateDetailedProgress(
                    new TaskProgressDetail
                    {
                        Progress = p.Percentage,
                        StatusText = $"Upgrading {displayName}: {p.Status}",
                        IsIndeterminate = p.IsIndeterminate,
                    }
                );
            });

            try
            {
                return await operation(progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                progressService.UpdateProgress(0, $"Upgrade of {displayName} was cancelled");
                return new UpgradeResult
                {
                    Success = false,
                    Message = $"Upgrade of {displayName} was cancelled by user",
                    PackageId = displayName
                };
            }
            catch (Exception ex)
            {
                progressService.UpdateProgress(0, $"Failed to upgrade {displayName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Tracks the progress of a WinGet uninstallation operation.
        /// </summary>
        /// <param name="progressService">The progress service.</param>
        /// <param name="operation">The asynchronous operation to track.</param>
        /// <param name="displayName">The display name of the application.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the result.</returns>
        public static async Task<UninstallationResult> TrackWinGetUninstallationAsync(
            this ITaskProgressService progressService,
            Func<IProgress<UninstallationProgress>, Task<UninstallationResult>> operation,
            string displayName,
            CancellationToken cancellationToken = default
        )
        {
            if (progressService == null)
                throw new ArgumentNullException(nameof(progressService));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var progress = new Progress<UninstallationProgress>(p =>
            {
                progressService.UpdateDetailedProgress(
                    new TaskProgressDetail
                    {
                        Progress = p.Percentage,
                        StatusText = $"Uninstalling {displayName}: {p.Status}",
                        IsIndeterminate = p.IsIndeterminate,
                    }
                );
            });

            try
            {
                return await operation(progress).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                progressService.UpdateProgress(0, $"Failed to uninstall {displayName}: {ex.Message}");
                throw;
            }
        }
    }
}

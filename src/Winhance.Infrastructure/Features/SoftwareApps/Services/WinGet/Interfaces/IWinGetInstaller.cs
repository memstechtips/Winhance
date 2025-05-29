using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces
{
    /// <summary>
    /// Defines the contract for WinGet package installation and management.
    /// </summary>
    public interface IWinGetInstaller
    {
        /// <summary>
        /// Installs a package using WinGet.
        /// </summary>
        /// <param name="packageId">The ID of the package to install.</param>
        /// <param name="options">Optional installation options.</param>
        /// <param name="displayName">The display name to use in progress reporting.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the installation result.</returns>
        Task<InstallationResult> InstallPackageAsync(
            string packageId, 
            InstallationOptions options = null,
            string displayName = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Upgrades a package using WinGet.
        /// </summary>
        /// <param name="packageId">The ID of the package to upgrade.</param>
        /// <param name="options">Optional upgrade options.</param>
        /// <param name="displayName">The display name to use in progress reporting.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the upgrade result.</returns>
        Task<UpgradeResult> UpgradePackageAsync(
            string packageId, 
            UpgradeOptions options = null,
            string displayName = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Uninstalls a package using WinGet.
        /// </summary>
        /// <param name="packageId">The ID of the package to uninstall.</param>
        /// <param name="options">Optional uninstallation options.</param>
        /// <param name="displayName">The display name to use in progress reporting.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the uninstallation result.</returns>
        Task<UninstallationResult> UninstallPackageAsync(
            string packageId, 
            UninstallationOptions options = null,
            string displayName = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets information about an installed package.
        /// </summary>
        /// <param name="packageId">The ID of the package to get information about.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the package information.</returns>
        Task<PackageInfo> GetPackageInfoAsync(
            string packageId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for packages matching the given query.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="options">Optional search options.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the search results.</returns>
        Task<IEnumerable<PackageInfo>> SearchPackagesAsync(
            string query, 
            SearchOptions options = null, 
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Attempts to install WinGet if it's not already installed.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if WinGet was installed successfully or was already installed.</returns>
        Task<bool> TryInstallWinGetAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Executes a WinGet command directly.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <param name="arguments">The arguments for the command.</param>
        /// <param name="progressAdapter">Optional progress adapter.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the command result.</returns>
        Task<(int ExitCode, string Output, string Error)> ExecuteWinGetCommandAsync(
            string command,
            string arguments,
            IProgress<InstallationProgress> progressAdapter = null,
            CancellationToken cancellationToken = default);
    }
}

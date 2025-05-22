using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces
{
    /// <summary>
    /// Defines the contract for verifying software installations.
    /// </summary>
    public interface IInstallationVerifier
    {
        /// <summary>
        /// Verifies if a package is installed.
        /// </summary>
        /// <param name="packageId">The ID of the package to verify.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the verification result.</returns>
        Task<VerificationResult> VerifyInstallationAsync(
            string packageId, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies if a package is installed with the specified version.
        /// </summary>
        /// <param name="packageId">The ID of the package to verify.</param>
        /// <param name="version">The expected version of the package.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the verification result.</returns>
        Task<VerificationResult> VerifyInstallationAsync(
            string packageId, 
            string version, 
            CancellationToken cancellationToken = default);
    }
}

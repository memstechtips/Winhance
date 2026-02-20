using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces
{
    /// <summary>
    /// Defines the contract for different verification methods to check if a package is installed.
    /// </summary>
    public interface IVerificationMethod
    {
        /// <summary>
        /// Gets the name of the verification method.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the priority of this verification method. Lower numbers indicate higher priority.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Verifies if a package is installed using this verification method.
        /// </summary>
        /// <param name="packageId">The ID of the package to verify.</param>
        /// <param name="version">Optional version to verify. If null, only the presence is checked.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the verification result.</returns>
        Task<VerificationResult> VerifyAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default);
    }
}

using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Verification
{
    /// <summary>
    /// Base class for verification methods that check if a package is installed.
    /// </summary>
    public abstract class VerificationMethodBase
    {
        /// <summary>
        /// Gets the name of the verification method.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the priority of this verification method. Lower values mean higher priority.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VerificationMethodBase"/> class.
        /// </summary>
        /// <param name="name">The name of the verification method.</param>
        /// <param name="priority">The priority of the verification method. Lower values mean higher priority.</param>
        protected VerificationMethodBase(string name, int priority)
        {
            Name = name ?? throw new System.ArgumentNullException(nameof(name));
            Priority = priority;
        }

        /// <summary>
        /// Verifies if a package is installed.
        /// </summary>
        /// <param name="packageId">The ID of the package to verify.</param>
        /// <param name="version">The version of the package to verify, or null to check for any version.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the verification result.</returns>
        public async Task<VerificationResult> VerifyAsync(
            string packageId,
            string? version = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new System.ArgumentException("Package ID cannot be null or whitespace.", nameof(packageId));
            }

            return version == null
                ? await VerifyPresenceAsync(packageId, cancellationToken).ConfigureAwait(false)
                : await VerifyVersionAsync(packageId, version, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// When overridden in a derived class, verifies if a package is installed, regardless of version.
        /// </summary>
        /// <param name="packageId">The ID of the package to verify.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the verification result.</returns>
        protected abstract Task<VerificationResult> VerifyPresenceAsync(
            string packageId,
            CancellationToken cancellationToken);

        /// <summary>
        /// When overridden in a derived class, verifies if a specific version of a package is installed.
        /// </summary>
        /// <param name="packageId">The ID of the package to verify.</param>
        /// <param name="version">The version of the package to verify.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the verification result.</returns>
        protected abstract Task<VerificationResult> VerifyVersionAsync(
            string packageId,
            string version,
            CancellationToken cancellationToken);
    }
}

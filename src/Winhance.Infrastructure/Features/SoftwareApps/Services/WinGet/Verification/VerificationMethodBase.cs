using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification
{
    /// <summary>
    /// Base class for verification methods that provides common functionality.
    /// </summary>
    public abstract class VerificationMethodBase : IVerificationMethod
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VerificationMethodBase"/> class.
        /// </summary>
        /// <param name="name">The name of the verification method.</param>
        /// <param name="priority">The priority of the verification method. Lower numbers indicate higher priority.</param>
        protected VerificationMethodBase(string name, int priority)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Verification method name cannot be null or whitespace.",
                    nameof(name)
                );

            Name = name;
            Priority = priority;
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public int Priority { get; }

        /// <inheritdoc/>
        public async Task<VerificationResult> VerifyAsync(
            string packageId,
            string version = null,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException(
                    "Package ID cannot be null or whitespace.",
                    nameof(packageId)
                );

            try
            {
                if (version == null)
                {
                    return await VerifyPresenceAsync(packageId, cancellationToken)
                        .ConfigureAwait(false);
                }

                return await VerifyVersionAsync(packageId, version, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new VerificationResult
                {
                    IsVerified = false,
                    Message = $"Error during verification using {Name}: {ex.Message}",
                    MethodUsed = Name,
                };
            }
        }

        /// <summary>
        /// Verifies if a package is present (without version check).
        /// </summary>
        /// <param name="packageId">The ID of the package to verify.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the verification result.</returns>
        protected abstract Task<VerificationResult> VerifyPresenceAsync(
            string packageId,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Verifies if a package is present with the specified version.
        /// </summary>
        /// <param name="packageId">The ID of the package to verify.</param>
        /// <param name="version">The expected version of the package.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the verification result.</returns>
        protected abstract Task<VerificationResult> VerifyVersionAsync(
            string packageId,
            string version,
            CancellationToken cancellationToken
        );
    }
}

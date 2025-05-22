using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification
{
    /// <summary>
    /// Coordinates multiple verification methods to determine if a package is installed.
    /// </summary>
    public class CompositeInstallationVerifier : IInstallationVerifier
    {
        private readonly IEnumerable<IVerificationMethod> _verificationMethods;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeInstallationVerifier"/> class.
        /// </summary>
        /// <param name="verificationMethods">The verification methods to use.</param>
        public CompositeInstallationVerifier(IEnumerable<IVerificationMethod> verificationMethods)
        {
            _verificationMethods =
                verificationMethods?.OrderBy(m => m.Priority).ToList()
                ?? throw new ArgumentNullException(nameof(verificationMethods));
        }

        /// <inheritdoc/>
        public async Task<VerificationResult> VerifyInstallationAsync(
            string packageId,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException(
                    "Package ID cannot be null or whitespace.",
                    nameof(packageId)
                );

            // Add a short delay before verification to allow Windows to complete registration
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);

            var results = new List<VerificationResult>();
            VerificationResult successfulResult = null;

            // Try verification with up to 3 attempts with increasing delays
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                // Clear previous results for each attempt
                results.Clear();
                successfulResult = null;
                
                foreach (var method in _verificationMethods)
                {
                    try
                    {
                        var result = await method
                            .VerifyAsync(packageId, cancellationToken: cancellationToken)
                            .ConfigureAwait(false);

                        results.Add(result);

                        if (result.IsVerified)
                        {
                            successfulResult = result;
                            break; // Stop at first successful verification
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        results.Add(
                            VerificationResult.Failure($"Error in {method.Name}: {ex.Message}")
                        );
                    }
                }

                // If we found a successful result, return it immediately
                if (successfulResult != null)
                    return successfulResult;
                
                // If this isn't the last attempt, wait before trying again
                if (attempt < 3)
                {
                    // Exponential backoff: 1s, then 2s
                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken).ConfigureAwait(false);
                }
            }

            // If we get here, all attempts failed
            var errorMessages = results
                .Where(r => !r.IsVerified && !string.IsNullOrEmpty(r.Message))
                .Select(r => r.Message)
                .ToList();

            return new VerificationResult
            {
                IsVerified = false,
                Message =
                    $"Package '{packageId}' not found after multiple attempts. Details: {string.Join("; ", errorMessages)}",
                MethodUsed = "Composite",
                AdditionalInfo = new { PackageId = packageId, VerificationResults = results },
            };
        }

        /// <inheritdoc/>
        public async Task<VerificationResult> VerifyInstallationAsync(
            string packageId,
            string version,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException(
                    "Package ID cannot be null or whitespace.",
                    nameof(packageId)
                );
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException(
                    "Version cannot be null or whitespace.",
                    nameof(version)
                );

            var results = new List<VerificationResult>();
            VerificationResult successfulResult = null;

            foreach (var method in _verificationMethods)
            {
                try
                {
                    var result = await method
                        .VerifyAsync(packageId, version, cancellationToken)
                        .ConfigureAwait(false);

                    results.Add(result);

                    if (result.IsVerified)
                    {
                        successfulResult = result;
                        // Don't break here, as we want to try all methods to find an exact version match
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    results.Add(
                        VerificationResult.Failure($"Error in {method.Name}: {ex.Message}")
                    );
                }
            }

            // If we found a successful result, return it
            if (successfulResult != null)
                return successfulResult;

            // Check if any method found the package but with a different version
            var versionMismatch = results.FirstOrDefault(r =>
                r.IsVerified
                && r.AdditionalInfo is IDictionary<string, object> info
                && info.ContainsKey("Version")
                && info["Version"]?.ToString() != version
            );

            if (versionMismatch != null)
            {
                var installedVersion =
                    (versionMismatch.AdditionalInfo as IDictionary<string, object>)?[
                        "Version"
                    ]?.ToString() ?? "unknown";
                return VerificationResult.Failure(
                    $"Version mismatch for '{packageId}'. Installed: {installedVersion}, Expected: {version}",
                    versionMismatch.MethodUsed
                );
            }

            // Otherwise, return a composite result with all the failures
            var errorMessages = results
                .Where(r => !r.IsVerified && !string.IsNullOrEmpty(r.Message))
                .Select(r => r.Message)
                .ToList();

            return new VerificationResult
            {
                IsVerified = false,
                Message =
                    $"Package '{packageId}' version '{version}' not found. Details: {string.Join("; ", errorMessages)}",
                MethodUsed = "Composite",
                AdditionalInfo = new
                {
                    PackageId = packageId,
                    Version = version,
                    VerificationResults = results,
                },
            };
        }
    }
}

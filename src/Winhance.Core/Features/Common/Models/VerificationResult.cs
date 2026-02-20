using System;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents the result of a verification operation.
    /// </summary>
    public class VerificationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the verification was successful.
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Gets or sets the version that was verified, if applicable.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets an optional message providing additional information about the verification.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the name of the verification method that was used.
        /// </summary>
        public string? MethodUsed { get; set; }

        /// <summary>
        /// Gets or sets additional information about the verification result.
        /// This can be any object containing relevant details.
        /// </summary>
        public object? AdditionalInfo { get; set; }

        /// <summary>
        /// Creates a successful verification result.
        /// </summary>
        /// <param name="version">The version that was verified.</param>
        /// <param name="methodUsed">The name of the verification method that was used.</param>
        /// <param name="additionalInfo">Additional information about the verification result.</param>
        /// <returns>A successful verification result.</returns>
        public static VerificationResult Success(string? version = null, string? methodUsed = null, object? additionalInfo = null)
        {
            return new VerificationResult
            {
                IsVerified = true,
                Version = version,
                MethodUsed = methodUsed,
                AdditionalInfo = additionalInfo
            };
        }

        /// <summary>
        /// Creates a failed verification result.
        /// </summary>
        /// <param name="message">An optional message explaining why the verification failed.</param>
        /// <param name="methodUsed">The name of the verification method that was used.</param>
        /// <param name="additionalInfo">Additional information about the verification result.</param>
        /// <returns>A failed verification result.</returns>
        public static VerificationResult Failure(string? message = null, string? methodUsed = null, object? additionalInfo = null)
        {
            return new VerificationResult
            {
                IsVerified = false,
                Message = message,
                MethodUsed = methodUsed,
                AdditionalInfo = additionalInfo
            };
        }
    }
}

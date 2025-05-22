namespace Winhance.Core.Features.Common.Enums
{
    /// <summary>
    /// Represents the reason for a cancellation operation.
    /// </summary>
    public enum CancellationReason
    {
        /// <summary>
        /// No cancellation occurred.
        /// </summary>
        None = 0,

        /// <summary>
        /// Cancellation was initiated by the user.
        /// </summary>
        UserCancelled = 1,

        /// <summary>
        /// Cancellation occurred due to internet connectivity issues.
        /// </summary>
        InternetConnectivityLost = 2,

        /// <summary>
        /// Cancellation occurred due to a system error.
        /// </summary>
        SystemError = 3
    }
}

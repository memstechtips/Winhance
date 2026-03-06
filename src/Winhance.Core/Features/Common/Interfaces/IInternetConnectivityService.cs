using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Interface for services that check and monitor internet connectivity.
/// </summary>
public interface IInternetConnectivityService
{
    /// <summary>
    /// Asynchronously checks if the system has an active internet connection.
    /// </summary>
    /// <param name="forceCheck">If true, bypasses the cache and performs a fresh check.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if internet is connected, false otherwise.</returns>
    Task<bool> IsInternetConnectedAsync(bool forceCheck = false, CancellationToken cancellationToken = default, bool userInitiatedCancellation = false);
}

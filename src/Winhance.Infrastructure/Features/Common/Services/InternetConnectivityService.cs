using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for checking and monitoring internet connectivity.
    /// </summary>
    public class InternetConnectivityService : IInternetConnectivityService
    {
        // List of reliable domains to check for internet connectivity
        private static readonly string[] _connectivityCheckUrls = new string[]
        {
            "https://www.microsoft.com",
            "https://www.google.com",
            "https://www.cloudflare.com",
        };

        // Timeout for connectivity check requests (per-request, does not mutate shared HttpClient)
        private static readonly TimeSpan _connectivityCheckTimeout = TimeSpan.FromSeconds(5);

        // HttpClient for internet connectivity checks
        private readonly HttpClient _httpClient;

        // Cache the internet connectivity status to avoid frequent checks
        private bool? _cachedInternetStatus = null;
        private DateTime _lastInternetCheckTime = DateTime.MinValue;
        private readonly TimeSpan _internetStatusCacheDuration = TimeSpan.FromSeconds(10); // Cache for 10 seconds
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="InternetConnectivityService"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public InternetConnectivityService(ILogService logService, HttpClient httpClient)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Asynchronously checks if the system has an active internet connection.
        /// </summary>
        /// <param name="forceCheck">If true, bypasses the cache and performs a fresh check.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if internet is connected, false otherwise.</returns>
        public async Task<bool> IsInternetConnectedAsync(
            bool forceCheck = false,
            CancellationToken cancellationToken = default,
            bool userInitiatedCancellation = false
        )
        {
            try
            {
                // Check for cancellation before doing anything
                if (cancellationToken.IsCancellationRequested)
                {
                    _logService.LogInformation(userInitiatedCancellation ? "Internet connectivity check was cancelled by user" : "Internet connectivity check was cancelled as part of operation cancellation");
                    throw new OperationCanceledException(cancellationToken);
                }

                // Return cached result if it's still valid and force check is not requested
                if (
                    !forceCheck
                    && _cachedInternetStatus.HasValue
                    && (DateTime.Now - _lastInternetCheckTime) < _internetStatusCacheDuration
                )
                {
                    _logService.LogInformation(
                        $"Using cached internet connectivity status: {_cachedInternetStatus.Value}"
                    );
                    return _cachedInternetStatus.Value;
                }

                // First check: NetworkInterface.GetIsNetworkAvailable()
                bool isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();
                if (!isNetworkAvailable)
                {
                    _logService.LogInformation(
                        "Network is not available according to NetworkInterface.GetIsNetworkAvailable()"
                    );
                    _cachedInternetStatus = false;
                    _lastInternetCheckTime = DateTime.Now;

                    return false;
                }

                // Second check: Look for known network interface types as a quick-pass
                // optimization. If found, skip straight to HTTP check. If not found
                // (e.g. PPPoE, VPN, cellular), still fall through to the HTTP check
                // instead of returning false — the HTTP check is the real arbiter.
                bool hasKnownInterface = false;
                try
                {
                    // Check for cancellation before network interface check
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logService.LogInformation(userInitiatedCancellation ? "Internet connectivity check was cancelled by user" : "Internet connectivity check was cancelled as part of operation cancellation");
                        throw new OperationCanceledException(cancellationToken);
                    }

                    NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                    foreach (NetworkInterface ni in interfaces)
                    {
                        if (
                            ni.OperationalStatus == OperationalStatus.Up
                            && (
                                ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                                || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                            )
                            && ni.GetIPProperties().GatewayAddresses.Count > 0
                        )
                        {
                            hasKnownInterface = true;
                            break;
                        }
                    }

                    if (!hasKnownInterface)
                    {
                        _logService.LogInformation(
                            "No standard network interfaces (Wi-Fi/Ethernet) with gateway found, "
                            + "falling through to HTTP connectivity check"
                        );
                    }
                }
                catch (OperationCanceledException)
                {
                    // This is a user-initiated cancellation, not a connectivity issue
                    _logService.LogInformation(userInitiatedCancellation ? "Internet connectivity check was cancelled by user" : "Internet connectivity check was cancelled as part of operation cancellation");

                    throw; // Re-throw to be handled by the caller
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Error checking network interfaces: {ex.Message}");
                    // Continue to the next check even if this one fails
                }

                // Third check: Try to connect to reliable domains
                foreach (string url in _connectivityCheckUrls)
                {
                    try
                    {
                        // Check for cancellation before making the request
                        if (cancellationToken.IsCancellationRequested)
                        {
                            // This is a user-initiated cancellation, not a connectivity issue
                            _logService.LogInformation(userInitiatedCancellation ? "Internet connectivity check was cancelled by user" : "Internet connectivity check was cancelled as part of operation cancellation");
                            throw new OperationCanceledException(cancellationToken);
                        }

                        // Make a HEAD request to minimize data transfer, with a per-request timeout
                        // so we don't mutate the shared singleton HttpClient.Timeout
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        cts.CancelAfter(_connectivityCheckTimeout);
                        var request = new HttpRequestMessage(HttpMethod.Head, url);
                        var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

                        if (response.IsSuccessStatusCode)
                        {
                            _logService.LogInformation($"Successfully connected to {url}");
                            _cachedInternetStatus = true;
                            _lastInternetCheckTime = DateTime.Now;

                            return true;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // This is a user-initiated cancellation, not a connectivity issue
                        _logService.LogInformation(userInitiatedCancellation ? "Internet connectivity check was cancelled by user" : "Internet connectivity check was cancelled as part of operation cancellation");

                        throw; // Re-throw to be handled by the caller
                    }
                    catch (Exception ex)
                    {
                        _logService.LogInformation($"Failed to connect to {url}: {ex.Message}");
                        // Try the next URL
                    }
                }

                // If we get here, all connectivity checks failed
                _logService.LogWarning("All internet connectivity checks failed");
                _cachedInternetStatus = false;
                _lastInternetCheckTime = DateTime.Now;

                return false;
            }
            catch (OperationCanceledException)
            {
                // This is a user-initiated cancellation, not a connectivity issue
                _logService.LogInformation("Internet connectivity check was cancelled by user");

                throw; // Re-throw to be handled by the caller
            }
            catch (Exception ex)
            {
                _logService.LogError("Error checking internet connectivity", ex);

                return false;
            }
        }

    }
}

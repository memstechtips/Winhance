using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Fetches sponsor/supporter data from the sponsors branch with a bundled-snapshot fallback.
/// Results are cached for the lifetime of the session.
/// </summary>
public class SponsorsService : ISponsorsService
{
    private const string LiveUrl = "https://raw.githubusercontent.com/memstechtips/Winhance/sponsors/sponsors/sponsors.json";
    private const string UserAgent = "Winhance-Sponsors-Loader";
    private const int TimeoutSeconds = 4;

    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;

    private SponsorsDocument? _cached;
    private readonly SemaphoreSlim _fetchLock = new SemaphoreSlim(1, 1);

    public SponsorsService(HttpClient httpClient, ILogService logService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    /// <inheritdoc/>
    public async Task<SponsorsDocument?> GetSponsorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
            return _cached;

        await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-checked after acquiring the lock.
            if (_cached is not null)
                return _cached;

            SponsorsDocument? result = await TryFetchLiveAsync(cancellationToken).ConfigureAwait(false);

            if (result is null)
                result = await TryLoadBundledAsync().ConfigureAwait(false);

            _cached = result;
            return _cached;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    /// <inheritdoc/>
    public string GetLogoUri(SponsorEntry sponsor)
    {
        string logo = sponsor.Logo ?? string.Empty;
        return $"https://raw.githubusercontent.com/memstechtips/Winhance/sponsors/sponsors/{logo}";
    }

    /// <inheritdoc/>
    public string? GetBundledLogoPath(SponsorEntry sponsor)
    {
        if (string.IsNullOrEmpty(sponsor.Logo))
            return null;

        string logoRelative = sponsor.Logo.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sponsors", logoRelative);

        if (!File.Exists(fullPath))
            return null;

        // Return an ms-appx URI using forward slashes regardless of platform.
        string uriLogo = sponsor.Logo.Replace('\\', '/');
        return $"ms-appx:///Assets/Sponsors/{uriLogo}";
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task<SponsorsDocument?> TryFetchLiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            using var request = new HttpRequestMessage(HttpMethod.Get, LiveUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            HttpResponseMessage response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logService.Log(LogLevel.Warning, $"SponsorsService: live fetch returned {(int)response.StatusCode}. Falling back to bundled snapshot.");
                return null;
            }

            string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            SponsorsDocument? doc = JsonSerializer.Deserialize<SponsorsDocument>(json);
            _logService.Log(LogLevel.Info, "SponsorsService: loaded sponsors from live URL.");
            return doc;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning, $"SponsorsService: live fetch failed ({ex.Message}). Falling back to bundled snapshot.");
            return null;
        }
    }

    private async Task<SponsorsDocument?> TryLoadBundledAsync()
    {
        try
        {
            string bundledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sponsors", "sponsors.json");

            if (!File.Exists(bundledPath))
            {
                _logService.Log(LogLevel.Warning, "SponsorsService: bundled sponsors.json not found. No sponsor data available.");
                return null;
            }

            string json = await File.ReadAllTextAsync(bundledPath).ConfigureAwait(false);
            SponsorsDocument? doc = JsonSerializer.Deserialize<SponsorsDocument>(json);
            _logService.Log(LogLevel.Info, "SponsorsService: loaded sponsors from bundled snapshot.");
            return doc;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"SponsorsService: bundled snapshot load failed ({ex.Message}).", ex);
            return null;
        }
    }
}

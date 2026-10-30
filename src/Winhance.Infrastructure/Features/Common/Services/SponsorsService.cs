using System.Text.Json;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class SponsorsService : ISponsorsService
{
    private const string LiveBase = "https://raw.githubusercontent.com/memstechtips/Winhance/sponsors/sponsors/";
    private const string LiveUrl = LiveBase + "sponsors.json";
    private const string UserAgent = "Winhance-Sponsors-Loader";
    private const int TimeoutSeconds = LiveJsonFeed<SponsorsDocument>.TimeoutSeconds;
    private const string LastGoodFileName = "sponsors.json";
    private const string LogoFolderName = "logos";

    // Winhance can sit in the tray for weeks. A fallback list is older than the live one from the start, so it is
    // replaced sooner.
    private static readonly TimeSpan LiveListLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan FallbackListLifetime = TimeSpan.FromHours(1);

    private static readonly string BundledFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sponsors");
    private static readonly string BundledPath = Path.Combine(BundledFolder, "sponsors.json");

    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;
    private readonly IFileSystemService _fileSystemService;
    private readonly TimeProvider _timeProvider;
    private readonly LiveJsonFeed<SponsorsDocument> _feed;

    private SponsorsDocument? _cached;
    private bool _cachedIsLive;
    private long _cachedAt;
    private SponsorsDocument? _fetchedLive;
    private bool _liveListChanged;
    private readonly SemaphoreSlim _fetchLock = new SemaphoreSlim(1, 1);

    public SponsorsService(
        HttpClient httpClient,
        ILogService logService,
        IFileSystemService fileSystemService,
        IInteractiveUserService interactiveUserService,
        TimeProvider timeProvider)
        : this(httpClient, logService, fileSystemService, interactiveUserService, timeProvider, delay: null)
    {
    }

    // Test seam: lets the suite run the retries without real delays.
    internal SponsorsService(
        HttpClient httpClient,
        ILogService logService,
        IFileSystemService fileSystemService,
        IInteractiveUserService interactiveUserService,
        TimeProvider timeProvider,
        Func<int, CancellationToken, Task>? delay)
    {
        _httpClient = httpClient;
        _logService = logService;
        _fileSystemService = fileSystemService;
        _timeProvider = timeProvider;
        _feed = new LiveJsonFeed<SponsorsDocument>(
            httpClient, logService, fileSystemService, interactiveUserService, delay,
            "sponsors", LiveUrl, UserAgent, LastGoodFileName,
            json => JsonSerializer.Deserialize<SponsorsDocument>(json));
    }

    // Test seam: the refetch GetSponsorsAsync last started, so a test can wait for it.
    internal Task Refresh { get; private set; } = Task.CompletedTask;

    public async Task<SponsorsDocument?> GetSponsorsAsync(CancellationToken cancellationToken = default)
    {
        // The exit dialog waits on this, so an old list is returned as it is and replaced behind the call: the
        // next caller gets the new one.
        if (_cached is { } cached)
        {
            if (IsStale())
                Refresh = RefreshAsync();

            return cached;
        }

        await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cached is not null)
                return _cached;

            // The bundled snapshot is as old as the release, so the last list this PC fetched is the better fallback.
            var live = await TryFetchLiveAsync(cancellationToken).ConfigureAwait(false);
            var document = live
                ?? await _feed.LoadLastGoodAsync().ConfigureAwait(false)
                ?? await _feed.LoadAsync(BundledPath, "the bundled snapshot").ConfigureAwait(false);

            if (document is null)
                _logService.Log(LogLevel.Warning, "No sponsor data available.");

            _cachedIsLive = live is not null;
            _cachedAt = _timeProvider.GetTimestamp();
            _cached = document;
            return document;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    public async Task<SponsorsDocument?> GetSnapshotAsync()
    {
        return _cached
            ?? await _feed.LoadLastGoodAsync().ConfigureAwait(false)
            ?? await _feed.LoadAsync(BundledPath, "the bundled snapshot").ConfigureAwait(false);
    }

    // A logo is re-downloaded only when the live list changed since the last fetch or its file is missing, so a
    // changed logo needs a sponsors.json change too (the README's "bump updated" step).
    public async Task CacheLogosAsync(CancellationToken cancellationToken = default)
    {
        if (_fetchedLive is null)
            return;

        string folder = _fileSystemService.CombinePath(_feed.LastGoodFolder, LogoFolderName);
        foreach (string logo in _fetchedLive.Sponsors.Select(s => s.Logo).OfType<string>().Where(l => l.Length > 0).Distinct())
        {
            string path = _fileSystemService.CombinePath(folder, Path.GetFileName(logo));
            if (!_liveListChanged && _fileSystemService.FileExists(path))
                continue;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
                using var request = new HttpRequestMessage(HttpMethod.Get, LiveBase + logo);
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

                using HttpResponseMessage response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logService.Log(LogLevel.Warning, $"Sponsor logo {logo} returned {(int)response.StatusCode}.");
                    continue;
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
                _fileSystemService.CreateDirectory(folder);
                await _fileSystemService.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Warning, $"Could not cache sponsor logo {logo} ({ex.Message}).");
            }
        }
    }

    public IReadOnlyList<string> GetLogoSources(SponsorEntry sponsor)
    {
        if (string.IsNullOrEmpty(sponsor.Logo))
            return [];

        var sources = new List<string>();

        string cached = _fileSystemService.CombinePath(_feed.LastGoodFolder, LogoFolderName, Path.GetFileName(sponsor.Logo));
        if (_fileSystemService.FileExists(cached))
            sources.Add(new Uri(cached).AbsoluteUri);

        if (_fileSystemService.FileExists(Path.Combine(BundledFolder, sponsor.Logo.Replace('/', Path.DirectorySeparatorChar))))
            sources.Add("ms-appx:///Assets/Sponsors/" + sponsor.Logo.Replace('\\', '/'));

        sources.Add(LiveBase + sponsor.Logo);
        return sources;
    }

    private bool IsStale() =>
        _timeProvider.GetElapsedTime(_cachedAt) >= (_cachedIsLive ? LiveListLifetime : FallbackListLifetime);

    private async Task RefreshAsync()
    {
        if (!await _fetchLock.WaitAsync(0).ConfigureAwait(false))
            return;

        try
        {
            if (!IsStale())
                return;

            // A live list that could not be renewed is kept, and counts as a fallback from here on.
            var live = await TryFetchLiveAsync(CancellationToken.None).ConfigureAwait(false);
            _cachedIsLive = live is not null;
            _cachedAt = _timeProvider.GetTimestamp();
            if (live is not null)
                _cached = live;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<SponsorsDocument?> TryFetchLiveAsync(CancellationToken cancellationToken)
    {
        var fetch = await _feed.FetchAsync(cancellationToken).ConfigureAwait(false);
        if (fetch is null)
            return null;

        _liveListChanged = fetch.PreviousJson != fetch.Json;
        _fetchedLive = fetch.Value;
        return fetch.Value;
    }
}

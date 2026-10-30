using System.Text.Json;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class WindowsUpdateListService : IWindowsUpdateListService
{
    private const string LiveUrl = "https://raw.githubusercontent.com/memstechtips/Winhance/updates/updates/updates.json";
    private const string UserAgent = "Winhance-Updates-Loader";
    private const int TimeoutSeconds = 4;
    private const string LastGoodFileName = "updates.json";

    // raw.githubusercontent.com's 503s flap per cache node (measured 2026-09-09, see SponsorsService): most clear
    // on the very next request. Only a failure that comes back fast is retried; a timeout already cost 4 s.
    private static readonly int[] RetryDelaysMs = [500, 1000];

    private static readonly TimeSpan LiveListLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan FallbackListLifetime = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string BundledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Updates", "updates.json");

    private readonly HttpClient _httpClient;
    private readonly ILogService _logService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly TimeProvider _timeProvider;
    private readonly Func<int, CancellationToken, Task> _delay;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private IReadOnlyList<WindowsUpdateBuild>? _builds;
    private bool _buildsAreLive;
    private long _loadedAt;

    public WindowsUpdateListService(
        HttpClient httpClient,
        ILogService logService,
        IFileSystemService fileSystemService,
        IInteractiveUserService interactiveUserService,
        TimeProvider timeProvider)
        : this(httpClient, logService, fileSystemService, interactiveUserService, timeProvider, delay: null)
    {
    }

    // Test seam: lets the suite run the retries without real delays.
    internal WindowsUpdateListService(
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
        _interactiveUserService = interactiveUserService;
        _timeProvider = timeProvider;
        _delay = delay ?? ((ms, token) => Task.Delay(ms, token));
    }

    public async Task<WindowsUpdate?> FindUpdateAsync(int build, int ubr, string architecture, CancellationToken cancellationToken = default)
    {
        try
        {
            var builds = await GetBuildsAsync(cancellationToken).ConfigureAwait(false);
            return builds
                .Where(b => b.Build == build && string.Equals(b.Arch, architecture, StringComparison.OrdinalIgnoreCase))
                .SelectMany(b => b.Updates)
                .Where(u => u.TargetUbr > ubr)
                .OrderByDescending(u => u.TargetUbr)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not look up a Windows update ({ex.Message}).");
            return null;
        }
    }

    private async Task<IReadOnlyList<WindowsUpdateBuild>> GetBuildsAsync(CancellationToken cancellationToken)
    {
        if (_builds is not null && !IsStale())
            return _builds;

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_builds is not null && !IsStale())
                return _builds;

            var live = await TryFetchLiveAsync(cancellationToken).ConfigureAwait(false);
            var list = live ?? await LoadNewestOfflineAsync().ConfigureAwait(false);

            if (list is null)
                _logService.LogWarning("No Windows update list available.");

            // A list that could not be renewed is kept rather than replaced by nothing.
            if (list is not null || _builds is null)
            {
                _builds = list is null ? [] : WindowsUpdateListValidator.Validate(list, _logService);
                _buildsAreLive = live is not null;
            }

            _loadedAt = _timeProvider.GetTimestamp();
            return _builds!;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    // An app update can ship a bundled snapshot newer than the last list this machine fetched.
    private async Task<WindowsUpdateList?> LoadNewestOfflineAsync()
    {
        var lastGood = await TryLoadAsync(_fileSystemService.CombinePath(LastGoodFolder(), LastGoodFileName), "the last good fetch").ConfigureAwait(false);
        var bundled = await TryLoadAsync(BundledPath, "the bundled snapshot").ConfigureAwait(false);
        if (lastGood is null || bundled is null)
            return lastGood ?? bundled;

        return bundled.GeneratedAt > lastGood.GeneratedAt ? bundled : lastGood;
    }

    private bool IsStale() =>
        _timeProvider.GetElapsedTime(_loadedAt) >= (_buildsAreLive ? LiveListLifetime : FallbackListLifetime);

    private async Task<WindowsUpdateList?> TryFetchLiveAsync(CancellationToken cancellationToken)
    {
        foreach (int delayMs in RetryDelaysMs)
        {
            var (list, retry) = await TryFetchOnceAsync(cancellationToken).ConfigureAwait(false);
            if (list is not null || !retry)
                return list;

            await _delay(delayMs, cancellationToken).ConfigureAwait(false);
        }

        return (await TryFetchOnceAsync(cancellationToken).ConfigureAwait(false)).List;
    }

    private async Task<(WindowsUpdateList? List, bool Retry)> TryFetchOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            using var request = new HttpRequestMessage(HttpMethod.Get, LiveUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logService.LogWarning($"Live Windows update list fetch returned {(int)response.StatusCode}.");
                return (null, (int)response.StatusCode >= 500);
            }

            string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            if (Parse(json) is not { } list)
            {
                _logService.LogWarning("Live Windows update list is not a schema 1 list.");
                return (null, false);
            }

            _logService.LogInformation("Loaded the Windows update list from the live URL.");
            await SaveLastGoodAsync(json).ConfigureAwait(false);
            return (list, false);
        }
        catch (HttpRequestException ex)
        {
            _logService.LogWarning($"Live Windows update list fetch failed ({ex.Message}).");
            return (null, true);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Live Windows update list fetch failed ({ex.Message}).");
            return (null, false);
        }
    }

    private static WindowsUpdateList? Parse(string json) =>
        JsonSerializer.Deserialize<WindowsUpdateList>(json, JsonOptions) is { Schema: 1 } list ? list : null;

    private async Task SaveLastGoodAsync(string json)
    {
        try
        {
            string folder = LastGoodFolder();
            _fileSystemService.CreateDirectory(folder);
            await _fileSystemService.WriteAllTextAsync(_fileSystemService.CombinePath(folder, LastGoodFileName), json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not save the fetched Windows update list ({ex.Message}).");
        }
    }

    private async Task<WindowsUpdateList?> TryLoadAsync(string path, string source)
    {
        try
        {
            if (!_fileSystemService.FileExists(path))
                return null;

            string json = await _fileSystemService.ReadAllTextAsync(path).ConfigureAwait(false);
            if (Parse(json) is not { } list)
            {
                _logService.LogWarning($"The Windows update list from {source} is not a schema 1 list.");
                return null;
            }

            _logService.LogInformation($"Loaded the Windows update list from {source}.");
            return list;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not load the Windows update list from {source} ({ex.Message}).");
            return null;
        }
    }

    private string LastGoodFolder() => _fileSystemService.CombinePath(
        AppDataPaths.UserRoot(_interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        "Cache");
}

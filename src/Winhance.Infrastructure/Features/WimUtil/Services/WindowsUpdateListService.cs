using System.Text.Json;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class WindowsUpdateListService : IWindowsUpdateListService
{
    private const string LiveUrl = "https://raw.githubusercontent.com/memstechtips/Winhance/updates/updates/updates.json";
    private const string UserAgent = "Winhance-Updates-Loader";
    private const string LastGoodFileName = "updates.json";

    private static readonly TimeSpan LiveListLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan FallbackListLifetime = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string BundledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Updates", "updates.json");

    private readonly ILogService _logService;
    private readonly TimeProvider _timeProvider;
    private readonly LiveJsonFeed<WindowsUpdateList> _feed;
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
        _logService = logService;
        _timeProvider = timeProvider;
        _feed = new LiveJsonFeed<WindowsUpdateList>(
            httpClient, logService, fileSystemService, interactiveUserService, delay,
            "Windows update list", LiveUrl, UserAgent, LastGoodFileName, Parse);
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

            var live = (await _feed.FetchAsync(cancellationToken).ConfigureAwait(false))?.Value;
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
        var lastGood = await _feed.LoadLastGoodAsync().ConfigureAwait(false);
        var bundled = await _feed.LoadAsync(BundledPath, "the bundled snapshot").ConfigureAwait(false);
        if (lastGood is null || bundled is null)
            return lastGood ?? bundled;

        return bundled.GeneratedAt > lastGood.GeneratedAt ? bundled : lastGood;
    }

    private bool IsStale() =>
        _timeProvider.GetElapsedTime(_loadedAt) >= (_buildsAreLive ? LiveListLifetime : FallbackListLifetime);

    private static WindowsUpdateList? Parse(string json) =>
        JsonSerializer.Deserialize<WindowsUpdateList>(json, JsonOptions) is { Schema: 1 } list ? list : null;
}

using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed record LiveJsonFetch<T>(T Value, string Json, string? PreviousJson);

// A JSON list fetched from a raw.githubusercontent.com URL, with the last good copy kept in the shared Cache
// folder. What counts as valid and how long a list stays fresh is up to the caller.
internal sealed class LiveJsonFeed<T>(
    HttpClient httpClient,
    ILogService logService,
    IFileSystemService fileSystemService,
    IInteractiveUserService interactiveUserService,
    Func<int, CancellationToken, Task>? delay,
    string name,
    string url,
    string userAgent,
    string lastGoodFileName,
    Func<string, T?> parse) where T : class
{
    public const int TimeoutSeconds = 4;

    // raw.githubusercontent.com's 503s flap per cache node (measured 2026-09-09): most clear on the very next request.
    // Only a failure that comes back fast is retried; a timeout already cost 4 s, and the exit dialog waits on this.
    private static readonly int[] RetryDelaysMs = [500, 1000];

    private readonly Func<int, CancellationToken, Task> _delay = delay ?? ((ms, token) => Task.Delay(ms, token));

    public string LastGoodFolder => fileSystemService.CombinePath(
        AppDataPaths.UserRoot(interactiveUserService.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        "Cache");

    public async Task<LiveJsonFetch<T>?> FetchAsync(CancellationToken cancellationToken)
    {
        foreach (int delayMs in RetryDelaysMs)
        {
            var (fetch, retry) = await FetchOnceAsync(cancellationToken).ConfigureAwait(false);
            if (fetch is not null || !retry)
                return fetch;

            await _delay(delayMs, cancellationToken).ConfigureAwait(false);
        }

        return (await FetchOnceAsync(cancellationToken).ConfigureAwait(false)).Fetch;
    }

    public Task<T?> LoadLastGoodAsync() =>
        LoadAsync(fileSystemService.CombinePath(LastGoodFolder, lastGoodFileName), "the last good fetch");

    public async Task<T?> LoadAsync(string path, string source)
    {
        try
        {
            if (!fileSystemService.FileExists(path))
                return null;

            string json = await fileSystemService.ReadAllTextAsync(path).ConfigureAwait(false);
            if (parse(json) is not { } value)
            {
                logService.Log(LogLevel.Warning, $"The {name} from {source} is not valid.");
                return null;
            }

            logService.Log(LogLevel.Info, $"Loaded the {name} from {source}.");
            return value;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not load the {name} from {source} ({ex.Message}).");
            return null;
        }
    }

    private async Task<(LiveJsonFetch<T>? Fetch, bool Retry)> FetchOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logService.Log(LogLevel.Warning, $"Live {name} fetch returned {(int)response.StatusCode}.");
                return (null, (int)response.StatusCode >= 500);
            }

            string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            if (parse(json) is not { } value)
            {
                logService.Log(LogLevel.Warning, $"The live {name} is not valid.");
                return (null, false);
            }

            logService.Log(LogLevel.Info, $"Loaded the {name} from the live URL.");
            string? previous = await ReadLastGoodTextAsync().ConfigureAwait(false);
            await SaveLastGoodAsync(json).ConfigureAwait(false);
            return (new LiveJsonFetch<T>(value, json, previous), false);
        }
        catch (HttpRequestException ex)
        {
            logService.Log(LogLevel.Warning, $"Live {name} fetch failed ({ex.Message}).");
            return (null, true);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Live {name} fetch failed ({ex.Message}).");
            return (null, false);
        }
    }

    private async Task SaveLastGoodAsync(string json)
    {
        try
        {
            string folder = LastGoodFolder;
            fileSystemService.CreateDirectory(folder);
            await fileSystemService.WriteAllTextAsync(fileSystemService.CombinePath(folder, lastGoodFileName), json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not save the fetched {name} ({ex.Message}).");
        }
    }

    private async Task<string?> ReadLastGoodTextAsync()
    {
        try
        {
            string path = fileSystemService.CombinePath(LastGoodFolder, lastGoodFileName);
            return fileSystemService.FileExists(path) ? await fileSystemService.ReadAllTextAsync(path).ConfigureAwait(false) : null;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not read the saved {name} ({ex.Message}).");
            return null;
        }
    }
}

using System.Diagnostics;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Utilities;

internal static class DismSessionManager
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    // Native DISM calls cannot be cancelled via CancellationToken, so Task.WhenAny abandons the blocking thread past this deadline.
    private const int HardTimeoutSeconds = 30;

    // DismGetImageInfo reads an image file directly and takes no session, so opening the online
    // one would spend seconds of servicing-stack setup to read a WIM header. It still has to hold
    // the same lock, because DismInitialize is per-process and a second call fails.
    public static T ExecuteWithoutSession<T>(Func<T> action)
    {
        _lock.Wait();
        try
        {
            DismApi.ThrowIfFailed(DismApi.DismInitialize(DismApi.DismLogErrors, null, null), "Initialize");
            try
            {
                return action();
            }
            finally
            {
                _ = DismApi.DismShutdown();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public static async Task<T> ExecuteAsync<T>(
        Func<uint, T> action,
        CancellationToken ct = default,
        ILogService? log = null)
    {
        var sw = Stopwatch.StartNew();
        log?.LogDebug("Waiting for semaphore...");
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        log?.LogDebug($"Semaphore acquired ({sw.ElapsedMilliseconds}ms). Thread={Environment.CurrentManagedThreadId}");
        try
        {
            var workTask = Task.Run(() =>
            {
                log?.LogDebug($"Task.Run started. Thread={Environment.CurrentManagedThreadId}");

                var initSw = Stopwatch.StartNew();
                log?.LogDebug("Calling DismInitialize...");
                var hr = DismApi.DismInitialize(DismApi.DismLogErrors, null, null);
                log?.LogDebug($"DismInitialize returned 0x{hr:X8} ({initSw.ElapsedMilliseconds}ms)");
                DismApi.ThrowIfFailed(hr, "Initialize");

                try
                {
                    log?.LogDebug("Calling DismOpenSession...");
                    var openSw = Stopwatch.StartNew();
                    hr = DismApi.DismOpenSession(DismApi.DISM_ONLINE_IMAGE_PATH, null, null, out uint session);
                    log?.LogDebug($"DismOpenSession returned 0x{hr:X8}, session={session} ({openSw.ElapsedMilliseconds}ms)");
                    DismApi.ThrowIfFailed(hr, "OpenSession");

                    try
                    {
                        log?.LogDebug("Executing action...");
                        var actionSw = Stopwatch.StartNew();
                        var result = action(session);
                        log?.LogDebug($"Action completed ({actionSw.ElapsedMilliseconds}ms)");
                        return result;
                    }
                    finally
                    {
                        log?.LogDebug("Calling DismCloseSession...");
                        _ = DismApi.DismCloseSession(session);
                        log?.LogDebug("DismCloseSession done");
                    }
                }
                finally
                {
                    log?.LogDebug("Calling DismShutdown...");
                    _ = DismApi.DismShutdown();
                    log?.LogDebug("DismShutdown done");
                }
            }, ct);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(HardTimeoutSeconds), ct);

            if (await Task.WhenAny(workTask, timeoutTask).ConfigureAwait(false) == timeoutTask)
            {
                log?.LogDebug($"HARD TIMEOUT after {HardTimeoutSeconds}s — native DISM call is unresponsive, abandoning thread");
                throw new OperationCanceledException($"DISM operation timed out after {HardTimeoutSeconds}s");
            }

            return await workTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            log?.LogDebug($"Operation cancelled/timed out in ExecuteAsync<T>");
            throw;
        }
        catch (Exception ex)
        {
            log?.LogDebug($"EXCEPTION in ExecuteAsync<T>: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        finally
        {
            _lock.Release();
            log?.LogDebug($"Semaphore released. Total elapsed={sw.ElapsedMilliseconds}ms");
        }
    }

    public static async Task ExecuteAsync(
        Action<uint> action,
        CancellationToken ct = default,
        ILogService? log = null)
    {
        var sw = Stopwatch.StartNew();
        log?.LogDebug("Waiting for semaphore...");
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        log?.LogDebug($"Semaphore acquired ({sw.ElapsedMilliseconds}ms). Thread={Environment.CurrentManagedThreadId}");
        try
        {
            var workTask = Task.Run(() =>
            {
                log?.LogDebug($"Task.Run started. Thread={Environment.CurrentManagedThreadId}");

                var initSw = Stopwatch.StartNew();
                log?.LogDebug("Calling DismInitialize...");
                var hr = DismApi.DismInitialize(DismApi.DismLogErrors, null, null);
                log?.LogDebug($"DismInitialize returned 0x{hr:X8} ({initSw.ElapsedMilliseconds}ms)");
                DismApi.ThrowIfFailed(hr, "Initialize");

                try
                {
                    log?.LogDebug("Calling DismOpenSession...");
                    var openSw = Stopwatch.StartNew();
                    hr = DismApi.DismOpenSession(DismApi.DISM_ONLINE_IMAGE_PATH, null, null, out uint session);
                    log?.LogDebug($"DismOpenSession returned 0x{hr:X8}, session={session} ({openSw.ElapsedMilliseconds}ms)");
                    DismApi.ThrowIfFailed(hr, "OpenSession");

                    try
                    {
                        log?.LogDebug("Executing action...");
                        var actionSw = Stopwatch.StartNew();
                        action(session);
                        log?.LogDebug($"Action completed ({actionSw.ElapsedMilliseconds}ms)");
                    }
                    finally
                    {
                        log?.LogDebug("Calling DismCloseSession...");
                        _ = DismApi.DismCloseSession(session);
                        log?.LogDebug("DismCloseSession done");
                    }
                }
                finally
                {
                    log?.LogDebug("Calling DismShutdown...");
                    _ = DismApi.DismShutdown();
                    log?.LogDebug("DismShutdown done");
                }
            }, ct);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(HardTimeoutSeconds), ct);

            if (await Task.WhenAny(workTask, timeoutTask).ConfigureAwait(false) == timeoutTask)
            {
                log?.LogDebug($"HARD TIMEOUT after {HardTimeoutSeconds}s — native DISM call is unresponsive, abandoning thread");
                throw new OperationCanceledException($"DISM operation timed out after {HardTimeoutSeconds}s");
            }

            await workTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            log?.LogDebug($"Operation cancelled/timed out in ExecuteAsync");
            throw;
        }
        catch (Exception ex)
        {
            log?.LogDebug($"EXCEPTION in ExecuteAsync: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        finally
        {
            _lock.Release();
            log?.LogDebug($"Semaphore released. Total elapsed={sw.ElapsedMilliseconds}ms");
        }
    }
}

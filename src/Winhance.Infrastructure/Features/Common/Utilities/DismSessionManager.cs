using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Utilities;

internal static class DismSessionManager
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static async Task<T> ExecuteAsync<T>(
        Func<uint, T> action,
        CancellationToken ct = default,
        Action<string>? log = null)
    {
        var sw = Stopwatch.StartNew();
        log?.Invoke("[DismSession] Waiting for semaphore...");
        await _lock.WaitAsync(ct);
        log?.Invoke($"[DismSession] Semaphore acquired ({sw.ElapsedMilliseconds}ms). Thread={Environment.CurrentManagedThreadId}");
        try
        {
            return await Task.Run(() =>
            {
                log?.Invoke($"[DismSession] Task.Run started. Thread={Environment.CurrentManagedThreadId}");

                var initSw = Stopwatch.StartNew();
                log?.Invoke("[DismSession] Calling DismInitialize...");
                var hr = DismApi.DismInitialize(DismApi.DismLogErrors, null, null);
                log?.Invoke($"[DismSession] DismInitialize returned 0x{hr:X8} ({initSw.ElapsedMilliseconds}ms)");
                DismApi.ThrowIfFailed(hr, "Initialize");

                try
                {
                    log?.Invoke("[DismSession] Calling DismOpenSession...");
                    var openSw = Stopwatch.StartNew();
                    hr = DismApi.DismOpenSession(DismApi.DISM_ONLINE_IMAGE_PATH, null, null, out uint session);
                    log?.Invoke($"[DismSession] DismOpenSession returned 0x{hr:X8}, session={session} ({openSw.ElapsedMilliseconds}ms)");
                    DismApi.ThrowIfFailed(hr, "OpenSession");

                    try
                    {
                        log?.Invoke("[DismSession] Executing action...");
                        var actionSw = Stopwatch.StartNew();
                        var result = action(session);
                        log?.Invoke($"[DismSession] Action completed ({actionSw.ElapsedMilliseconds}ms)");
                        return result;
                    }
                    finally
                    {
                        log?.Invoke("[DismSession] Calling DismCloseSession...");
                        DismApi.DismCloseSession(session);
                        log?.Invoke("[DismSession] DismCloseSession done");
                    }
                }
                finally
                {
                    log?.Invoke("[DismSession] Calling DismShutdown...");
                    DismApi.DismShutdown();
                    log?.Invoke("[DismSession] DismShutdown done");
                }
            }, ct);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[DismSession] EXCEPTION in ExecuteAsync<T>: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        finally
        {
            _lock.Release();
            log?.Invoke($"[DismSession] Semaphore released. Total elapsed={sw.ElapsedMilliseconds}ms");
        }
    }

    public static async Task ExecuteAsync(
        Action<uint> action,
        CancellationToken ct = default,
        Action<string>? log = null)
    {
        var sw = Stopwatch.StartNew();
        log?.Invoke("[DismSession] Waiting for semaphore...");
        await _lock.WaitAsync(ct);
        log?.Invoke($"[DismSession] Semaphore acquired ({sw.ElapsedMilliseconds}ms). Thread={Environment.CurrentManagedThreadId}");
        try
        {
            await Task.Run(() =>
            {
                log?.Invoke($"[DismSession] Task.Run started. Thread={Environment.CurrentManagedThreadId}");

                var initSw = Stopwatch.StartNew();
                log?.Invoke("[DismSession] Calling DismInitialize...");
                var hr = DismApi.DismInitialize(DismApi.DismLogErrors, null, null);
                log?.Invoke($"[DismSession] DismInitialize returned 0x{hr:X8} ({initSw.ElapsedMilliseconds}ms)");
                DismApi.ThrowIfFailed(hr, "Initialize");

                try
                {
                    log?.Invoke("[DismSession] Calling DismOpenSession...");
                    var openSw = Stopwatch.StartNew();
                    hr = DismApi.DismOpenSession(DismApi.DISM_ONLINE_IMAGE_PATH, null, null, out uint session);
                    log?.Invoke($"[DismSession] DismOpenSession returned 0x{hr:X8}, session={session} ({openSw.ElapsedMilliseconds}ms)");
                    DismApi.ThrowIfFailed(hr, "OpenSession");

                    try
                    {
                        log?.Invoke("[DismSession] Executing action...");
                        var actionSw = Stopwatch.StartNew();
                        action(session);
                        log?.Invoke($"[DismSession] Action completed ({actionSw.ElapsedMilliseconds}ms)");
                    }
                    finally
                    {
                        log?.Invoke("[DismSession] Calling DismCloseSession...");
                        DismApi.DismCloseSession(session);
                        log?.Invoke("[DismSession] DismCloseSession done");
                    }
                }
                finally
                {
                    log?.Invoke("[DismSession] Calling DismShutdown...");
                    DismApi.DismShutdown();
                    log?.Invoke("[DismSession] DismShutdown done");
                }
            }, ct);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[DismSession] EXCEPTION in ExecuteAsync: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        finally
        {
            _lock.Release();
            log?.Invoke($"[DismSession] Semaphore released. Total elapsed={sw.ElapsedMilliseconds}ms");
        }
    }

    public static ManualResetEvent CreateCancelEvent(CancellationToken ct)
    {
        var cancelEvent = new ManualResetEvent(false);
        if (ct.CanBeCanceled)
        {
            ct.Register(() => cancelEvent.Set());
        }
        return cancelEvent;
    }
}

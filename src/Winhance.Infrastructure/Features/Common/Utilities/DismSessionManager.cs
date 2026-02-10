using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dism;

namespace Winhance.Infrastructure.Features.Common.Utilities;

internal static class DismSessionManager
{
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static async Task<T> ExecuteAsync<T>(
        Func<DismSession, T> action,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            DismApi.Initialize(DismLogLevel.LogErrors);
            try
            {
                using var session = DismApi.OpenOnlineSession();
                return action(session);
            }
            finally
            {
                DismApi.Shutdown();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public static async Task ExecuteAsync(
        Action<DismSession> action,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            DismApi.Initialize(DismLogLevel.LogErrors);
            try
            {
                using var session = DismApi.OpenOnlineSession();
                action(session);
            }
            finally
            {
                DismApi.Shutdown();
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}

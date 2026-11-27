using System.Runtime.CompilerServices;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Extensions;

public static class TaskExtensions
{
    public static async void FireAndForget(this Task task, ILogService logService,
        [CallerMemberName] string? callerName = null, [CallerFilePath] string callerFilePath = "")
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Unobserved exception in {callerName}: {ex.Message}", callerFilePath);
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Extensions
{
    public static class TaskExtensions
    {
        public static async void FireAndForget(this Task task, ILogService? logService = null,
            [CallerMemberName] string? callerName = null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logService?.LogDebug($"[FireAndForget] Unobserved exception in {callerName}: {ex.Message}");
            }
        }
    }
}

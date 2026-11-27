using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Extensions;

public static class TaskProgressServiceExtensions
{
    public static CancellationToken GetCurrentCancellationToken(this ITaskProgressService? taskProgressService)
        => taskProgressService?.CurrentTaskCancellationSource?.Token ?? CancellationToken.None;
}

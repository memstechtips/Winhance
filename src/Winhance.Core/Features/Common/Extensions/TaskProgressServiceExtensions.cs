using System.Threading;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Extensions;

/// <summary>
/// Convenience extensions over <see cref="ITaskProgressService"/>.
/// </summary>
public static class TaskProgressServiceExtensions
{
    /// <summary>
    /// The cancellation token of the in-progress task, or <see cref="CancellationToken.None"/>
    /// when no task is running.
    /// </summary>
    public static CancellationToken GetCurrentCancellationToken(this ITaskProgressService? taskProgressService)
        => taskProgressService?.CurrentTaskCancellationSource?.Token ?? CancellationToken.None;
}

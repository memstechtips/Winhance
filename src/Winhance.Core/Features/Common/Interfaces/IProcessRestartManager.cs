using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IProcessRestartManager
{
    Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting);

    /// <summary>
    /// Coalesces restarts across a batch of applied settings: each distinct
    /// RestartProcess / RestartService is triggered exactly once, regardless of
    /// how many settings in the batch declared it. Call this AFTER the batch,
    /// outside any SuppressRestarts scope.
    /// </summary>
    Task FlushCoalescedRestartsAsync(IEnumerable<SettingDefinition> appliedSettings);

    /// <summary>
    /// Suppresses all process/service restarts until the returned scope is disposed.
    /// Used by the dependency resolver when auto-enabling multiple children,
    /// so that a single restart from the parent covers all of them.
    /// </summary>
    IDisposable SuppressRestarts();
}

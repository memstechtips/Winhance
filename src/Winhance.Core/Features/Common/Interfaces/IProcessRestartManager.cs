using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IProcessRestartManager
{
    /// <summary>Single-setting restart: reads the unified ApplyBehavior.Restart (RestartProcess / RestartService).
    /// No setting sets both a process and a service restart.</summary>
    Task HandleProcessAndServiceRestartsAsync(Setting setting);

    /// <summary>Batch flush: reads the unified ApplyBehavior.Restart. The apply-cluster
    /// (RecommendedSettingsApplier / BulkSettingsActionService) uses this. No setting sets both a process and
    /// a service restart.</summary>
    Task FlushCoalescedRestartsAsync(IEnumerable<Setting> appliedSettings);

    /// <summary>
    /// Suppresses all process/service restarts until the returned scope is disposed.
    /// Used by the dependency resolver when auto-enabling multiple children,
    /// so that a single restart from the parent covers all of them.
    /// </summary>
    IDisposable SuppressRestarts();
}

using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IProcessRestartManager
{
    // No setting sets both a process and a service restart.
    Task HandleProcessAndServiceRestartsAsync(Setting setting);

    Task FlushCoalescedRestartsAsync(IEnumerable<Setting> appliedSettings);

    // Used when auto-enabling multiple children, so a single restart from the parent covers all of them.
    IDisposable SuppressRestarts();

    // Broadcasts requested inside the scope go out once, when it ends, carrying every notice they asked for.
    IDisposable CoalesceBroadcasts();
}

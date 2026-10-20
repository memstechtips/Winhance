using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IAppInstallationService
{
    Task<OperationResult<bool>> InstallAppAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null, bool shouldRemoveFromBloatScript = true);

    // For a PC Winhance's answer file just set up: the installer WIMUtil staged on the media first, then the online
    // route asking for a machine-wide install.
    Task<OperationResult<bool>> InstallOnNewPcAsync(ItemDefinition app, IProgress<TaskProgressDetail>? progress = null);

    // Features and capabilities share this one entry point so they cannot open two servicing windows.
    // It dispatches the whole batch to one PowerShell window and reports DeferredSuccess: the outcome
    // belongs to that window, so the caller must not mark anything installed.
    Task<OperationResult<bool>> EnableServicingBatchAsync(IReadOnlyList<ItemDefinition> apps, IProgress<TaskProgressDetail>? progress = null);
}

using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

// Builds the drivers and the update staged for Setup into install.wim index 1. Whatever cannot go in
// stays staged with its answer-file step, so Setup installs it instead.
public interface IOfflineServicingService
{
    bool HasStagedItems(string workingDirectory);

    bool CanMountFor(string workingDirectory);

    Task<OfflineServicingResult> ServiceAsync(
        string workingDirectory,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}

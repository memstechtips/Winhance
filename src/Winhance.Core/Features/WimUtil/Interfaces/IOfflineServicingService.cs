using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

// Builds the drivers, the update, the chosen apps and settings and the cleanup into the install image. Whatever
// cannot go in stays staged with its answer-file step, so Setup does it instead.
public interface IOfflineServicingService
{
    bool HasStagedItems(string workingDirectory);

    bool CanMountFor(string workingDirectory);

    // The phases ServiceAsync will run for this request, in order; reads the working folder but changes nothing.
    IReadOnlyList<ServicingPhase> PhasesFor(OfflineBuildRequest request);

    Task<OfflineServicingResult> ServiceAsync(
        OfflineBuildRequest request,
        IProgress<TaskProgressDetail>? progress,
        IProgress<ServicingPhaseUpdate>? phases,
        CancellationToken cancellationToken);
}

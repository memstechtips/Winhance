using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// Removes the chosen apps from an image that is already mounted at mountDir.
internal interface IOfflineAppRemover
{
    Task<OfflineStepOutcome> RemoveAsync(
        string mountDir,
        SelectionSet set,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}

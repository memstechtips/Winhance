using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// Applies the chosen settings to an image that is already mounted at mountDir.
internal interface IOfflineSettingsApplier
{
    Task<OfflineStepOutcome> ApplyAsync(
        string mountDir,
        SelectionSet set,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}

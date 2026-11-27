using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IAppStagingService
{
    // Puts the installers for the set's external apps and own installers, and the Winhance installer, on the media, so
    // the new PC installs them without a download. Returns one outcome per item it tried, in the order it tried them,
    // so the caller can report the whole run; anything not staged downloads after setup.
    Task<IReadOnlyList<StagedApp>> StageAsync(
        string mediaFolder,
        SelectionSet set,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    void RemoveStaged(string mediaFolder);
}

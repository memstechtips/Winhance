using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

// Installers WIMUtil put on the media: one folder per installer, holding the file and, when winget or the user named
// its silent switches, a winget-style manifest beside it.
public interface IStagedAppInstaller
{
    // The folder staged for this app and this PC's architecture, or null.
    StagedInstaller? Find(ItemDefinition app);

    IReadOnlyList<StagedInstaller> FindAll(string folder);

    // Deletes the folder once the installer succeeds, unless the answer file asked to keep it.
    Task<bool> InstallAsync(StagedInstaller installer, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
}

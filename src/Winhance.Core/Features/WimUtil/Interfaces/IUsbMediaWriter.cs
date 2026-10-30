using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IUsbMediaWriter
{
    // USB hard drives and SSDs (fixed media over USB) are listed only when includeUsbHardDrives is set.
    IReadOnlyList<RemovableDrive> GetCandidateTargets(bool includeUsbHardDrives);

    // Destroys everything on the drive. The caller owns the confirmation.
    void Write(
        RemovableDrive target,
        string workingDirectory,
        BootFileReplacements replacements,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}

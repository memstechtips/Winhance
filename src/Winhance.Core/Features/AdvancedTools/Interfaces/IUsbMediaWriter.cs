using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IUsbMediaWriter
{
    IReadOnlyList<RemovableDrive> GetCandidateTargets();

    // Destroys everything on the drive. The caller owns the confirmation.
    void Write(
        RemovableDrive target,
        string workingDirectory,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}

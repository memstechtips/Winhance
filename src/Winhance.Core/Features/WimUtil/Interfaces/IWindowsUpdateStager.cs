using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IWindowsUpdateStager
{
    // Downloads the update's files from Microsoft, checks size, SHA256 and the Microsoft signature, and puts them
    // in the media's Updates folder. Anything short of Staged leaves no .msu behind.
    Task<UpdateStageResult> StageAsync(
        string workingDirectory,
        WindowsUpdate update,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    bool IsStaged(string workingDirectory);

    void RemoveStaged(string workingDirectory);
}

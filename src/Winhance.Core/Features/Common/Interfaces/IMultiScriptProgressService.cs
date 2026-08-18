using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IMultiScriptProgressService
{
    CancellationTokenSource StartMultiScriptTask(string[] scriptNames);

    // Must be called on the UI thread so Progress<T> captures the SynchronizationContext.
    IProgress<TaskProgressDetail> CreateScriptProgress(int slotIndex);

    void CompleteMultiScriptTask();
}

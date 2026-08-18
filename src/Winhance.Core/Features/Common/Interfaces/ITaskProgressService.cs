using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ITaskProgressService
{
    bool IsTaskRunning { get; }

    string CurrentStatusText { get; }

    bool IsIndeterminate { get; }

    CancellationTokenSource? CurrentTaskCancellationSource { get; }

    CancellationTokenSource StartTask(string taskName, bool isIndeterminate = false);

    void UpdateProgress(int progressPercentage, string? statusText = null);

    void UpdateDetailedProgress(TaskProgressDetail detail);

    void CompleteTask();

    void CancelCurrentTask();

    IProgress<TaskProgressDetail> CreateDetailedProgress();

    IProgress<TaskProgressDetail> CreatePowerShellProgress();

    event EventHandler<TaskProgressDetail>? ProgressUpdated;

    bool ConsumeSkipNextRequest();

    IReadOnlyList<string> GetTerminalOutputLines();

}

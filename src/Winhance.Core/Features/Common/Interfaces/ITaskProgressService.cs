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

    // Ends the task the way CompleteTask does, but logged and shown as a failure.
    void FailTask();

    // Ends the task the way CompleteTask does, but logged as cancelled rather than completed.
    void CancelTask();

    void CancelCurrentTask();

    IProgress<TaskProgressDetail> CreateDetailedProgress();

    event EventHandler<TaskProgressDetail>? ProgressUpdated;

    bool ConsumeSkipNextRequest();

    IReadOnlyList<string> GetTerminalOutputLines();

}

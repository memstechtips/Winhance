using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Provides functionality for tracking task progress.
    /// </summary>
    public interface ITaskProgressService
    {
        /// <summary>
        /// Gets a value indicating whether a task is currently running.
        /// </summary>
        bool IsTaskRunning { get; }

        /// <summary>
        /// Gets the current progress value (0-100).
        /// </summary>
        int CurrentProgress { get; }

        /// <summary>
        /// Gets the current status text.
        /// </summary>
        string CurrentStatusText { get; }

        /// <summary>
        /// Gets a value indicating whether the current task progress is indeterminate.
        /// </summary>
        bool IsIndeterminate { get; }

        /// <summary>
        /// Gets the cancellation token source for the current task.
        /// </summary>
        CancellationTokenSource? CurrentTaskCancellationSource { get; }

        /// <summary>
        /// Starts a new task.
        /// </summary>
        /// <param name="taskName">The name of the task.</param>
        /// <param name="isIndeterminate">Whether the task progress is indeterminate.</param>
        /// <returns>A cancellation token source for the task.</returns>
        CancellationTokenSource StartTask(string taskName, bool isIndeterminate = false);

        /// <summary>
        /// Updates the progress of the current task.
        /// </summary>
        /// <param name="progressPercentage">The progress percentage (0-100).</param>
        /// <param name="statusText">The status text.</param>
        void UpdateProgress(int progressPercentage, string? statusText = null);

        /// <summary>
        /// Updates the progress of the current task with detailed information.
        /// </summary>
        /// <param name="detail">The detailed progress information.</param>
        void UpdateDetailedProgress(TaskProgressDetail detail);

        /// <summary>
        /// Completes the current task.
        /// </summary>
        void CompleteTask();

        /// <summary>
        /// Adds a log message to the task progress.
        /// </summary>
        /// <param name="message">The log message.</param>
        void AddLogMessage(string message);

        /// <summary>
        /// Cancels the current task.
        /// </summary>
        void CancelCurrentTask();

        /// <summary>
        /// Creates a progress reporter for detailed progress.
        /// </summary>
        /// <returns>The progress reporter.</returns>
        IProgress<TaskProgressDetail> CreateDetailedProgress();

        /// <summary>
        /// Creates a progress reporter for PowerShell progress.
        /// </summary>
        /// <returns>The progress reporter.</returns>
        IProgress<TaskProgressDetail> CreatePowerShellProgress();

        /// <summary>
        /// Event raised when progress is updated.
        /// </summary>
        event EventHandler<TaskProgressDetail>? ProgressUpdated;
        
        /// <summary>
        /// Event raised when progress is updated (legacy compatibility).
        /// </summary>
        event EventHandler<TaskProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// Event raised when a log message is added.
        /// </summary>
        event EventHandler<string>? LogMessageAdded;
    }
}
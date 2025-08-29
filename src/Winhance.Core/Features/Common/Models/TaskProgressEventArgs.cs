using System;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Event arguments for task progress events.
    /// </summary>
    public class TaskProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the progress value (0-100), or null if not applicable.
        /// </summary>
        public double Progress { get; }

        /// <summary>
        /// Gets the status text.
        /// </summary>
        public string StatusText { get; }

        /// <summary>
        /// Gets a detailed message about the current operation.
        /// </summary>
        public string DetailedMessage { get; }

        /// <summary>
        /// Gets the log level for the detailed message.
        /// </summary>
        public LogLevel LogLevel { get; }

        /// <summary>
        /// Gets whether the progress is indeterminate.
        /// </summary>
        public bool IsIndeterminate { get; }

        /// <summary>
        /// Gets whether a task is currently running.
        /// </summary>
        public bool IsTaskRunning { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskProgressEventArgs"/> class.
        /// </summary>
        /// <param name="progress">The progress value.</param>
        /// <param name="statusText">The status text.</param>
        /// <param name="detailedMessage">The detailed message.</param>
        /// <param name="logLevel">The log level.</param>
        /// <param name="isIndeterminate">Whether the progress is indeterminate.</param>
        /// <param name="isTaskRunning">Whether a task is running.</param>
        public TaskProgressEventArgs(double progress, string statusText, string detailedMessage = "", LogLevel logLevel = LogLevel.Info, bool isIndeterminate = false, bool isTaskRunning = true)
        {
            Progress = progress;
            StatusText = statusText;
            DetailedMessage = detailedMessage;
            LogLevel = logLevel;
            IsIndeterminate = isIndeterminate;
            IsTaskRunning = isTaskRunning;
        }

        /// <summary>
        /// Creates a TaskProgressEventArgs instance from a TaskProgressDetail.
        /// </summary>
        /// <param name="detail">The progress detail.</param>
        /// <param name="isTaskRunning">Whether a task is running.</param>
        /// <returns>A new TaskProgressEventArgs instance.</returns>
        public static TaskProgressEventArgs FromTaskProgressDetail(TaskProgressDetail detail, bool isTaskRunning = true)
        {
            return new TaskProgressEventArgs(
                detail.Progress ?? 0,
                detail.StatusText ?? string.Empty,
                detail.DetailedMessage ?? string.Empty,
                detail.LogLevel,
                detail.IsIndeterminate,
                isTaskRunning);
        }
    }
}
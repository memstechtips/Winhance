using System;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Models
{
    public class TaskProgressEventArgs : EventArgs
    {
        public double Progress { get; }
        public string StatusText { get; }
        public string DetailedMessage { get; }
        public LogLevel LogLevel { get; }
        public bool IsIndeterminate { get; }
        public bool IsTaskRunning { get; }
        public string TerminalOutput { get; }
        public bool IsActive { get; }

        public TaskProgressEventArgs(double progress, string statusText, string detailedMessage = "", LogLevel logLevel = LogLevel.Info, bool isIndeterminate = false, bool isTaskRunning = true, string terminalOutput = "", bool isActive = false)
        {
            Progress = progress;
            StatusText = statusText;
            DetailedMessage = detailedMessage;
            LogLevel = logLevel;
            IsIndeterminate = isIndeterminate;
            IsTaskRunning = isTaskRunning;
            TerminalOutput = terminalOutput;
            IsActive = isActive;
        }

        public static TaskProgressEventArgs FromTaskProgressDetail(TaskProgressDetail detail, bool isTaskRunning = true)
        {
            return new TaskProgressEventArgs(
                detail.Progress ?? 0,
                detail.StatusText ?? string.Empty,
                detail.DetailedMessage ?? string.Empty,
                detail.LogLevel,
                detail.IsIndeterminate,
                isTaskRunning,
                detail.TerminalOutput ?? string.Empty,
                detail.IsActive);
        }
    }
}
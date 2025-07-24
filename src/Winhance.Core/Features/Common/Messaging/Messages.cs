using System;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Messaging
{
    /// <summary>
    /// Base class for all messages that will be sent through the messenger
    /// </summary>
    public abstract class MessageBase
    {
        public DateTime Timestamp { get; } = DateTime.Now;
    }

    /// <summary>
    /// Message sent when a log entry is created
    /// </summary>
    public class LogMessage : MessageBase
    {
        public string Message { get; set; }
        public LogLevel Level { get; set; }
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// Message sent when progress changes
    /// </summary>
    public class ProgressMessage : MessageBase
    {
        public double Progress { get; set; }
        public string StatusText { get; set; }
        public bool IsIndeterminate { get; set; }
        public bool IsTaskRunning { get; set; }
    }

    /// <summary>
    /// Message sent when detailed task progress changes
    /// </summary>
    public class TaskProgressMessage : MessageBase
    {
        public string TaskName { get; set; }
        public double Progress { get; set; }
        public string StatusText { get; set; }
        public bool IsIndeterminate { get; set; }
        public bool IsTaskRunning { get; set; }
        public bool CanCancel { get; set; }
    }

    /// <summary>
    /// Message sent when window state changes
    /// </summary>
    public class WindowStateMessage : MessageBase
    {
        public enum WindowStateAction
        {
            Minimize,
            Maximize,
            Restore,
            Close
        }

        public WindowStateAction Action { get; set; }
    }

    /// <summary>
    /// Message sent to update the UI theme
    /// </summary>
    public class ThemeChangedMessage : MessageBase
    {
        public bool IsDarkTheme { get; set; }
    }

    /// <summary>
    /// Message sent to show the MoreMenu context menu
    /// </summary>
    public class ShowMoreMenuMessage : MessageBase
    {
        // No additional properties needed - the message itself is the trigger
    }
}

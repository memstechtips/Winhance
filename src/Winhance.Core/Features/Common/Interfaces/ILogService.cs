using System;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Provides logging functionality for the application.
    /// </summary>
    public interface ILogService
    {
        /// <summary>
        /// Starts logging to a file.
        /// </summary>
        void StartLog();

        /// <summary>
        /// Stops logging to a file.
        /// </summary>
        void StopLog();

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogInformation(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception associated with the error, if any.</param>
        void LogError(string message, Exception? exception = null);

        /// <summary>
        /// Logs a success message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogSuccess(string message);

        /// <summary>
        /// Gets the path to the current log file.
        /// </summary>
        /// <returns>The path to the log file.</returns>
        string GetLogPath();

        /// <summary>
        /// Logs a message with the specified level.
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception associated with the message, if any.</param>
        void Log(LogLevel level, string message, Exception? exception = null);

        /// <summary>
        /// Event raised when a log message is generated.
        /// </summary>
        event EventHandler<LogMessageEventArgs>? LogMessageGenerated;
    }
}
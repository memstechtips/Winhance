using System;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Null implementation of ILogService for use during service registration.
    /// </summary>
    public class NullLogService : ILogService
    {
        /// <summary>
        /// Event raised when a log message is generated. Always null for null implementation.
        /// </summary>
        public event EventHandler<LogMessageEventArgs>? LogMessageGenerated;

        /// <summary>
        /// Does nothing - null implementation.
        /// </summary>
        public void StartLog()
        {
            // Do nothing
        }

        /// <summary>
        /// Does nothing - null implementation.
        /// </summary>
        public void StopLog()
        {
            // Do nothing
        }

        /// <summary>
        /// Does nothing - null implementation.
        /// </summary>
        /// <param name="message">The message.</param>
        public void LogInformation(string message)
        {
            // Do nothing
        }

        /// <summary>
        /// Does nothing - null implementation.
        /// </summary>
        /// <param name="message">The message.</param>
        public void LogWarning(string message)
        {
            // Do nothing
        }

        /// <summary>
        /// Does nothing - null implementation.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception.</param>
        public void LogError(string message, Exception? exception = null)
        {
            // Do nothing
        }

        /// <summary>
        /// Does nothing - null implementation.
        /// </summary>
        /// <param name="message">The message.</param>
        public void LogSuccess(string message)
        {
            // Do nothing
        }

        /// <summary>
        /// Returns empty string - null implementation.
        /// </summary>
        /// <returns>Empty string.</returns>
        public string GetLogPath()
        {
            return string.Empty;
        }

        /// <summary>
        /// Does nothing - null implementation.
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="message">The message.</param>
        /// <param name="exception">The exception.</param>
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            // Do nothing
        }
    }
}

using System;
using System.IO;
using System.Threading;

namespace Winhance.WPF.Features.Common.Utilities
{
    /// <summary>
    /// Utility class for direct file logging, used for diagnostic purposes.
    /// </summary>
    public static class FileLogger
    {
        private static readonly object _lockObject = new object();
        private const string LOG_FOLDER = "DiagnosticLogs";
        
        // Logging is disabled as CloseButtonDiagnostics.txt is no longer used
        private static readonly bool _loggingEnabled = false;

        /// <summary>
        /// Logs a message to the diagnostic log file.
        /// </summary>
        /// <param name="source">The source of the log message (e.g., class name)</param>
        /// <param name="message">The message to log</param>
        public static void Log(string source, string message)
        {
            // Early return if logging is disabled
            if (!_loggingEnabled)
                return;
                
            try
            {
                string logPath = GetLogFilePath();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string threadId = Thread.CurrentThread.ManagedThreadId.ToString();
                string logMessage = $"[{timestamp}] [Thread:{threadId}] [{source}] {message}";

                lock (_lockObject)
                {
                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    File.AppendAllText(logPath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // Ignore errors in logging to avoid affecting the application
            }
        }

        /// <summary>
        /// Gets the full path to the log file.
        /// </summary>
        /// <returns>The full path to the log file</returns>
        public static string GetLogFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string winhancePath = Path.Combine(appDataPath, "Winhance");
            string logFolderPath = Path.Combine(winhancePath, LOG_FOLDER);
            // Using a placeholder filename since logging is disabled
            return Path.Combine(logFolderPath, "diagnostics.log");
        }
    }
}
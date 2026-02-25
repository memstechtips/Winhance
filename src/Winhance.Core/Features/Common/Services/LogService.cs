using System;
using System.IO;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Services
{
    public class LogService : ILogService, IDisposable
    {
        private string _logPath;
        private StreamWriter? _logWriter;
        private readonly object _lockObject = new object();
        private IInteractiveUserService? _interactiveUserService;

        public LogService()
        {
            _logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Winhance",
                "Logs",
                $"Winhance_Log_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            );
        }

        public void SetInteractiveUserService(IInteractiveUserService interactiveUserService)
        {
            _interactiveUserService = interactiveUserService;
        }

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            switch (level)
            {
                case LogLevel.Info:
                    LogInformation(message);
                    break;
                case LogLevel.Warning:
                    LogWarning(message);
                    break;
                case LogLevel.Error:
                    LogError(message, exception);
                    break;
                case LogLevel.Success:
                    LogSuccess(message);
                    break;
                case LogLevel.Debug:
                    LogDebug(message);
                    break;
                default:
                    LogInformation(message);
                    break;
            }

        }

        public void StartLog()
        {
            try
            {
                // Ensure directory exists
                var logDirectory = Path.GetDirectoryName(_logPath);
                if (logDirectory != null)
                {
                    Directory.CreateDirectory(logDirectory);
                }
                else
                {
                    throw new InvalidOperationException("Log directory path is null.");
                }

                // Create or overwrite log file
                _logWriter = new StreamWriter(_logPath, false, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                // Write initial log header
                LogInformation($"==== Winhance Log Started ====");
                LogInformation($"Timestamp: {DateTime.Now}");
                if (_interactiveUserService != null && _interactiveUserService.IsOtsElevation)
                {
                    LogInformation($"User: {_interactiveUserService.InteractiveUserName}");
                    LogInformation($"Elevated User: {Environment.UserName}");
                }
                else
                {
                    LogInformation($"User: {Environment.UserName}");
                }
                LogInformation($"Machine: {Environment.MachineName}");

                LogInformation($"OS Version: {Environment.OSVersion}");
                LogInformation("===========================");
            }
            catch (Exception ex)
            {
                // Re-throw so caller can handle/log the error
                throw new InvalidOperationException($"Failed to start log at '{_logPath}': {ex.Message}", ex);
            }
        }

        private void StopLog()
        {
            lock (_lockObject)
            {
                try
                {
                    LogInformation("==== Winhance Log Ended ====");
                    _logWriter?.Close();
                    _logWriter?.Dispose();
                }
                catch (Exception)
                {
                    // Error stopping log
                }
            }
        }

        public void LogInformation(string message)
        {
            WriteLog(message, "INFO");
        }

        public void LogWarning(string message)
        {
            WriteLog(message, "WARNING");
        }

        public void LogError(string message, Exception? exception = null)
        {
            string fullMessage = exception != null
                ? $"{message} - Exception: {exception.Message}\n{exception.StackTrace}"
                : message;
            WriteLog(fullMessage, "ERROR");
        }

        public void LogDebug(string message)
        {
            WriteLog(message, "DEBUG");
        }

        private void LogSuccess(string message)
        {
            WriteLog(message, "SUCCESS");
        }

        public string GetLogPath()
        {
            return _logPath;
        }

        private void WriteLog(string message, string level)
        {
            lock (_lockObject)
            {
                try
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

                    // Write to file if log writer is available
                    _logWriter?.WriteLine(logEntry);

                }
                catch (Exception)
                {
                    // Logging failed
                }
            }
        }

        // Implement IDisposable pattern to ensure logs are stopped
        public void Dispose()
        {
            StopLog();
            GC.SuppressFinalize(this);
        }
    }
}
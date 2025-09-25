using System;
using System.IO;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services
{
    public class LogService : ILogService
    {
        private string _logPath;
        private StreamWriter? _logWriter;
        private readonly object _lockObject = new object();
        private IWindowsVersionService _versionService;

        public event EventHandler<LogMessageEventArgs>? LogMessageGenerated;

        public LogService()
        {
            _logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Winhance",
                "Logs",
                $"Winhance_Log_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            );
        }

        public void Initialize(IWindowsVersionService versionService)
        {
            _versionService = versionService;
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
                default:
                    LogInformation(message);
                    break;
            }

            // Raise event for subscribers
            LogMessageGenerated?.Invoke(this, new LogMessageEventArgs(level, message, exception));
        }

        // This method should be removed, but it might still be used in some places
        // Redirecting to the standard method with correct parameter order
        public void Log(string message, LogLevel level)
        {
            Log(level, message);
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
                LogInformation($"User: {Environment.UserName}");
                LogInformation($"Machine: {Environment.MachineName}");

                if (_versionService != null)
                {
                    LogInformation($"OS Version: {_versionService.GetOsVersionString()}");
                    LogInformation($"OS Build: {_versionService.GetOsBuildString()}");
                }
                else
                {
                    LogInformation($"OS Version: {Environment.OSVersion}");
                }
                LogInformation("===========================");
            }
            catch (Exception ex)
            {
                // Fallback if file write fails
            }
        }

        public void StopLog()
        {
            lock (_lockObject)
            {
                try
                {
                    LogInformation("==== Winhance Log Ended ====");
                    _logWriter?.Close();
                    _logWriter?.Dispose();
                }
                catch (Exception ex)
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

        public void LogSuccess(string message)
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
                catch (Exception ex)
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
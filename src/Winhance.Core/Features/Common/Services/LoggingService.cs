using Microsoft.Extensions.Hosting;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Winhance.Core.Features.Common.Services
{
    public class LoggingService : ILogService, IHostedService, IDisposable
    {
        private readonly ILogService _logService;

        public event EventHandler<LogMessageEventArgs>? LogMessageGenerated;

        public LoggingService(ILogService logService)
        {
            _logService = logService;

            // Subscribe to the inner log service events and forward them
            _logService.LogMessageGenerated += (sender, args) =>
            {
                LogMessageGenerated?.Invoke(this, args);
            };
        }

        public void StartLog()
        {
            _logService.StartLog();
        }

        public void StopLog()
        {
            _logService.StopLog();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logService.StartLog();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logService.StopLog();
            return Task.CompletedTask;
        }

        public void LogInformation(string message)
        {
            _logService.LogInformation(message);
        }

        public void LogWarning(string message)
        {
            _logService.LogWarning(message);
        }

        public void LogError(string message, Exception? exception)
        {
            _logService.LogError(message, exception);
        }

        public void LogSuccess(string message)
        {
            _logService.LogSuccess(message);
        }

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            _logService.Log(level, message, exception);
        }


        public string GetLogPath()
        {
            return _logService.GetLogPath();
        }

        public void Dispose()
        {
            if (_logService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
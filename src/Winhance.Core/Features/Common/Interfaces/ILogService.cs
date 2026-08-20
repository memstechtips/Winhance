using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ILogService
{
    void StartLog();

    void LogInformation(string message);

    void LogWarning(string message);

    void LogError(string message, Exception? exception = null);

    void LogDebug(string message);

    string GetLogPath();

    void Log(LogLevel level, string message, Exception? exception = null);
}

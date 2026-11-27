using System.Runtime.CompilerServices;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Interfaces;

// The compiler fills callerFilePath at every call site and the log line carries that file's type
// name as its source, so a message never names its own class.
public interface ILogService
{
    void StartLog();

    void LogInformation(string message, [CallerFilePath] string callerFilePath = "");

    void LogWarning(string message, [CallerFilePath] string callerFilePath = "");

    void LogError(string message, Exception? exception = null, [CallerFilePath] string callerFilePath = "");

    void LogDebug(string message, [CallerFilePath] string callerFilePath = "");

    string GetLogPath();

    void Log(LogLevel level, string message, Exception? exception = null, [CallerFilePath] string callerFilePath = "");
}

using System.Runtime.CompilerServices;
using Winhance.Core.Features.Common.Helpers;

namespace Winhance.Core.Features.Common.Services;

// Pre-DI startup diagnostics: <data root>\Logs\WinhanceStartupLog.txt, started fresh on the first
// call per run. The last run's log moves to WinhanceStartupLog.previous.txt first, so a relaunch after
// a failed start (or a second launch that hands over to a running copy) keeps the failure. Thread-safe.
public static class StartupLogger
{
    private static readonly string LogPath = Path.Combine(
        AppDataPaths.MachineRoot,
        "Logs",
        "WinhanceStartupLog.txt");

    private static readonly string PreviousLogPath = Path.ChangeExtension(LogPath, ".previous.txt");

    private static readonly object Lock = new object();
    private static bool _firstCall = true;

    public static void Log(string message, [CallerFilePath] string callerFilePath = "")
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{LogService.SourceName(callerFilePath)}] {message}{Environment.NewLine}";
        lock (Lock)
        {
            try
            {
                if (_firstCall)
                {
                    var dir = Path.GetDirectoryName(LogPath);
                    if (dir != null) Directory.CreateDirectory(dir);
                    try { File.Move(LogPath, PreviousLogPath, overwrite: true); }
                    catch (IOException) { } // No log yet, or the old one is held open: start fresh anyway
                    File.WriteAllText(LogPath, line);
                    _firstCall = false;
                }
                else
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch { } // Static pre-DI logger — nowhere to log the failure, and throwing would crash startup
        }
    }
}

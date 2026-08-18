namespace Winhance.Core.Features.Common.Services;

// Pre-DI startup diagnostics: C:\ProgramData\Winhance\Logs\WinhanceStartupLog.txt, overwritten on the first
// call per run. Thread-safe.
public static class StartupLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Winhance",
        "Logs",
        "WinhanceStartupLog.txt");

    private static readonly object Lock = new object();
    private static bool _firstCall = true;

    public static void Log(string message)
    {
        lock (Lock)
        {
            try
            {
                if (_firstCall)
                {
                    var dir = Path.GetDirectoryName(LogPath);
                    if (dir != null) Directory.CreateDirectory(dir);
                    File.WriteAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                    _firstCall = false;
                }
                else
                {
                    File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                }
            }
            catch { /* Static pre-DI logger — nowhere to log the failure, and throwing would crash startup */ }
        }
    }

    public static void Log(string source, string message)
    {
        Log($"[{source}] {message}");
    }
}

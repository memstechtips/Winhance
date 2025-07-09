using System;
using System.IO;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// Simple debug logger that writes directly to desktop
    /// </summary>
    public static class DebugLogger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
            "WinhanceDebug.log"
        );
        
        private static readonly object LockObject = new object();
        
        public static void Log(string message)
        {
            try
            {
                lock (LockObject)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = $"[{timestamp}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogPath, logEntry);
                }
            }
            catch
            {
                // Ignore any logging errors
            }
        }
    }
}
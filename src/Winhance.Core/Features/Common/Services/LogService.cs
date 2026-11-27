using System.Runtime.CompilerServices;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services;

public class LogService : ILogService, IDisposable
{
    // Lines reach the disk in one batch this long after the first of them, so a killed process loses at most this
    // much. Errors are written at once.
    private static readonly TimeSpan FlushDelay = TimeSpan.FromSeconds(1);

    // The header's WMI queries take about a second on a cold start; ending the log waits no longer than this.
    private static readonly TimeSpan HeaderWait = TimeSpan.FromSeconds(2);

    private string _logPath;
    private StreamWriter? _logWriter;
    private readonly object _lockObject = new object();
    private readonly Timer _flushTimer;
    private bool _flushPending;
    private Task _header = Task.CompletedTask;
    // Lines logged while the header is still being collected; they follow it into the file.
    private List<string>? _beforeHeader;
    private ISystemInfoProvider? _systemInfoProvider;

    public LogService() : this(Path.Combine(AppDataPaths.MachineRoot, "Logs"))
    {
    }

    internal LogService(string logDirectory)
    {
        _logPath = Path.Combine(logDirectory, $"Winhance_Log_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        _flushTimer = new Timer(_ => Flush());
    }

    public void SetSystemInfoProvider(ISystemInfoProvider systemInfoProvider)
    {
        _systemInfoProvider = systemInfoProvider;
    }

    public void Log(LogLevel level, string message, Exception? exception = null, [CallerFilePath] string callerFilePath = "")
    {
        switch (level)
        {
            case LogLevel.Warning:
                LogWarning(message, callerFilePath);
                break;
            case LogLevel.Error:
                LogError(message, exception, callerFilePath);
                break;
            case LogLevel.Success:
                WriteLog(message, "SUCCESS", callerFilePath);
                break;
            case LogLevel.Debug:
                LogDebug(message, callerFilePath);
                break;
            default:
                LogInformation(message, callerFilePath);
                break;
        }
    }

    public void StartLog()
    {
        try
        {
            var logDirectory = Path.GetDirectoryName(_logPath);
            if (logDirectory != null)
            {
                Directory.CreateDirectory(logDirectory);
            }
            else
            {
                throw new InvalidOperationException("Log directory path is null.");
            }

            CleanupOldLogs(logDirectory, maxAgeDays: 30, maxFiles: 50);

            lock (_lockObject)
            {
                ReleaseBeforeHeader();
                _logWriter?.Dispose();
                _logWriter = null;
                _logWriter = new StreamWriter(_logPath, false, Encoding.UTF8);
                _beforeHeader = [];
            }

            // The app exits through Application.Exit and Environment.Exit, which never dispose the host, so the
            // unflushed tail is written here.
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            // Collecting the system info held the UI thread for about a second at startup, so it runs beside it and
            // the lines logged meanwhile wait in memory behind it.
            var provider = _systemInfoProvider;
            _header = Task.Run(() => WriteHeader(provider));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start log at '{_logPath}': {ex.Message}", ex);
        }
    }

    private void WriteHeader(ISystemInfoProvider? provider)
    {
        string[] lines;
        try
        {
            lines = provider is null
                ? ["==== Winhance Log Started ====", "System info unavailable (provider not configured)"]
                : HeaderLines(provider.Collect());
        }
        catch (Exception ex)
        {
            lines = ["==== Winhance Log Started ====", $"System info unavailable: {ex.Message}"];
        }

        lock (_lockObject)
        {
            if (_logWriter is null)
                return;
            foreach (var line in lines.Append("====================================="))
                Write(Format(line, "INFO", nameof(LogService)));
            ReleaseBeforeHeader();
        }
    }

    private void ReleaseBeforeHeader()
    {
        if (_beforeHeader is null)
            return;
        var waiting = _beforeHeader;
        _beforeHeader = null;
        foreach (var entry in waiting)
            Write(entry);
    }

    private static string[] HeaderLines(SystemInfo info) =>
    [
        $"==== Winhance {info.AppVersion} Log Started ====",
        $"OS:            {info.OperatingSystem}",
        $"Architecture:  {info.Architecture}",
        $"Device Type:   {info.DeviceType}",
        $"CPU:           {info.Cpu}",
        $"RAM:           {info.Ram}",
        $"GPU:           {info.Gpu}",
        $".NET Runtime:  {info.DotNetRuntime}",
        $"Elevation:     {info.Elevation}",
        $"Firmware:      {info.FirmwareType}",
        $"Secure Boot:   {info.SecureBoot}",
        $"TPM:           {info.Tpm}",
        $"Domain Joined: {info.DomainJoined}",
    ];

    private void OnProcessExit(object? sender, EventArgs e) => StopLog();

    private void StopLog()
    {
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        _header.Wait(HeaderWait);

        lock (_lockObject)
        {
            ReleaseBeforeHeader();
            try
            {
                LogInformation("==== Winhance Log Ended ====");
                _logWriter?.Dispose();
            }
            catch (IOException)
            {
                // Nothing left to write the failure to.
            }
            // Late log calls after Dispose become no-ops instead of ObjectDisposedExceptions.
            _logWriter = null;
        }
    }

    public void LogInformation(string message, [CallerFilePath] string callerFilePath = "")
    {
        WriteLog(message, "INFO", callerFilePath);
    }

    public void LogWarning(string message, [CallerFilePath] string callerFilePath = "")
    {
        WriteLog(message, "WARNING", callerFilePath);
    }

    public void LogError(string message, Exception? exception = null, [CallerFilePath] string callerFilePath = "")
    {
        string fullMessage = exception != null
            ? $"{message} - Exception: {exception.Message}\n{exception.StackTrace}"
            : message;
        WriteLog(fullMessage, "ERROR", callerFilePath, flushNow: true);
    }

    public void LogDebug(string message, [CallerFilePath] string callerFilePath = "")
    {
        WriteLog(message, "DEBUG", callerFilePath);
    }

    // "App.xaml.cs" is the App type, so everything from the first dot goes, not just the extension.
    internal static string SourceName(string callerFilePath)
    {
        var fileName = Path.GetFileName(callerFilePath);
        var dot = fileName.IndexOf('.');
        var name = dot < 0 ? fileName : fileName[..dot];
        return name.Length == 0 ? "Unknown" : name;
    }

    public string GetLogPath()
    {
        return _logPath;
    }

    internal static void CleanupOldLogs(string logDirectory, int maxAgeDays = 30, int maxFiles = 50)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
                return;

            var logFiles = Directory.GetFiles(logDirectory, "Winhance_Log_*.log")
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.CreationTimeUtc)
                .ToList();

            var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);

            for (int i = logFiles.Count - 1; i >= 0; i--)
            {
                if (logFiles[i].CreationTimeUtc < cutoff)
                {
                    try { logFiles[i].Delete(); logFiles.RemoveAt(i); }
                    catch { }
                }
            }

            while (logFiles.Count > maxFiles)
            {
                try { logFiles[0].Delete(); }
                catch { }
                logFiles.RemoveAt(0);
            }
        }
        catch
        {
            // Cleanup is best-effort; don't let it prevent logging from starting
        }
    }

    private void WriteLog(string message, string level, string callerFilePath, bool flushNow = false)
    {
        var entry = Format(message, level, SourceName(callerFilePath));

        // An error must reach the disk at once, so it waits for the header rather than sitting in memory behind it.
        if (flushNow && !_header.IsCompleted)
            _header.Wait(HeaderWait);

        lock (_lockObject)
        {
            if (_logWriter is null)
                return;

            if (_beforeHeader is not null && !flushNow)
            {
                _beforeHeader.Add(entry);
                return;
            }

            ReleaseBeforeHeader();
            Write(entry, flushNow);
        }
    }

    private static string Format(string message, string level, string source) =>
        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{source}] {message}";

    private void Write(string entry, bool flushNow = false)
    {
        try
        {
            _logWriter!.WriteLine(entry);
            if (flushNow)
            {
                _logWriter.Flush();
            }
            else if (!_flushPending)
            {
                _flushPending = true;
                _flushTimer.Change(FlushDelay, Timeout.InfiniteTimeSpan);
            }
        }
        catch (IOException)
        {
            // A logger must not take the app down because the disk write failed.
        }
    }

    private void Flush()
    {
        lock (_lockObject)
        {
            _flushPending = false;
            try
            {
                _logWriter?.Flush();
            }
            catch (IOException)
            {
                // A logger must not take the app down because the disk write failed.
            }
        }
    }

    public void Dispose()
    {
        StopLog();
        _flushTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}

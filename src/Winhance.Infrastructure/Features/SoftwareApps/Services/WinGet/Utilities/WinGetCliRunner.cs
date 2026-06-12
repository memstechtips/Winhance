using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

/// <summary>
/// Resolves the bundled winget.exe path and runs it as a process.
/// </summary>
public static class WinGetCliRunner
{
    private const int DefaultTimeoutMs = 300_000; // 5 minutes — wall-clock cap for short queries

    /// <summary>
    /// Why the winget process stopped, when Winhance (not winget) ended it.
    /// A killed process reports exit code -1 (0xFFFFFFFF), which is meaningless
    /// as a winget exit code — callers use this to tell the user what really happened.
    /// </summary>
    public enum TerminationReason
    {
        None,
        Cancelled,
        IdleTimeout,
        WallClockTimeout,
    }

    public record WinGetCliResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        TerminationReason Termination = TerminationReason.None);

    /// <summary>
    /// Returns a human-readable explanation when Winhance terminated the process,
    /// or null when winget exited on its own. Intended for the terminal output
    /// dialog so users (and support transcripts) don't see a bare -1 (0xFFFFFFFF).
    /// </summary>
    public static string? DescribeTermination(WinGetCliResult result, int timeoutMs, int idleTimeoutMs)
    {
        return result.Termination switch
        {
            TerminationReason.IdleTimeout =>
                $"winget was terminated by Winhance after producing no output for {idleTimeoutMs / 60_000} minutes. " +
                "The package source or the system's app deployment services may be unresponsive on this system.",
            TerminationReason.WallClockTimeout =>
                $"winget was terminated by Winhance after exceeding the {timeoutMs / 60_000} minute time limit.",
            TerminationReason.Cancelled =>
                "winget was terminated because the operation was cancelled.",
            _ => null,
        };
    }

    /// <summary>
    /// Returns the path to winget.exe.
    /// Priority: bundled copy (version-locked, ships with the app) →
    /// system winget (fallback only when bundled is missing).
    ///
    /// Bundled is preferred because system winget can be arbitrarily stale on
    /// machines with Microsoft Store updates blocked — newer flags
    /// (e.g. --disable-interactivity, added in winget 1.4) cause hard exits on
    /// old versions. The bundled copy is a controlled, known-good version.
    /// </summary>
    public static string? GetWinGetExePath(IInteractiveUserService? interactiveUserService = null)
    {
        // 1. Bundled (preferred — registration-free, version-locked).
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "winget-cli", "winget.exe");
        if (File.Exists(bundledPath))
            return bundledPath;

        // 2. System fallback — only reached if the bundled CLI is missing
        //    (corrupted Winhance install, dev build, etc.).
        if (interactiveUserService != null && interactiveUserService.IsOtsElevation)
        {
            // Under OTS, the admin's PATH points at admin's WindowsApps. Resolve
            // from the interactive user's profile instead.
            var interactiveAppData = interactiveUserService.GetInteractiveUserFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var interactiveWinGet = Path.Combine(interactiveAppData, "Microsoft", "WindowsApps", "winget.exe");
            if (File.Exists(interactiveWinGet))
                return interactiveWinGet;

            return null;
        }

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var dir in pathDirs)
        {
            try
            {
                var candidate = Path.Combine(dir, "winget.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (Exception)
            {
                // Skip invalid PATH entries (e.g., malformed paths, access denied)
            }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windowsAppsPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(windowsAppsPath))
            return windowsAppsPath;

        return null;
    }

    /// <summary>
    /// Returns true if winget.exe is found in system PATH or WindowsApps
    /// (indicates DesktopAppInstaller MSIX is registered).
    /// Does NOT check the bundled path.
    /// </summary>
    public static bool IsSystemWinGetAvailable(IInteractiveUserService? interactiveUserService = null)
    {
        // Under OTS, check the interactive user's WindowsApps (not admin's PATH)
        if (interactiveUserService != null && interactiveUserService.IsOtsElevation)
        {
            var interactiveAppData = interactiveUserService.GetInteractiveUserFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var interactiveWinGet = Path.Combine(interactiveAppData, "Microsoft", "WindowsApps", "winget.exe");
            return File.Exists(interactiveWinGet);
        }

        // Non-OTS: standard check
        // 1. System PATH
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var dir in pathDirs)
        {
            try
            {
                var candidate = Path.Combine(dir, "winget.exe");
                if (File.Exists(candidate))
                    return true;
            }
            catch (Exception)
            {
                // Skip invalid PATH entries (e.g., malformed paths, access denied)
            }
        }

        // 2. WindowsApps (standard MSIX install location, may not be on PATH)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windowsAppsPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(windowsAppsPath);
    }

    /// <summary>
    /// Returns the bundled winget.exe path only, or null if it doesn't exist.
    /// </summary>
    public static string? GetBundledWinGetExePath()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "winget-cli", "winget.exe");
        return File.Exists(bundledPath) ? bundledPath : null;
    }

    /// <summary>
    /// Returns a short log tag identifying which winget binary <paramref name="exePath"/> is —
    /// "bundled-winget" for the copy that ships with the app, "system-winget" for anything else.
    /// Used in log line prefixes so support transcripts make it obvious which CLI ran.
    /// </summary>
    public static string GetLogTag(string? exePath)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "winget-cli", "winget.exe");
        return string.Equals(exePath, bundled, StringComparison.OrdinalIgnoreCase)
            ? "bundled-winget"
            : "system-winget";
    }

    /// <summary>
    /// Runs winget.exe with the given arguments, streaming stdout/stderr.
    /// </summary>
    /// <param name="timeoutMs">
    /// Wall-clock kill timeout. Pass 0 (or Timeout.Infinite = -1) to disable — useful when
    /// the caller relies on <paramref name="idleTimeoutMs"/> instead. Default 5 minutes.
    /// </param>
    /// <param name="idleTimeoutMs">
    /// Optional idle-output kill timeout. When &gt; 0, the process is killed if no output
    /// (stdout, stderr, or progress line) arrives for this many milliseconds. The timer
    /// resets on every line, so legitimately slow-but-progressing installs (large CDN
    /// downloads, slow disks) keep renewing their deadline indefinitely. 0 = disabled.
    /// </param>
    /// <param name="exePathOverride">
    /// If provided, uses this path instead of auto-resolving via <see cref="GetWinGetExePath"/>.
    /// Useful for forcing the bundled copy (e.g. when installing AppInstaller itself).
    /// </param>
    /// <param name="interactiveUserService">
    /// If provided and OTS elevation is active, runs winget as the interactive user
    /// so packages install to the correct user's scope.
    /// </param>
    public static async Task<WinGetCliResult> RunAsync(
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default,
        int timeoutMs = DefaultTimeoutMs,
        string? exePathOverride = null,
        IInteractiveUserService? interactiveUserService = null,
        Action<string>? onProgressLine = null,
        int idleTimeoutMs = 0)
    {
        var exePath = exePathOverride ?? GetWinGetExePath(interactiveUserService)
            ?? throw new FileNotFoundException("winget.exe not found. Bundled CLI may be missing.");

        // OTS: run winget as the interactive user so packages install to their scope.
        // Idle-timeout is not plumbed through the OTS helper; callers that need it
        // pass a generous wall-clock timeoutMs as the fallback.
        if (interactiveUserService != null
            && interactiveUserService.IsOtsElevation
            && interactiveUserService.HasInteractiveUserToken)
        {
            var result = await interactiveUserService.RunProcessAsInteractiveUserAsync(
                exePath, arguments, onOutputLine, onErrorLine, cancellationToken, timeoutMs, onProgressLine).ConfigureAwait(false);
            return new WinGetCliResult(result.ExitCode, result.StandardOutput, result.StandardError);
        }

        CancellationTokenSource? wallClockCts = null;
        CancellationTokenSource? idleCts = null;
        CancellationTokenSource? linkedCts = null;
        try
        {
            var tokens = new List<CancellationToken> { cancellationToken };
            if (timeoutMs > 0)
            {
                wallClockCts = new CancellationTokenSource(timeoutMs);
                tokens.Add(wallClockCts.Token);
            }
            if (idleTimeoutMs > 0)
            {
                idleCts = new CancellationTokenSource(idleTimeoutMs);
                tokens.Add(idleCts.Token);
            }
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(tokens.ToArray());

            // Classifies why the process ended. Only a kill via Process.Kill reports
            // exit code -1; any other code means winget exited on its own, even if a
            // timer happened to fire in the same instant.
            TerminationReason GetTerminationReason(int exitCode)
            {
                if (exitCode != -1)
                    return TerminationReason.None;
                if (cancellationToken.IsCancellationRequested)
                    return TerminationReason.Cancelled;
                if (idleCts?.IsCancellationRequested == true)
                    return TerminationReason.IdleTimeout;
                if (wallClockCts?.IsCancellationRequested == true)
                    return TerminationReason.WallClockTimeout;
                return TerminationReason.None;
            }

            // Wrap callbacks so any output line resets the idle deadline. Wall-clock CTS
            // is NOT reset — it stays an absolute upper bound. When idleCts is null the
            // wrappers are unnecessary, but keeping them uniform avoids branchy plumbing.
            var capturedIdleCts = idleCts;
            var capturedIdleMs = idleTimeoutMs;
            Action<string>? wrapOutput = (onOutputLine == null && capturedIdleCts == null) ? null : line =>
            {
                ResetIdle(capturedIdleCts, capturedIdleMs);
                onOutputLine?.Invoke(line);
            };
            Action<string>? wrapError = (onErrorLine == null && capturedIdleCts == null) ? null : line =>
            {
                ResetIdle(capturedIdleCts, capturedIdleMs);
                onErrorLine?.Invoke(line);
            };
            Action<string>? wrapProgress = (onProgressLine == null && capturedIdleCts == null) ? null : line =>
            {
                ResetIdle(capturedIdleCts, capturedIdleMs);
                onProgressLine?.Invoke(line);
            };

            // When real-time progress is requested, use ConPTY so that winget sees
            // isatty(stdout)==true and outputs progress bars with std::flush.
            // Without ConPTY, winget detects a pipe and suppresses progress output.
            if (onProgressLine != null)
            {
                try
                {
                    using var conPty = new ConPtyProcess();
                    var ptyResult = await conPty.RunAsync(
                        exePath, arguments,
                        wrapOutput, wrapError, wrapProgress,
                        linkedCts.Token).ConfigureAwait(false);
                    return ptyResult with { Termination = GetTerminationReason(ptyResult.ExitCode) };
                }
                catch (Exception ex) when (
                    ex is InvalidOperationException or
                    EntryPointNotFoundException or
                    DllNotFoundException)
                {
                    // ConPTY unavailable (old Windows build or API failure)
                    // — fall through silently to pipe mode
                }
            }

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            process.Start();

            // Kill process tree on cancellation
            using var registration = linkedCts.Token.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { /* Best-effort process kill — process may have already exited */ }
            });

            // Read stdout char-by-char to detect \r (progress) vs \n (permanent) immediately.
            // ReadLineAsync peeks ahead after \r which blocks until the next char arrives,
            // preventing real-time progress bar updates.
            var readStdout = Task.Run(async () =>
            {
                await ReadStdoutCharByCharAsync(
                    process.StandardOutput, stdoutBuilder, wrapOutput, wrapProgress).ConfigureAwait(false);
            }, CancellationToken.None);

            var readStderr = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    stderrBuilder.AppendLine(line);
                    wrapError?.Invoke(line);
                }
            }, CancellationToken.None);

            await Task.WhenAll(readStdout, readStderr).ConfigureAwait(false);
            // Both streams hitting EOF means the process has exited (the kill
            // registration guarantees that on cancellation). Wait with no token:
            // passing the linked token here would throw on a timeout kill instead
            // of returning the -1 exit code with its termination reason.
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

            return new WinGetCliResult(
                process.ExitCode,
                stdoutBuilder.ToString(),
                stderrBuilder.ToString(),
                GetTerminationReason(process.ExitCode));
        }
        finally
        {
            linkedCts?.Dispose();
            wallClockCts?.Dispose();
            idleCts?.Dispose();
        }
    }

    private static void ResetIdle(CancellationTokenSource? cts, int idleTimeoutMs)
    {
        if (cts == null) return;
        try { cts.CancelAfter(idleTimeoutMs); }
        catch (ObjectDisposedException) { /* race with cleanup; idle no longer relevant */ }
    }

    /// <summary>
    /// Reads stdout char-by-char, classifying lines by their terminator:
    /// \r → progress (transient, emitted immediately via onProgressLine)
    /// \n → permanent (emitted via onOutputLine)
    /// \r\n → permanent (emitted via onOutputLine; \r fires onProgressLine first)
    /// </summary>
    internal static async Task ReadStdoutCharByCharAsync(
        StreamReader reader,
        StringBuilder outputBuilder,
        Action<string>? onOutputLine,
        Action<string>? onProgressLine)
    {
        var currentLine = new StringBuilder();
        var buffer = new char[1];
        string? lastStringBeforeLF = null;

        while (await reader.ReadBlockAsync(buffer, 0, 1).ConfigureAwait(false) > 0)
        {
            char c = buffer[0];

            if (c == '\n')
            {
                if (currentLine.Length == 0)
                {
                    if (lastStringBeforeLF is not null)
                    {
                        // \r\n sequence: already emitted as progress on \r,
                        // now re-emit as permanent line
                        onOutputLine?.Invoke(lastStringBeforeLF);
                        lastStringBeforeLF = null;
                    }
                    continue;
                }
                string line = currentLine.ToString();
                outputBuilder.AppendLine(line);
                onOutputLine?.Invoke(line);
                currentLine.Clear();
                lastStringBeforeLF = null;
            }
            else if (c == '\r')
            {
                if (currentLine.Length == 0) continue;
                string line = currentLine.ToString();
                lastStringBeforeLF = line;
                outputBuilder.AppendLine(line);
                onProgressLine?.Invoke(line);
                currentLine.Clear();
            }
            else
            {
                currentLine.Append(c);
            }
        }

        // Flush remaining content at EOF
        if (currentLine.Length > 0)
        {
            string line = currentLine.ToString();
            outputBuilder.AppendLine(line);
            onOutputLine?.Invoke(line);
        }
    }
}

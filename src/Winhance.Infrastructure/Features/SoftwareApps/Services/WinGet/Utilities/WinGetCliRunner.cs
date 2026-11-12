using System.Diagnostics;
using System.Text;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

internal static class WinGetCliRunner
{
    private const int DefaultTimeoutMs = 300_000; // 5 minutes — wall-clock cap for short queries

    // A user-started install, update or removal has no time limit; the user has the cancel button. When winget goes
    // this long without a line of output, the caller is told so the task dialog can say it is still waiting.
    public const int StallNoticeMs = 180_000;

    // A killed process reports exit code -1 (0xFFFFFFFF), meaningless as a winget code - callers use this to tell
    // the user what really happened.
    public enum TerminationReason
    {
        None,
        Cancelled,
        WallClockTimeout,
    }

    public record WinGetCliResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        TerminationReason Termination = TerminationReason.None);

    // For the terminal output dialog, so users and support transcripts don't see a bare -1 (0xFFFFFFFF).
    public static string? DescribeTermination(WinGetCliResult result, ILocalizationService? localization)
    {
        return result.Termination == TerminationReason.Cancelled
            ? localization.GetStringOrDefault(
                "WinGet_TerminatedCancelled",
                "winget was terminated because the operation was cancelled.")
            : null;
    }

    // Bundled copy first (version-locked); system winget only when the bundled one is missing. System winget can be
    // arbitrarily stale on machines with Store updates blocked, and newer flags (--disable-interactivity, winget
    // 1.4) hard-exit on old versions.
    public static string? GetWinGetExePath(IInteractiveUserService? interactiveUserService = null)
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "winget-cli", "winget.exe");
        if (File.Exists(bundledPath))
            return bundledPath;

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
            var candidate = Path.Combine(dir, "winget.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windowsAppsPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(windowsAppsPath))
            return windowsAppsPath;

        return null;
    }

    // Does NOT check the bundled path.
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

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir, "winget.exe");
            if (File.Exists(candidate))
                return true;
        }

        // WindowsApps (standard MSIX install location, may not be on PATH)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windowsAppsPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(windowsAppsPath);
    }

    public static string? GetBundledWinGetExePath()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "winget-cli", "winget.exe");
        return File.Exists(bundledPath) ? bundledPath : null;
    }

    // Used in log line prefixes so support transcripts make it obvious which CLI ran.

    // timeoutMs: wall-clock kill for short queries; 0 or Timeout.Infinite disables it, which user-started installs
    // use. onStall fires once after stallAfterMs with no stdout/stderr/progress, and again after any later quiet
    // spell; it never ends the run. exePathOverride forces a binary (e.g. the bundled copy when installing
    // AppInstaller itself). interactiveUserService + OTS runs winget as the interactive user so packages land in the
    // right scope. asShellUser runs it unelevated as the signed-in user instead, whether or not OTS is in play.
    // onProgressLine gets the transient \r fragments; only \r\n lines reach onOutputLine. Cancellation kills the
    // process tree.
    public static async Task<WinGetCliResult> RunAsync(
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default,
        int timeoutMs = DefaultTimeoutMs,
        string? exePathOverride = null,
        IInteractiveUserService? interactiveUserService = null,
        Action<string>? onProgressLine = null,
        Action? onStall = null,
        int stallAfterMs = StallNoticeMs,
        bool asShellUser = false)
    {
        var exePath = exePathOverride ?? GetWinGetExePath(interactiveUserService)
            ?? throw new FileNotFoundException("winget.exe not found. Bundled CLI may be missing.");

        CancellationTokenSource? wallClockCts = null;
        Timer? stallTimer = null;
        CancellationTokenSource? linkedCts = null;
        try
        {
            var tokens = new List<CancellationToken> { cancellationToken };
            if (timeoutMs > 0)
            {
                wallClockCts = new CancellationTokenSource(timeoutMs);
                tokens.Add(wallClockCts.Token);
            }
            if (onStall != null)
                stallTimer = new Timer(_ => onStall(), null, stallAfterMs, Timeout.Infinite);
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
                if (wallClockCts?.IsCancellationRequested == true)
                    return TerminationReason.WallClockTimeout;
                return TerminationReason.None;
            }

            // Output and error lines re-arm the stall notice. Progress lines re-arm it only when they changed:
            // winget prints a spinner frame every ~5 s whether or not it is working, so counting those would
            // hide a real wedge.
            var capturedStallTimer = stallTimer;
            Action<string>? wrapOutput = (onOutputLine == null && capturedStallTimer == null) ? null : line =>
            {
                RearmStall(capturedStallTimer, stallAfterMs);
                onOutputLine?.Invoke(line);
            };
            Action<string>? wrapError = (onErrorLine == null && capturedStallTimer == null) ? null : line =>
            {
                RearmStall(capturedStallTimer, stallAfterMs);
                onErrorLine?.Invoke(line);
            };
            var lastProgressKey = "";
            Action<string>? wrapProgress = (onProgressLine == null && capturedStallTimer == null) ? null : line =>
            {
                var key = ProgressKey(line);
                if (key != lastProgressKey)
                {
                    lastProgressKey = key;
                    RearmStall(capturedStallTimer, stallAfterMs);
                }
                onProgressLine?.Invoke(line);
            };

            if (asShellUser && interactiveUserService != null)
            {
                var shellResult = await interactiveUserService.RunProcessAsShellUserAsync(
                    exePath, arguments, wrapOutput, wrapError, linkedCts.Token, timeoutMs: 0, onProgressLine: wrapProgress).ConfigureAwait(false);
                if (shellResult != null)
                {
                    return new WinGetCliResult(shellResult.ExitCode, shellResult.StandardOutput, shellResult.StandardError,
                        GetTerminationReason(shellResult.ExitCode));
                }
            }

            // OTS: run winget as the interactive user so packages install to their scope. The helper's own
            // wall-clock stays off (0): linkedCts already carries wall-clock and caller cancellation.
            if (interactiveUserService != null
                && interactiveUserService.IsOtsElevation
                && interactiveUserService.HasInteractiveUserToken)
            {
                var otsResult = await interactiveUserService.RunProcessAsInteractiveUserAsync(
                    exePath, arguments, wrapOutput, wrapError, linkedCts.Token, timeoutMs: 0, onProgressLine: wrapProgress).ConfigureAwait(false);
                return new WinGetCliResult(otsResult.ExitCode, otsResult.StandardOutput, otsResult.StandardError,
                    GetTerminationReason(otsResult.ExitCode));
            }

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
                        wrapOutput, wrapProgress,
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

            using var registration = linkedCts.Token.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { }
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
            stallTimer?.Dispose();
        }
    }

    // The parsed percent when there is one, else the text without spinner glyphs: empty for a bare spinner frame.
    internal static string ProgressKey(string line)
    {
        var percent = WinGetProgressParser.ParseLine(line)?.Percent;
        if (percent.HasValue)
            return percent.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        return string.Concat(line.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '\\' or '|' or '/')));
    }

    private static void RearmStall(Timer? timer, int stallAfterMs)
    {
        if (timer == null) return;
        try { timer.Change(stallAfterMs, Timeout.Infinite); }
        catch (ObjectDisposedException) { } // race with cleanup; the notice no longer matters
    }

    // Classifies lines by terminator: \r -> progress (transient, onProgressLine); \n -> permanent (onOutputLine);
    // \r\n -> permanent, with \r firing onProgressLine first.
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

        if (currentLine.Length > 0)
        {
            string line = currentLine.ToString();
            outputBuilder.AppendLine(line);
            onOutputLine?.Invoke(line);
        }
    }
}

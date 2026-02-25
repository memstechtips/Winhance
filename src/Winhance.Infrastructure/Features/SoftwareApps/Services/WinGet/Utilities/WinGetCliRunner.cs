using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities
{
    /// <summary>
    /// Resolves the bundled winget.exe path and runs it as a process.
    /// </summary>
    public static class WinGetCliRunner
    {
        private const int DefaultTimeoutMs = 300_000; // 5 minutes

        public record WinGetCliResult(int ExitCode, string StandardOutput, string StandardError);

        /// <summary>
        /// Returns the path to winget.exe.
        /// Priority: system-installed winget (stays current via Store updates) →
        /// bundled copy (fallback for fresh installs / missing DesktopAppInstaller).
        /// </summary>
        public static string? GetWinGetExePath(IInteractiveUserService? interactiveUserService = null)
        {
            // Under OTS elevation, the system PATH contains the admin user's WindowsApps.
            // We must skip PATH and resolve from the interactive (logged-in) user's paths,
            // falling back to the bundled copy.
            if (interactiveUserService != null && interactiveUserService.IsOtsElevation)
            {
                // 1. Interactive user's WindowsApps (DesktopAppInstaller registered for them)
                var interactiveAppData = interactiveUserService.GetInteractiveUserFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                var interactiveWinGet = Path.Combine(interactiveAppData, "Microsoft", "WindowsApps", "winget.exe");
                if (File.Exists(interactiveWinGet))
                    return interactiveWinGet;

                // 2. Bundled copy (fallback)
                var bundled = Path.Combine(AppContext.BaseDirectory, "winget-cli", "winget.exe");
                if (File.Exists(bundled))
                    return bundled;

                return null;
            }

            // Non-OTS: standard resolution order
            // 1. System PATH (preferred — kept up-to-date via Microsoft Store)
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

            // 2. WindowsApps (standard MSIX install location, may not be on PATH)
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var windowsAppsPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
            if (File.Exists(windowsAppsPath))
                return windowsAppsPath;

            // 3. Bundled copy (fallback — ships with Winhance for fresh installs
            //    where DesktopAppInstaller isn't registered yet)
            var bundledPath = Path.Combine(AppContext.BaseDirectory, "winget-cli", "winget.exe");
            if (File.Exists(bundledPath))
                return bundledPath;

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
        /// Runs winget.exe with the given arguments, streaming stdout/stderr.
        /// </summary>
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
            Action<string>? onProgressLine = null)
        {
            var exePath = exePathOverride ?? GetWinGetExePath(interactiveUserService)
                ?? throw new FileNotFoundException("winget.exe not found. Bundled CLI may be missing.");

            // OTS: run winget as the interactive user so packages install to their scope
            if (interactiveUserService != null
                && interactiveUserService.IsOtsElevation
                && interactiveUserService.HasInteractiveUserToken)
            {
                var result = await interactiveUserService.RunProcessAsInteractiveUserAsync(
                    exePath, arguments, onOutputLine, onErrorLine, cancellationToken, timeoutMs, onProgressLine).ConfigureAwait(false);
                return new WinGetCliResult(result.ExitCode, result.StandardOutput, result.StandardError);
            }

            // When real-time progress is requested, use ConPTY so that winget sees
            // isatty(stdout)==true and outputs progress bars with std::flush.
            // Without ConPTY, winget detects a pipe and suppresses progress output.
            if (onProgressLine != null)
            {
                try
                {
                    using var conPty = new ConPtyProcess();
                    return await conPty.RunAsync(
                        exePath, arguments,
                        onOutputLine, onErrorLine, onProgressLine,
                        cancellationToken, timeoutMs).ConfigureAwait(false);
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

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

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
                    process.StandardOutput, stdoutBuilder, onOutputLine, onProgressLine).ConfigureAwait(false);
            }, CancellationToken.None);

            var readStderr = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    stderrBuilder.AppendLine(line);
                    onErrorLine?.Invoke(line);
                }
            }, CancellationToken.None);

            await Task.WhenAll(readStdout, readStderr).ConfigureAwait(false);
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);

            return new WinGetCliResult(
                process.ExitCode,
                stdoutBuilder.ToString(),
                stderrBuilder.ToString());
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
}

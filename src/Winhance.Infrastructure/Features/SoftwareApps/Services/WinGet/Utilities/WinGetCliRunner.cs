using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        public static string? GetWinGetExePath()
        {
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
                catch
                {
                    // Skip invalid PATH entries
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
        public static bool IsSystemWinGetAvailable()
        {
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
                catch
                {
                    // Skip invalid PATH entries
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
        public static async Task<WinGetCliResult> RunAsync(
            string arguments,
            Action<string>? onOutputLine = null,
            Action<string>? onErrorLine = null,
            CancellationToken cancellationToken = default,
            int timeoutMs = DefaultTimeoutMs,
            string? exePathOverride = null)
        {
            var exePath = exePathOverride ?? GetWinGetExePath()
                ?? throw new FileNotFoundException("winget.exe not found. Bundled CLI may be missing.");

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
                try { process.Kill(entireProcessTree: true); } catch { }
            });

            // Read stdout and stderr in parallel tasks to avoid deadlocks
            var readStdout = Task.Run(async () =>
            {
                while (await process.StandardOutput.ReadLineAsync() is { } line)
                {
                    stdoutBuilder.AppendLine(line);
                    onOutputLine?.Invoke(line);
                }
            }, CancellationToken.None);

            var readStderr = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync() is { } line)
                {
                    stderrBuilder.AppendLine(line);
                    onErrorLine?.Invoke(line);
                }
            }, CancellationToken.None);

            await Task.WhenAll(readStdout, readStderr);
            await process.WaitForExitAsync(linkedCts.Token);

            return new WinGetCliResult(
                process.ExitCode,
                stdoutBuilder.ToString(),
                stderrBuilder.ToString());
        }
    }
}

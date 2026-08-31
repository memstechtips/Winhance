using System.Diagnostics;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class ProcessExecutor : IProcessExecutor
{
    public async Task<ProcessExecutionResult> ExecuteAsync(
        string fileName,
        string arguments,
        CancellationToken ct = default)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        process.Start();

        // Read stdout and stderr concurrently to avoid deadlocks
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return new ProcessExecutionResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdoutTask.Result,
            StandardError = stderrTask.Result
        };
    }

    public async Task<ProcessExecutionResult> ExecuteWithStreamingAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken ct = default)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        process.Start();

        using var registration = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        });

        var readOutput = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                stdout.AppendLine(line);
                onOutputLine?.Invoke(line);
            }
        }, CancellationToken.None);

        var readError = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                stderr.AppendLine(line);
                onErrorLine?.Invoke(line);
            }
        }, CancellationToken.None);

        await Task.WhenAll(readOutput, readError).ConfigureAwait(false);
        // Both streams at EOF means the process is gone (the kill registration guarantees that on
        // cancellation). Waiting with the token would throw and lose the exit code and the output
        // collected so far; callers observe their own token to classify a kill as cancellation.
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        return new ProcessExecutionResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString()
        };
    }

    public async Task<int?> ShellExecuteAsync(
        string fileName,
        string? arguments = null,
        bool waitForExit = false,
        CancellationToken ct = default)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true
        });

        if (process == null)
            return null;

        if (waitForExit)
        {
            try
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
                return process.ExitCode;
            }
            finally
            {
                process.Dispose();
            }
        }

        // Fire-and-forget: dispose immediately to release the native handle.
        // The child process continues running independently.
        process.Dispose();
        return 0;
    }
}

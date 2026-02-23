using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Abstraction for launching external processes.
/// Enables testing of services that shell out to CLI tools (powercfg, dism, reg, msiexec, etc.).
/// </summary>
public interface IProcessExecutor
{
    /// <summary>
    /// Executes a process with redirected stdout/stderr and captures all output.
    /// Uses CreateNoWindow=true, UseShellExecute=false, UTF-8 encoding.
    /// </summary>
    Task<ProcessExecutionResult> ExecuteAsync(
        string fileName,
        string arguments,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a process and streams stdout/stderr line-by-line via callbacks.
    /// Used for long-running tools (DISM, Chocolatey) that report progress line-by-line.
    /// Cancellation kills the process.
    /// </summary>
    Task<ProcessExecutionResult> ExecuteWithStreamingAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken ct = default);

    /// <summary>
    /// Launches a process using shell execution (UseShellExecute=true).
    /// Used for opening URLs, Explorer windows, interactive installers, and regedit.
    /// </summary>
    /// <param name="waitForExit">If true, waits for the process to exit and returns exit code.</param>
    /// <returns>The exit code if waitForExit is true; 0 if launched successfully without waiting; null if the process failed to start.</returns>
    Task<int?> ShellExecuteAsync(
        string fileName,
        string? arguments = null,
        bool waitForExit = false,
        CancellationToken ct = default);
}

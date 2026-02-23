namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Result of a process execution with captured stdout and stderr.
/// </summary>
public record ProcessExecutionResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool Succeeded => ExitCode == 0;
}

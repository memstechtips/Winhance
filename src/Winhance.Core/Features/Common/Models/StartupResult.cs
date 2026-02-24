namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Communicates the result of the startup orchestration sequence back to MainWindow.
/// </summary>
public record StartupResult
{
    /// <summary>
    /// The backup result from phase 3 (system restore point), if that phase ran.
    /// </summary>
    public BackupResult? BackupResult { get; init; }
}

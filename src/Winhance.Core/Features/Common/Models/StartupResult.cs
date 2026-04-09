namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Communicates the result of the startup orchestration sequence back to MainWindow.
/// </summary>
public record StartupResult
{
    /// <summary>
    /// True if this is the first launch (config backup was just created).
    /// Used to trigger the restore point offer dialog.
    /// </summary>
    public bool IsFirstLaunch { get; init; }
}

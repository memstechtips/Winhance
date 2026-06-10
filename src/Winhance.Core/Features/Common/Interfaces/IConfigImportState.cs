namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Tracks whether a config import operation is currently active.
/// Services check this to defer expensive side effects (process restarts, explorer kills)
/// until the import completes.
/// </summary>
public interface IConfigImportState
{
    bool IsActive { get; set; }

    /// <summary>
    /// Human-readable name of the config being imported (file name or built-in config name),
    /// set by ConfigLoadService at load time and consumed for the change-history batch header.
    /// </summary>
    string? SourceName { get; set; }
}

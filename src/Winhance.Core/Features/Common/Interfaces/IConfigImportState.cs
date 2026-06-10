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

    /// <summary>
    /// True while an active config import's Power section carries individual power-setting
    /// items. Signals the power-plan special handler to skip its recommended-settings
    /// re-apply — the import's own values are the source of truth.
    /// </summary>
    bool ImportSuppliesPowerValues { get; set; }
}

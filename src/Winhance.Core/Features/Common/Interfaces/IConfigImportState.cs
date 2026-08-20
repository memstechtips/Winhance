namespace Winhance.Core.Features.Common.Interfaces;

// Services check this to defer expensive side effects (process restarts, Explorer kills) until the import completes.
public interface IConfigImportState
{
    bool IsActive { get; set; }

    string? SourceName { get; set; }

    // Tells the power-plan special handler to skip its recommended-settings re-apply: the import's own values are the source of truth.
    bool ImportSuppliesPowerValues { get; set; }
}

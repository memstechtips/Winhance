namespace Winhance.Core.Features.Common.Interfaces;

// Services check this to defer expensive side effects (process restarts, Explorer kills) until the import completes.
public interface IConfigImportState
{
    bool IsActive { get; set; }

    string? SourceName { get; set; }

    // The user's own file, when that is what is being imported; null for a preset or a backup.
    string? SourcePath { get; set; }

    // Tells the power-plan special handler to skip its recommended-settings re-apply: the import's own values are the source of truth.
    bool ImportSuppliesPowerValues { get; set; }

    // Sections apply in parallel, so this is called from several threads at once.
    void ReportNotApplied(string message);

    IReadOnlyList<string> TakeNotApplied();
}

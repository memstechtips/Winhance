namespace Winhance.Core.Features.Common.Interfaces;

// Services check this to defer expensive side effects (process restarts, Explorer kills) until the import completes.
public interface IConfigImportState
{
    // True only on the flow that called Begin and the work it starts, so a setting the user changes
    // meanwhile, or a second import, is not treated as part of this one.
    bool IsActive { get; }

    IDisposable Begin();

    // Any import on any flow, for UI that reads from outside the import (the Restart Explorer button).
    bool IsAnyImportRunning { get; }

    // Raised when IsAnyImportRunning flips, on the thread that began or ended the import.
    event EventHandler? AnyImportRunningChanged;

    string? SourceName { get; set; }

    // Tells the power-plan special handler to skip its recommended-settings re-apply: the import's own values are the source of truth.
    bool ImportSuppliesPowerValues { get; set; }

    // Sections apply in parallel, so this is called from several threads at once.
    void ReportNotApplied(string message);

    IReadOnlyList<string> TakeNotApplied();
}

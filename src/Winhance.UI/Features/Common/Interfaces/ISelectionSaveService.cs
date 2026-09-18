using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.UI.Features.Common.Interfaces;

public sealed record SelectionSaveOptions
{
    // Set by a caller that already owns a destination (WIMUtil's extracted-ISO working directory): no picker opens.
    public string? FixedPath { get; init; }

    // Null from the bar's Save, where the WIMUtil session decides; Generate names it so the album and the
    // driver step land on the media being built.
    public string? MediaFolder { get; init; }

    // The startup backup runs with nobody watching, so it must not stop on a question.
    public bool ConfirmEmptyAppSelection { get; init; } = true;

    // WIMUtil reports through its own inline status line, so it takes the confirmations but not the success dialog.
    public bool ReportSuccessInDialog { get; init; } = true;
}

// The written path comes back for callers that keep working with the file; it is null when nothing was saved.
// A failed write propagates instead: each entry point reports it on its own surface - a dialog, an inline status
// line, or the log alone for the startup backup.
public interface ISelectionSaveService
{
    Task<string?> SaveAsync(BuilderTarget target, SelectionSet selections, SelectionSaveOptions? options = null);
}

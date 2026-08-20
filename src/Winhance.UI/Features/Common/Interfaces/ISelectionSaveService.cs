using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.UI.Features.Common.Interfaces;

public sealed record SelectionSaveOptions
{
    // Set by a caller that already owns a destination (WIMUtil's extracted-ISO working directory): no picker opens.
    public string? FixedPath { get; init; }

    // The startup backup runs with nobody watching, so it must not stop on a question.
    public bool ConfirmEmptyAppSelection { get; init; } = true;

    // WIMUtil reports through its own inline status line, so it takes the confirmations but not the success dialog.
    public bool ReportSuccessInDialog { get; init; } = true;

    // Only the Advanced Tools card can navigate to WIMUtil from where it stands.
    public bool OfferWimUtil { get; init; }
}

public sealed record SaveOutcome(string? Path, bool WimUtilRequested)
{
    public bool Saved => Path is not null;
}

// A failed write propagates: each entry point reports it on its own surface - a dialog, an inline status line,
// or the log alone for the startup backup.
public interface ISelectionSaveService
{
    Task<SaveOutcome> SaveAsync(BuilderTarget target, SelectionSet selections, SelectionSaveOptions? options = null);
}

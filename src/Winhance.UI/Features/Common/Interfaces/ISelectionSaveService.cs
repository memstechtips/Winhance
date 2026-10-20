using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.UI.Features.Common.Interfaces;

public sealed record SelectionSaveOptions
{
    // Set by a caller that already owns a destination (WIMUtil's extracted-ISO working directory): no picker opens.
    public string? FixedPath { get; init; }

    // Null from the bar's Save, where the WIMUtil session decides; Generate names it so the album, the driver
    // step and the installers land on the media being built.
    public string? MediaFolder { get; init; }

    // WIMUtil gathers the whole run into one summary of its own, so it takes neither the success dialog nor the
    // one naming the apps that stayed off the media. The startup backup runs with nobody watching.
    public bool ReportSuccessInDialog { get; init; } = true;
}

// The written path comes back for callers that keep working with the file; it is null when nothing was saved.
// A failed write propagates instead: each entry point reports it on its own surface - a dialog, an inline status
// line, or the log alone for the startup backup. Apps carries one entry per installer the save tried to put on
// media, and is empty for a save that staged nothing.
public sealed record SelectionSaveResult(string? Path, IReadOnlyList<StagedApp> Apps)
{
    public static SelectionSaveResult Nothing { get; } = new(null, []);
}

public interface ISelectionSaveService
{
    Task<SelectionSaveResult> SaveAsync(BuilderTarget target, SelectionSet selections, SelectionSaveOptions? options = null);
}

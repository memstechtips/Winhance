using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IApplicationModeService
{
    WinhanceMode CurrentMode { get; }

    BuilderTarget CurrentBuilderTarget { get; }

    event EventHandler? ModeChanged;

    // The caller seeds the UI from current system state; no system writes occur in Builder mode.
    void EnterBuilderMode(BuilderTarget target);

    // Authored UI state is preserved; only card visibility and the Save output change.
    void SetBuilderTarget(BuilderTarget target);

    // Config Review has its own exit path (IConfigReviewModeService.ExitReviewMode).
    void EnterNormalMode();

    void RecordBuilderEdit(BuilderEdit edit);

    // Cleared when entering Builder, returning to Normal, or exiting Review.
    IReadOnlyCollection<BuilderEdit> GetBuilderEdits();

    // What a setting ViewModel re-reads to show authored values after its card was rebuilt from live state (a
    // filter or language change rebuilds every card), so it MUST answer from the same store Save writes.
    BuilderEdit? GetBuilderEdit(string settingId);

    // Separate from RecordBuilderEdit on purpose: not every input type is serialized into a BuilderEdit yet
    // (NumericRange and AC/DC power settings are not), but every one is authored work the user would lose on a mode switch.
    void MarkBuilderDirty();

    // This - not GetBuilderEdits - is the gate for "discard unsaved progress?" prompts.
    bool HasBuilderChanges { get; }
}

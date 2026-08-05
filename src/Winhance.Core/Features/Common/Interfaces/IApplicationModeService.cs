using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Owns the app-wide interaction mode. Single source of truth, registered as a
/// singleton. Widens the legacy boolean review flag into an explicit enum so
/// Normal / Builder / ConfigReview can be distinguished.
/// </summary>
public interface IApplicationModeService
{
    /// <summary>The current app-wide mode. Defaults to <see cref="WinhanceMode.Normal"/>.</summary>
    WinhanceMode CurrentMode { get; }

    /// <summary>
    /// The active Builder target. Only meaningful while <see cref="CurrentMode"/> is
    /// <see cref="WinhanceMode.Builder"/>. Defaults to <see cref="BuilderTarget.Config"/>.
    /// </summary>
    BuilderTarget CurrentBuilderTarget { get; }

    /// <summary>Raised whenever <see cref="CurrentMode"/> changes.</summary>
    event EventHandler? ModeChanged;

    /// <summary>
    /// Enter Builder mode with the given target. The caller is responsible for
    /// seeding the UI from current system state. No system writes occur while in
    /// Builder mode.
    /// </summary>
    void EnterBuilderMode(BuilderTarget target);

    /// <summary>
    /// Switch the Builder target without leaving Builder mode. Authored UI state
    /// is preserved; only card visibility and Save output change. No-op if not in
    /// Builder mode.
    /// </summary>
    void SetBuilderTarget(BuilderTarget target);

    /// <summary>
    /// Return to Normal mode from Builder (or any non-review mode). Config Review
    /// has its own exit path (<see cref="IConfigReviewModeService.ExitReviewMode"/>).
    /// </summary>
    void EnterNormalMode();

    /// <summary>
    /// Record (upsert by SettingId) a setting change made during the current Builder
    /// session. No-op semantics outside Builder are the caller's responsibility.
    /// </summary>
    void RecordBuilderEdit(BuilderEdit edit);

    /// <summary>
    /// The edits recorded during the current Builder session. Cleared when entering
    /// Builder, returning to Normal, or exiting Review.
    /// </summary>
    IReadOnlyCollection<BuilderEdit> GetBuilderEdits();

    /// <summary>
    /// The edit recorded for <paramref name="settingId"/> in the current authoring session, or
    /// null if that setting was not authored.
    ///
    /// This is what a setting ViewModel re-reads to show authored values after its card has been
    /// rebuilt from live system state, so it MUST answer from the same store as
    /// <see cref="GetBuilderEdits"/> — which is what Save writes. Two stores for "what the user
    /// authored" is precisely the defect this exists to close: a filter or language change during
    /// Builder rebuilds every card from the live machine, and before this the recorded edits
    /// survived unseen and were still saved, so the file disagreed with the screen.
    /// </summary>
    BuilderEdit? GetBuilderEdit(string settingId);

    /// <summary>
    /// Flags the current Builder session as having authored changes. Separate from
    /// <see cref="RecordBuilderEdit"/> on purpose: not every input type is serialized into a
    /// <see cref="BuilderEdit"/> yet (NumericRange and AC/DC power settings are not — see the
    /// scope note on <see cref="BuilderEdit"/>), but every one of them is still authored work
    /// the user would lose on a mode switch. No-op outside Builder mode.
    /// </summary>
    void MarkBuilderDirty();

    /// <summary>
    /// True when the current Builder session has any authored change, whether or not it produced
    /// a serializable <see cref="BuilderEdit"/>. This — not <see cref="GetBuilderEdits"/> — is the
    /// correct gate for "discard unsaved progress?" prompts. Cleared with the edits.
    /// </summary>
    bool HasBuilderChanges { get; }
}

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
}

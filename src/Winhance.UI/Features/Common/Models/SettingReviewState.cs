namespace Winhance.UI.Features.Common.Models;

/// <summary>
/// Everything a setting card holds that belongs to a config review rather than to the machine:
/// the diff being shown and the accept/reject decision made about it.
///
/// It is one object so that leaving review is one assignment to null. The shape it replaces was
/// nine separate properties on the ViewModel reset by a nine-line <c>ClearReviewState()</c>, which
/// meant a tenth property was one forgotten line away from surviving into the next mode — a leak
/// with no symptom at the point it was introduced. Fields added here cannot outlive the review,
/// because nothing resets fields at all; the whole object is dropped.
///
/// Mutable and unshared on purpose: exactly one card owns each instance, and it is discarded rather
/// than reset.
/// </summary>
public sealed class SettingReviewState
{
    /// <summary>Whether the config differs from the live value, so a diff bar is shown.</summary>
    public bool HasDiff { get; set; }

    /// <summary>The rendered "Current: X → Config: Y" line.</summary>
    public string? DiffMessage { get; set; }

    /// <summary>The user accepted this diff.</summary>
    public bool IsApproved { get; set; }

    /// <summary>The user rejected this diff.</summary>
    public bool IsRejected { get; set; }

    /// <summary>
    /// Whether an action (a wallpaper apply, say) is offered alongside the value diff, in its own
    /// bar with its own accept/reject.
    /// </summary>
    public bool HasAction { get; set; }

    /// <summary>The action's confirmation line.</summary>
    public string? ActionMessage { get; set; }

    /// <summary>The user accepted the action.</summary>
    public bool IsActionApproved { get; set; }

    /// <summary>The user rejected the action.</summary>
    public bool IsActionRejected { get; set; }
}

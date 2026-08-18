namespace Winhance.UI.Features.Common.Models;

// One object so that leaving review is one assignment to null: nine separate properties reset by a nine-line
// ClearReviewState() meant a tenth was one forgotten line away from leaking into the next mode. Mutable and
// unshared on purpose - exactly one card owns each instance, and it is discarded rather than reset.
public sealed class SettingReviewState
{
    public bool HasDiff { get; set; }

    public string? DiffMessage { get; set; }

    public bool IsApproved { get; set; }

    public bool IsRejected { get; set; }

    public bool HasAction { get; set; }

    public string? ActionMessage { get; set; }

    public bool IsActionApproved { get; set; }

    public bool IsActionRejected { get; set; }
}

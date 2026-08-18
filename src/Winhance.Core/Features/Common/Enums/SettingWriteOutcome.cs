namespace Winhance.Core.Features.Common.Enums;

// Every mode's answer to an edit lands on one of these three, so the bookkeeping after a change is written once
// per input shape rather than once per (input shape x mode).
public enum SettingWriteOutcome
{
    // Commit the value to the card and consider a restart banner - something on this system changed.
    Applied,

    // Commit the value (the user must see what they authored) but no restart banner: nothing on this system changed.
    Recorded,

    // Cancelled, failed, or forbidden by the mode: the caller must put the card back the way it was.
    Rejected,
}

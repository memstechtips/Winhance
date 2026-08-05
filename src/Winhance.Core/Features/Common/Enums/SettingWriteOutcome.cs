namespace Winhance.Core.Features.Common.Enums;

/// <summary>
/// What became of one user edit to one setting.
///
/// Callers read this instead of asking which mode is active. Every mode's answer to an edit lands
/// on one of these three, so the bookkeeping that follows a change is written once per input shape
/// rather than once per (input shape x mode) — which is what let two of the five Builder branches
/// drift into recording nothing at all.
/// </summary>
public enum SettingWriteOutcome
{
    /// <summary>
    /// The edit reached the machine. Commit the new value to the card, and consider a restart
    /// banner — something on this system actually changed.
    /// </summary>
    Applied,

    /// <summary>
    /// The edit was recorded as authored intent and deliberately not applied. Commit the new value
    /// to the card — the user has to see what they authored — but show no restart banner, because
    /// nothing on this system changed.
    /// </summary>
    Recorded,

    /// <summary>
    /// Nothing happened: the user cancelled the confirmation, the apply failed, or the active mode
    /// forbids editing. The caller must put the card back the way it was.
    /// </summary>
    Rejected,
}
